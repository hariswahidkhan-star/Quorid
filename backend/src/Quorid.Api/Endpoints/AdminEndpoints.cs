using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Quorid.Api.Authorization;
using Quorid.Application.Admin;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Common.Security; // Permissions catalog
using Quorid.Domain.Entities;
using Quorid.Domain.Enums;
using Quorid.Infrastructure.Persistence;

namespace Quorid.Api.Endpoints;

/// <summary>
/// Module 12 — Administration (spec §Module 12). User management, role &amp;
/// permission editing, organization / entity configuration, an onboarding
/// checklist, and billing (plan comparison, usage, invoices).
/// </summary>
public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization();

        // Users — mutations require the ManageUsers permission
        group.MapGet("/users", ListUsersAsync).RequireAuthorization(Policies.ManageUsers);
        group.MapPost("/users", InviteUserAsync).RequireAuthorization(Policies.ManageUsers);
        group.MapPut("/users/{id:guid}", UpdateUserAsync).RequireAuthorization(Policies.ManageUsers);

        // Roles & permissions — require the ManagePermissions permission
        group.MapGet("/roles", ListRolesAsync);
        group.MapGet("/permissions", PermissionsCatalog);
        group.MapPost("/roles", CreateRoleAsync).RequireAuthorization(Policies.ManagePermissions);
        group.MapPut("/roles/{id:guid}", UpdateRoleAsync).RequireAuthorization(Policies.ManagePermissions);
        group.MapDelete("/roles/{id:guid}", DeleteRoleAsync).RequireAuthorization(Policies.ManagePermissions);

        // Organization & entities — configuration requires ConfigureSettings
        group.MapGet("/organization", GetOrganizationAsync);
        group.MapPut("/organization", UpdateOrganizationAsync).RequireAuthorization(Policies.ConfigureSettings);
        group.MapGet("/entities", ListEntitiesAsync);
        group.MapPost("/entities", CreateEntityAsync).RequireAuthorization(Policies.ConfigureSettings);
        group.MapPut("/entities/{id:guid}", UpdateEntityAsync).RequireAuthorization(Policies.ConfigureSettings);

        // Onboarding & billing — plan changes require ManageBilling
        group.MapGet("/onboarding", OnboardingAsync);
        group.MapGet("/billing", BillingAsync);
        group.MapPut("/billing/plan", ChangePlanAsync).RequireAuthorization(Policies.ManageBilling);

        return app;
    }

    // ---- Users ----

    private static async Task<IResult> ListUsersAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var rows = await db.Users.AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id, u.Email, u.FullName, u.RoleId,
                RoleName = u.Role != null ? u.Role.Name : null,
                u.Status, u.LastLoginAt, u.MfaEnabled,
            })
            .ToListAsync(ct);

        var list = rows.Select(u => new UserListItemDto(
            u.Id, u.Email, u.FullName, u.RoleId, u.RoleName, u.Status.ToString(), u.LastLoginAt, u.MfaEnabled)).ToList();
        return Results.Ok(list);
    }

    private static async Task<IResult> InviteUserAsync(
        InviteUserRequest request, ApplicationDbContext db, ICurrentUser current, IPasswordHasher hasher, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.FullName))
            return Results.BadRequest(new { error = "Email and full name are required." });
        if (current.TenantId is not { } tenantId)
            return Results.Unauthorized();

        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return Results.Conflict(new { error = "A user with that email already exists." });

        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == request.RoleId, ct);
        if (role is null) return Results.BadRequest(new { error = "Unknown role." });

        var entityId = current.EntityId
            ?? await db.Entities.Select(e => e.Id).FirstOrDefaultAsync(ct);

        var tempPassword = GenerateTempPassword();
        var user = new User
        {
            TenantId = tenantId,
            EntityId = entityId,
            Email = email,
            FullName = request.FullName.Trim(),
            RoleId = role.Id,
            DepartmentId = request.DepartmentId,
            Status = UserStatus.Invited,
            PasswordHash = hasher.Hash(tempPassword),
            CreatedBy = current.UserId,
        };
        db.Users.Add(user);
        db.AuditLogs.Add(Audit(tenantId, current.UserId, "USER_INVITED", user.Id, nameof(User)));
        await db.SaveChangesAsync(ct);

        var dto = new UserListItemDto(user.Id, user.Email, user.FullName, user.RoleId, role.Name,
            user.Status.ToString(), null, false);
        return Results.Ok(new InviteUserResponse(dto, tempPassword));
    }

    private static async Task<IResult> UpdateUserAsync(
        Guid id, UpdateUserRequest request, ApplicationDbContext db, ICurrentUser current, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return Results.NotFound();

        if (request.FullName is not null) user.FullName = request.FullName.Trim();
        if (request.RoleId is { } roleId)
        {
            if (!await db.Roles.AnyAsync(r => r.Id == roleId, ct))
                return Results.BadRequest(new { error = "Unknown role." });
            user.RoleId = roleId;
        }
        if (request.DepartmentId is { } deptId) user.DepartmentId = deptId;
        if (request.Status is not null && Enum.TryParse<UserStatus>(request.Status, ignoreCase: true, out var status))
            user.Status = status;

        db.AuditLogs.Add(Audit(user.TenantId, current.UserId, "USER_UPDATED", user.Id, nameof(User)));
        await db.SaveChangesAsync(ct);

        var roleName = await db.Roles.Where(r => r.Id == user.RoleId).Select(r => r.Name).FirstOrDefaultAsync(ct);
        return Results.Ok(new UserListItemDto(user.Id, user.Email, user.FullName, user.RoleId, roleName,
            user.Status.ToString(), user.LastLoginAt, user.MfaEnabled));
    }

    // ---- Roles ----

    private static async Task<IResult> ListRolesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var counts = (await db.Users.AsNoTracking().Select(u => u.RoleId).ToListAsync(ct))
            .GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());

        var list = roles.Select(r => new RoleDto(
            r.Id, r.Name, r.Description, r.IsSystem,
            NormalizePermissions(Deserialize(r.Permissions)),
            counts.TryGetValue(r.Id, out var c) ? c : 0)).ToList();
        return Results.Ok(list);
    }

    private static IResult PermissionsCatalog() =>
        Results.Ok(Permissions.All.Select(p => new PermissionDto(p, Label(p))).ToList());

    private static async Task<IResult> CreateRoleAsync(
        CreateRoleRequest request, ApplicationDbContext db, ICurrentUser current, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Role name is required." });
        var name = request.Name.Trim();
        if (await db.Roles.AnyAsync(r => r.Name == name, ct))
            return Results.Conflict(new { error = "A role with that name already exists." });

        var role = new Role
        {
            Name = name,
            Description = request.Description,
            IsSystem = false,
            Permissions = JsonSerializer.Serialize(NormalizePermissions(request.Permissions)),
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new RoleDto(role.Id, role.Name, role.Description, false,
            NormalizePermissions(Deserialize(role.Permissions)), 0));
    }

    private static async Task<IResult> UpdateRoleAsync(
        Guid id, UpdateRoleRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return Results.NotFound();

        if (request.Description is not null) role.Description = request.Description;
        if (request.Permissions is not null)
        {
            if (role.IsSystem)
                return Results.BadRequest(new { error = "System role permissions cannot be modified." });
            role.Permissions = JsonSerializer.Serialize(NormalizePermissions(request.Permissions));
        }

        await db.SaveChangesAsync(ct);
        var count = await db.Users.CountAsync(u => u.RoleId == role.Id, ct);
        return Results.Ok(new RoleDto(role.Id, role.Name, role.Description, role.IsSystem,
            NormalizePermissions(Deserialize(role.Permissions)), count));
    }

    private static async Task<IResult> DeleteRoleAsync(Guid id, ApplicationDbContext db, CancellationToken ct)
    {
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (role is null) return Results.NotFound();
        if (role.IsSystem) return Results.BadRequest(new { error = "System roles cannot be deleted." });
        if (await db.Users.AnyAsync(u => u.RoleId == id, ct))
            return Results.BadRequest(new { error = "Reassign users off this role before deleting it." });

        db.Roles.Remove(role);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    // ---- Organization & entities ----

    private static async Task<IResult> GetOrganizationAsync(ApplicationDbContext db, ICurrentUser current, CancellationToken ct)
    {
        if (current.TenantId is not { } tenantId) return Results.Unauthorized();
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return Results.NotFound();

        var entityCount = await db.Entities.CountAsync(ct);
        var userCount = await db.Users.CountAsync(ct);
        return Results.Ok(new OrganizationDto(
            tenant.Id, tenant.Name, tenant.Domain, tenant.Plan.ToString(), tenant.Status.ToString(),
            entityCount, userCount));
    }

    private static async Task<IResult> UpdateOrganizationAsync(
        UpdateOrganizationRequest request, ApplicationDbContext db, ICurrentUser current, CancellationToken ct)
    {
        if (current.TenantId is not { } tenantId) return Results.Unauthorized();
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return Results.NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name)) tenant.Name = request.Name.Trim();
        if (request.Domain is not null) tenant.Domain = string.IsNullOrWhiteSpace(request.Domain) ? null : request.Domain.Trim();

        await db.SaveChangesAsync(ct);
        return Results.Ok(new OrganizationDto(tenant.Id, tenant.Name, tenant.Domain,
            tenant.Plan.ToString(), tenant.Status.ToString(),
            await db.Entities.CountAsync(ct), await db.Users.CountAsync(ct)));
    }

    private static async Task<IResult> ListEntitiesAsync(ApplicationDbContext db, CancellationToken ct)
    {
        var rows = await db.Entities.AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new { e.Id, e.Name, e.Code, e.Type, e.State, e.Ein, e.Status })
            .ToListAsync(ct);
        var list = rows.Select(e => new EntityDto(
            e.Id, e.Name, e.Code, e.Type.ToString(), e.State, e.Ein, e.Status.ToString())).ToList();
        return Results.Ok(list);
    }

    private static async Task<IResult> CreateEntityAsync(
        CreateEntityRequest request, ApplicationDbContext db, ICurrentUser current, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Results.BadRequest(new { error = "Entity name is required." });
        if (current.TenantId is not { } tenantId) return Results.Unauthorized();

        var entity = new Entity
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Code = await UniqueEntityCodeAsync(db, request.Name, ct),
            Type = ParseEnum(request.Type, EntityType.Llc),
            State = request.State,
            Ein = request.Ein,
            Status = EntityStatus.Active,
        };
        db.Entities.Add(entity);
        await db.SaveChangesAsync(ct);

        return Results.Ok(new EntityDto(entity.Id, entity.Name, entity.Code, entity.Type.ToString(),
            entity.State, entity.Ein, entity.Status.ToString()));
    }

    private static async Task<IResult> UpdateEntityAsync(
        Guid id, UpdateEntityRequest request, ApplicationDbContext db, CancellationToken ct)
    {
        var entity = await db.Entities.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return Results.NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name)) entity.Name = request.Name.Trim();
        if (request.Type is not null) entity.Type = ParseEnum(request.Type, entity.Type);
        if (request.State is not null) entity.State = request.State;
        if (request.Ein is not null) entity.Ein = request.Ein;
        if (request.Address is not null) entity.Address = request.Address;
        if (request.Phone is not null) entity.Phone = request.Phone;
        if (request.Website is not null) entity.Website = request.Website;
        if (request.Status is not null) entity.Status = ParseEnum(request.Status, entity.Status);

        await db.SaveChangesAsync(ct);
        return Results.Ok(new EntityDto(entity.Id, entity.Name, entity.Code, entity.Type.ToString(),
            entity.State, entity.Ein, entity.Status.ToString()));
    }

    // ---- Onboarding ----

    private static async Task<IResult> OnboardingAsync(ApplicationDbContext db, ICurrentUser current, CancellationToken ct)
    {
        var entity = current.EntityId is { } eid
            ? await db.Entities.AsNoTracking().FirstOrDefaultAsync(e => e.Id == eid, ct)
            : await db.Entities.AsNoTracking().FirstOrDefaultAsync(ct);

        var profileComplete = entity is not null &&
            !string.IsNullOrWhiteSpace(entity.State) &&
            !string.IsNullOrWhiteSpace(entity.Ein) &&
            !string.IsNullOrWhiteSpace(entity.Address);
        var hasTeam = await db.Users.CountAsync(ct) > 1;
        var hasDocuments = await db.Documents.AnyAsync(d => d.Status != DocumentStatus.Archived, ct);
        // Equality on the two verified tiers avoids a relational comparison on the
        // string-converted enum column (keeps the translation provider-safe).
        var hasVerified = await db.Documents.AnyAsync(
            d => d.Status != DocumentStatus.Archived &&
                 (d.VerificationTier == VerificationTier.T3 || d.VerificationTier == VerificationTier.T4), ct);
        var hasCompliance = await db.ComplianceFrameworks.AnyAsync(ct);
        var hasRoom = await db.DataRooms.AnyAsync(ct);

        var steps = new List<OnboardingStepDto>
        {
            new("profile", "Complete your organization profile",
                "Add your entity's state, EIN, and address.", profileComplete, "/admin"),
            new("team", "Invite your team", "Add colleagues and assign roles.", hasTeam, "/admin"),
            new("documents", "Upload your first documents", "Capture and classify documents into the vault.", hasDocuments, "/documents"),
            new("verify", "Verify a document", "Raise a document to Tier 3 (source-verified).", hasVerified, "/vault"),
            new("compliance", "Set up compliance", "Review a framework and close your gaps.", hasCompliance, "/compliance"),
            new("room", "Create a data room", "Share documents securely with an external party.", hasRoom, "/rooms"),
        };

        var done = steps.Count(s => s.Done);
        var percent = steps.Count == 0 ? 0 : (int)Math.Round(100.0 * done / steps.Count);
        return Results.Ok(new OnboardingDto(percent, steps));
    }

    // ---- Billing ----

    private static async Task<IResult> BillingAsync(ApplicationDbContext db, ICurrentUser current, CancellationToken ct)
    {
        if (current.TenantId is not { } tenantId) return Results.Unauthorized();
        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return Results.NotFound();

        var users = await db.Users.CountAsync(ct);
        var documents = await db.Documents.CountAsync(d => d.Status != DocumentStatus.Archived, ct);
        var storage = await db.Documents.AsNoTracking()
            .Where(d => d.Status != DocumentStatus.Archived)
            .SumAsync(d => (long?)d.FileSizeBytes, ct) ?? 0L;
        var rooms = await db.DataRooms.CountAsync(ct);

        var currentPlan = PlanCatalog.For(tenant.Plan);
        var invoices = BuildInvoices(currentPlan);

        return Results.Ok(new BillingDto(
            tenant.Plan.ToString(), currentPlan, PlanCatalog.Plans,
            new UsageDto(users, documents, storage, rooms), invoices));
    }

    private static async Task<IResult> ChangePlanAsync(
        ChangePlanRequest request, ApplicationDbContext db, ICurrentUser current, CancellationToken ct)
    {
        if (current.TenantId is not { } tenantId) return Results.Unauthorized();
        if (!Enum.TryParse<PlanTier>(request.Plan, ignoreCase: true, out var plan))
            return Results.BadRequest(new { error = "Unknown plan." });

        var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return Results.NotFound();

        tenant.Plan = plan;
        db.AuditLogs.Add(Audit(tenantId, current.UserId, "PLAN_CHANGED", tenantId, nameof(Tenant)));
        await db.SaveChangesAsync(ct);
        return Results.Ok(new { plan = tenant.Plan.ToString() });
    }

    // ---- helpers ----

    private static List<InvoiceDto> BuildInvoices(PlanDto plan)
    {
        if (plan.PricePerMonth <= 0m) return new List<InvoiceDto>();

        var invoices = new List<InvoiceDto>();
        var month = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        for (var i = 1; i <= 3; i++)
        {
            var date = month.AddMonths(-i);
            invoices.Add(new InvoiceDto(
                $"INV-{date:yyyy-MM}", DateOnly.FromDateTime(date), plan.PricePerMonth, "Paid"));
        }
        return invoices;
    }

    private static Dictionary<string, bool> Deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new Dictionary<string, bool>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, bool>();
        }
    }

    private static Dictionary<string, bool> NormalizePermissions(IReadOnlyDictionary<string, bool>? requested)
    {
        var result = new Dictionary<string, bool>();
        foreach (var key in Permissions.All)
            result[key] = requested is not null && requested.TryGetValue(key, out var v) && v;
        return result;
    }

    private static async Task<string> UniqueEntityCodeAsync(ApplicationDbContext db, string name, CancellationToken ct)
    {
        var letters = new string(name.Where(char.IsLetter).Take(3).ToArray()).ToUpperInvariant();
        if (letters.Length < 3) letters = (letters + "XXX")[..3];
        var n = 1;
        string code;
        do
        {
            code = $"{letters}-{n:D3}";
            n++;
        }
        while (await db.Entities.AnyAsync(e => e.Code == code, ct));
        return code;
    }

    private static string GenerateTempPassword() =>
        "Qd" + Guid.NewGuid().ToString("N")[..8] + "!7Za";

    private static TEnum ParseEnum<TEnum>(string? value, TEnum fallback) where TEnum : struct, Enum =>
        Enum.TryParse<TEnum>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static string Label(string permissionKey) => permissionKey switch
    {
        Permissions.ViewDocuments => "View documents",
        Permissions.Download => "Download",
        Permissions.Upload => "Upload",
        Permissions.EditMetadata => "Edit metadata",
        Permissions.DeleteDocuments => "Delete documents",
        Permissions.CreateRooms => "Create data rooms",
        Permissions.ManageUsers => "Manage users",
        Permissions.ViewAuditLog => "View audit log",
        Permissions.ManagePermissions => "Manage roles & permissions",
        Permissions.ConfigureSettings => "Configure settings",
        Permissions.ShareExternally => "Share externally",
        Permissions.ApproveDocuments => "Approve documents",
        Permissions.ManageCompliance => "Manage compliance",
        Permissions.ExportData => "Export data",
        Permissions.ManageBilling => "Manage billing",
        _ => permissionKey,
    };

    private static AuditLog Audit(Guid tenantId, Guid? userId, string action, Guid resourceId, string resourceType) =>
        new()
        {
            TenantId = tenantId, UserId = userId, Action = action, ResourceType = resourceType, ResourceId = resourceId,
        };
}
