namespace Quorid.Infrastructure.Storage;

public class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Local directory for the dev file store.</summary>
    public string RootPath { get; set; } = "storage-data";

    public string Bucket { get; set; } = "quorid-local";

    /// <summary>Maximum accepted upload size (spec §10: 50 MB).</summary>
    public long MaxFileSizeBytes { get; set; } = 50L * 1024 * 1024;

    // ---- S3 (production) ----

    /// <summary>"Local" (dev disk) or "S3". Selects the IFileStorage implementation.</summary>
    public string Provider { get; set; } = "Local";

    /// <summary>AWS region for the S3 bucket (e.g. "us-east-1").</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Optional explicit access key; falls back to the default AWS credential chain when blank.</summary>
    public string? AccessKey { get; set; }

    public string? SecretKey { get; set; }

    /// <summary>Optional custom endpoint for S3-compatible stores (e.g. MinIO); enables path-style addressing.</summary>
    public string? ServiceUrl { get; set; }
}
