using System.Reflection;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Quorid.Application.Common.Interfaces;
using Quorid.Domain.Common;
using Quorid.Domain.Entities;

namespace Quorid.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work. Enforces multi-tenant isolation with a global query
/// filter on every <see cref="ITenantScoped"/> entity, and maps all tables and
/// columns to snake_case to match the spec DDL.
/// </summary>
public class ApplicationDbContext : DbContext
{
    private readonly ITenantContext _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>Tenant applied by global query filters. Guid.Empty means "no tenant".</summary>
    public Guid CurrentTenantId => _tenantContext.TenantId ?? Guid.Empty;

    // Core
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Entity> Entities => Set<Entity>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
    public DbSet<Department> Departments => Set<Department>();

    // Documents
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<ExtractedField> ExtractedFields => Set<ExtractedField>();
    public DbSet<DocumentTag> DocumentTags => Set<DocumentTag>();
    public DbSet<DocumentTaxonomy> DocumentTaxonomies => Set<DocumentTaxonomy>();
    public DbSet<UploadJob> UploadJobs => Set<UploadJob>();

    // Rooms
    public DbSet<RoomType> RoomTypes => Set<RoomType>();
    public DbSet<RoomTemplate> RoomTemplates => Set<RoomTemplate>();
    public DbSet<DataRoom> DataRooms => Set<DataRoom>();
    public DbSet<RoomFolder> RoomFolders => Set<RoomFolder>();
    public DbSet<RoomDocument> RoomDocuments => Set<RoomDocument>();
    public DbSet<RoomGuest> RoomGuests => Set<RoomGuest>();
    public DbSet<GuestSession> GuestSessions => Set<GuestSession>();
    public DbSet<DocumentView> DocumentViews => Set<DocumentView>();
    public DbSet<QaQuestion> QaQuestions => Set<QaQuestion>();
    public DbSet<GroupRoomAccess> GroupRoomAccess => Set<GroupRoomAccess>();

    // System
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();

    // Sharing
    public DbSet<SharingRecord> SharingRecords => Set<SharingRecord>();
    public DbSet<ShareView> ShareViews => Set<ShareView>();

    // Compliance
    public DbSet<ComplianceFramework> ComplianceFrameworks => Set<ComplianceFramework>();
    public DbSet<ComplianceRequirement> ComplianceRequirements => Set<ComplianceRequirement>();

    // Engagements (Projects / Vendors / Clients)
    public DbSet<Engagement> Engagements => Set<Engagement>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();

    // Proposals
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<ProposalDocument> ProposalDocuments => Set<ProposalDocument>();

    // Document Creation studio (Module 4)
    public DbSet<DocumentTemplate> DocumentTemplates => Set<DocumentTemplate>();
    public DbSet<DocumentDraft> DocumentDrafts => Set<DocumentDraft>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(40);
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Unique constraints & indexes (spec §6, Screens 1/4/5) ---
        modelBuilder.Entity<Entity>().HasIndex(e => new { e.TenantId, e.Code }).IsUnique();
        modelBuilder.Entity<User>().HasIndex(e => new { e.TenantId, e.Email }).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(e => new { e.TenantId, e.Name }).IsUnique();
        modelBuilder.Entity<Group>().HasIndex(e => new { e.EntityId, e.Name }).IsUnique();
        modelBuilder.Entity<GroupMember>().HasIndex(e => new { e.GroupId, e.UserId }).IsUnique();
        modelBuilder.Entity<DataRoom>().HasIndex(e => new { e.TenantId, e.EntityId, e.Name }).IsUnique();
        modelBuilder.Entity<RoomDocument>().HasIndex(e => new { e.RoomId, e.DocumentId }).IsUnique();
        modelBuilder.Entity<RoomGuest>().HasIndex(e => new { e.RoomId, e.Email }).IsUnique();
        modelBuilder.Entity<RoomGuest>().HasIndex(e => e.AccessToken);
        modelBuilder.Entity<RoomGuest>().HasIndex(e => e.Status);
        modelBuilder.Entity<RoomFolder>().HasIndex(e => new { e.RoomId, e.DisplayOrder });
        modelBuilder.Entity<Document>().HasIndex(e => new { e.TenantId, e.EntityId });
        modelBuilder.Entity<AuditLog>().HasIndex(e => new { e.TenantId, e.RoomId });
        modelBuilder.Entity<AuditLog>().HasIndex(e => e.ResourceId);
        modelBuilder.Entity<SharingRecord>().HasIndex(e => e.AccessToken).IsUnique();
        modelBuilder.Entity<SharingRecord>().HasIndex(e => e.DocumentId);
        modelBuilder.Entity<ShareView>().HasIndex(e => e.SharingRecordId);
        modelBuilder.Entity<ComplianceFramework>().HasIndex(e => new { e.TenantId, e.Code });
        modelBuilder.Entity<ComplianceRequirement>().HasIndex(e => e.FrameworkId);
        modelBuilder.Entity<Engagement>().HasIndex(e => new { e.TenantId, e.Type });
        modelBuilder.Entity<Engagement>().HasIndex(e => e.AccessToken);
        modelBuilder.Entity<ChecklistItem>().HasIndex(e => e.EngagementId);
        modelBuilder.Entity<Proposal>().HasIndex(e => new { e.TenantId, e.Stage });
        modelBuilder.Entity<ProposalDocument>().HasIndex(e => e.ProposalId);
        modelBuilder.Entity<DocumentTemplate>().HasIndex(e => new { e.TenantId, e.Key });
        modelBuilder.Entity<DocumentDraft>().HasIndex(e => new { e.TenantId, e.Status });

