using Microsoft.Extensions.Options;
using Quorid.Application.Common.Interfaces;

namespace Quorid.Infrastructure.Storage;

/// <summary>
/// Dev file store writing to local disk under a per-tenant prefix. Production
/// replaces this with an S3-backed implementation of <see cref="IFileStorage"/>.
/// </summary>
public class LocalFileStorage : IFileStorage
{
    private readonly StorageOptions _options;

    public LocalFileStorage(IOptions<StorageOptions> options)
    {
        _options = options.Value;
    }

    public async Task<StoredFile> SaveAsync(
        Stream content, string fileName, string contentType, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        var key = $"{tenantId:N}/{Guid.NewGuid():N}{extension}";

        var fullPath = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return new StoredFile(key, _options.Bucket, fileStream.Length);
    }

    public Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(storageKey);
        Stream? stream = File.Exists(fullPath) ? File.OpenRead(fullPath) : null;
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(storageKey);
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolvePath(string storageKey) =>
        Path.Combine(_options.RootPath, storageKey.Replace('/', Path.DirectorySeparatorChar));
}
