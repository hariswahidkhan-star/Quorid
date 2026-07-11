using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Quorid.Application.Common.Interfaces;
using Quorid.Domain.Common;
using Quorid.Domain.Entities;

namespace Quorid.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps CreatedAt/UpdatedAt, assigns TenantId on insert, and enforces the
/// append-only rule on <see cref="AuditLog"/>.
/// </summary>
public class AuditableEntitySaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenantContext;

    public AuditableEntitySaveChangesInterceptor(ITenantContext tenantContext)
    {
        _tenantContext = tenantContext;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null) return;

        var now = DateTime.UtcNow;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "audit_log is append-only; entries cannot be modified or deleted.");
            }

            if (entry.State == EntityState.Added && entry.Entity is BaseEntity baseEntity &&
                baseEntity.CreatedAt == default)
            {
                baseEntity.CreatedAt = now;
            }

            if (entry.State == EntityState.Added && entry.Entity is ITenantScoped tenantScoped &&
                tenantScoped.TenantId == Guid.Empty && _tenantContext.TenantId is Guid tenantId)
            {
                tenantScoped.TenantId = tenantId;
            }

            if (entry.State is EntityState.Added or EntityState.Modified &&
                entry.Entity is IAuditableEntity auditable)
            {
                auditable.UpdatedAt = now;
            }
        }
    }
}