        // --- Relationship delete behaviour ---
        modelBuilder.Entity<RoomFolder>()
            .HasOne(f => f.Room).WithMany(r => r.Folders)
            .HasForeignKey(f => f.RoomId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RoomDocument>()
            .HasOne(d => d.Room).WithMany(r => r.Documents)
            .HasForeignKey(d => d.RoomId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<RoomGuest>()
            .HasOne(g => g.Room).WithMany(r => r.Guests)
            .HasForeignKey(g => g.RoomId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DocumentVersion>()
            .HasOne(v => v.Document).WithMany(d => d.Versions)
            .HasForeignKey(v => v.DocumentId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ExtractedField>()
            .HasOne(f => f.Document).WithMany(d => d.ExtractedFields)
            .HasForeignKey(f => f.DocumentId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<DocumentTag>()
            .HasOne<Document>().WithMany(d => d.Tags)
            .HasForeignKey(t => t.DocumentId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<GroupMember>()
            .HasOne(m => m.Group).WithMany(g => g.Members)
            .HasForeignKey(m => m.GroupId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ChecklistItem>()
            .HasOne(c => c.Engagement).WithMany(e => e.Checklist)
            .HasForeignKey(c => c.EngagementId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProposalDocument>()
            .HasOne(pd => pd.Proposal).WithMany(p => p.Documents)
            .HasForeignKey(pd => pd.ProposalId).OnDelete(DeleteBehavior.Cascade);

        // --- Selected column caps (indexed string columns need a length in MySQL) ---
        modelBuilder.Entity<User>().Property(u => u.Email).HasMaxLength(255);
        modelBuilder.Entity<Entity>().Property(e => e.Code).HasMaxLength(50);
        modelBuilder.Entity<Role>().Property(r => r.Permissions).HasColumnType("json");
        modelBuilder.Entity<Role>().Property(r => r.Name).HasMaxLength(100);
        modelBuilder.Entity<Group>().Property(g => g.Name).HasMaxLength(100);
        modelBuilder.Entity<DataRoom>().Property(r => r.Name).HasMaxLength(100);
        modelBuilder.Entity<RoomGuest>().Property(g => g.Email).HasMaxLength(255);
        modelBuilder.Entity<RoomGuest>().Property(g => g.AccessToken).HasMaxLength(128);
        modelBuilder.Entity<SharingRecord>().Property(s => s.AccessToken).HasMaxLength(128);
        modelBuilder.Entity<SharingRecord>().Property(s => s.RecipientEmail).HasMaxLength(255);
        modelBuilder.Entity<ComplianceFramework>().Property(f => f.Name).HasMaxLength(150);
        modelBuilder.Entity<ComplianceFramework>().Property(f => f.Code).HasMaxLength(50);
        modelBuilder.Entity<Engagement>().Property(e => e.AccessToken).HasMaxLength(128);
        modelBuilder.Entity<DocumentTemplate>().Property(t => t.Key).HasMaxLength(100);
        modelBuilder.Entity<DocumentTemplate>().Property(t => t.Name).HasMaxLength(150);
        modelBuilder.Entity<DocumentTemplate>().Property(t => t.Category).HasMaxLength(60);
        modelBuilder.Entity<DocumentDraft>().Property(d => d.Title).HasMaxLength(255);
        modelBuilder.Entity<DocumentDraft>().Property(d => d.TemplateKey).HasMaxLength(100);

        // --- Global multi-tenant query filter ---
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType))
            {
                typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType)
                    .Invoke(this, new object[] { modelBuilder });
            }
        }

        // --- snake_case naming for tables and columns ---
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (tableName is not null)
                entityType.SetTableName(ToSnakeCase(tableName));

            foreach (var property in entityType.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder builder) where TEntity : class, ITenantScoped
    {
        builder.Entity<TEntity>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        var sb = new StringBuilder(input.Length + 8);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (!char.IsUpper(input[i - 1]) ||
                              (i + 1 < input.Length && char.IsLower(input[i + 1]))))
                {
                    sb.Append('_');
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }
}
