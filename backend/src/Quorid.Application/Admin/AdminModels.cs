namespace Quorid.Application.Admin;

// ---- Users ----

public record UserListItemDto(
    Guid Id, string Email, string FullName, Guid RoleId, string? RoleName,
    string Status, DateTime? LastLoginAt, bool MfaEnabled);

public record InviteUserRequest(string Email, string FullName, Guid RoleId, Guid? DepartmentId);

public record InviteUserResponse(UserListItemDto User, string TemporaryPassword);

public record UpdateUserRequest(string? FullName, Guid? RoleId, string? Status, Guid? DepartmentId);

// ---- Roles ----

public record RoleDto(
    Guid Id, string Name, string? Description, bool IsSystem,
    IReadOnlyDictionary<string, bool> Permissions, int UserCount);

public record PermissionDto(string Key, string Label);

public record CreateRoleRequest(string Name, string? Description, Dictionary<string, bool>? Permissions);

public record UpdateRoleRequest(string? Description, Dictionary<string, bool>? Permissions);

// ---- Organization & entities ----

public record OrganizationDto(
    Guid TenantId, string Name, string? Domain, string Plan, string Status, int EntityCount, int UserCount);

public record UpdateOrganizationRequest(string? Name, string? Domain);

public record EntityDto(
    Guid Id, string Name, string Code, string Type, string? State, string? Ein, string Status);

public record CreateEntityRequest(string Name, string? Type, string? State, string? Ein);

public record UpdateEntityRequest(
    string? Name, string? Type, string? State, string? Ein,
    string? Address, string? Phone, string? Website, string? Status);

// ---- Onboarding ----

public record OnboardingStepDto(string Key, string Title, string Description, bool Done, string ActionPath);

public record OnboardingDto(int CompletionPercent, IReadOnlyList<OnboardingStepDto> Steps);

// ---- Billing ----

public record PlanDto(
    string Key, string Name, decimal PricePerMonth,
    int UserLimit, int DocumentLimit, int StorageGb, int RoomLimit);

public record UsageDto(int Users, int Documents, long StorageBytes, int Rooms);

public record InvoiceDto(string Number, DateOnly Date, decimal Amount, string Status);

public record BillingDto(
    string CurrentPlan, PlanDto CurrentPlanDetail, IReadOnlyList<PlanDto> Plans,
    UsageDto Usage, IReadOnlyList<InvoiceDto> Invoices);

public record ChangePlanRequest(string Plan);
