namespace Quorid.Application.Common.Interfaces;

/// <summary>
/// Abstraction over document blob storage. Files never live in the database —
/// only the returned storage key does. The dev implementation writes to local
/// disk; production swaps in S3 (AES-256 at rest) behind this same interface.
/// </summary>
public interface IFileStorage
{
    Task<StoredFile> SaveAsync(
        Stream content, string fileName, string contentType, Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default);
}

public record StoredFile(string StorageKey, string Bucket, long SizeBytes);
