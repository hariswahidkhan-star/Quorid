namespace Quorid.Application.Vault;

/// <summary>Aggregated vault profile and cross-validation for an entity.</summary>
public interface IVaultService
{
    Task<VaultProfileDto?> GetProfileAsync(Guid entityId, CancellationToken cancellationToken = default);

    Task<CrossValidationReportDto?> CrossValidateAsync(Guid entityId, CancellationToken cancellationToken = default);
}
