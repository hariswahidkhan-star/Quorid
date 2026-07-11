using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Quorid.Application.Common.Interfaces;

namespace Quorid.Infrastructure.Storage;

/// <summary>
/// Production <see cref="IFileStorage"/> backed by Amazon S3 (AES-256 at rest,
/// per-tenant key prefix). Selected when <c>Storage:Provider</c> is <c>"S3"</c>;
/// otherwise <see cref="LocalFileStorage"/> is used. The database still stores only
/// the returned key — never the bytes.
/// </summary>
public sealed class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly StorageOptions _options;

    public S3FileStorage(IAmazonS3 s3, IOptions<StorageOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    public async Task<StoredFile> SaveAsync(
        Stream content, string fileName, string contentType, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        var key = $"{tenantId:N}/{Guid.NewGuid():N}{extension}";

        // Buffer so we can set a content length and let S3 read a seekable stream.
        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var request = new PutObjectRequest
        {
            BucketName = _options.Bucket,
            Key = key,
            InputStream = buffer,
            ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            AutoCloseStream = false,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256,
        };
        await _s3.PutObjectAsync(request, cancellationToken);

        return new StoredFile(key, _options.Bucket, buffer.Length);
    }

    public async Task<Stream?> OpenAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _s3.GetObjectAsync(_options.Bucket, storageKey, cancellationToken);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        await _s3.DeleteObjectAsync(_options.Bucket, storageKey, cancellationToken);
    }
}
