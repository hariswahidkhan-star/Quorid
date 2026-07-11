using Amazon;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Quorid.Application.Common.Interfaces;
using Quorid.Application.Vault;
using Quorid.Infrastructure.Ai;
using Quorid.Infrastructure.Identity;
using Quorid.Infrastructure.Multitenancy;
using Quorid.Infrastructure.Persistence;
using Quorid.Infrastructure.Persistence.Interceptors;
using Quorid.Infrastructure.Storage;
using Quorid.Infrastructure.Vault;

namespace Quorid.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<AuditableEntitySaveChangesInterceptor>();

        // Discrete Database__* parts take precedence (e.g. a Render private-service DB
        // where the password is injected separately); otherwise use the full connection string.
        string connectionString;
        var dbHost = configuration["Database:Host"];
        if (!string.IsNullOrWhiteSpace(dbHost))
        {
            var dbPort = configuration["Database:Port"] ?? "3306";
            var dbName = configuration["Database:Name"] ?? "quorid";
            var dbUser = configuration["Database:User"] ?? "quorid";
            var dbPassword = configuration["Database:Password"] ?? string.Empty;
            connectionString = $"server={dbHost};port={dbPort};database={dbName};user={dbUser};password={dbPassword};";
        }
        else
        {
            connectionString = configuration.GetConnectionString("Default")
                ?? "server=localhost;port=3306;database=quorid;user=quorid;password=quorid_dev;";
        }

        // Fixed server version avoids opening a connection during startup.
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditableEntitySaveChangesInterceptor>();
            options
                .UseMySql(connectionString, serverVersion)
                .AddInterceptors(interceptor);
        });

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // --- Blob storage: S3 in production, local disk in dev (Storage:Provider) ---
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
        var storage = configuration.GetSection(StorageOptions.SectionName).Get<StorageOptions>() ?? new StorageOptions();
        if (string.Equals(storage.Provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IAmazonS3>(_ => BuildS3Client(storage));
            services.AddSingleton<IFileStorage, S3FileStorage>();
        }
        else
        {
            services.AddSingleton<IFileStorage, LocalFileStorage>();
        }

        // --- AI: offline heuristics by default; Claude adapters when an API key is set ---
        // Both are registered so the heuristics remain the guaranteed fallback; the
        // Anthropic registrations come last and win when resolved (DI last-wins).
        services.AddSingleton<IDocumentAiService, HeuristicDocumentAiService>();     // Module 1
        services.AddSingleton<IAnalyticsAssistant, HeuristicAnalyticsAssistant>();   // Module 11
        services.AddSingleton<IDocumentGenerator, HeuristicDocumentGenerator>();     // Module 4

        services.Configure<AnthropicOptions>(configuration.GetSection(AnthropicOptions.SectionName));
        var anthropic = configuration.GetSection(AnthropicOptions.SectionName).Get<AnthropicOptions>() ?? new AnthropicOptions();
        if (anthropic.Enabled)
        {
            services.AddHttpClient<AnthropicChatClient>(c =>
            {
                c.BaseAddress = new Uri(anthropic.BaseUrl);
                c.Timeout = TimeSpan.FromSeconds(120);
            });
            services.AddScoped<IDocumentAiService, AnthropicDocumentAiService>();
            services.AddScoped<IDocumentGenerator, AnthropicDocumentGenerator>();
            services.AddScoped<IAnalyticsAssistant, AnthropicAnalyticsAssistant>();
        }

        // Identity Vault (Module 2)
        services.AddScoped<IVaultService, VaultService>();

        return services;
    }

    private static IAmazonS3 BuildS3Client(StorageOptions storage)
    {
        var config = new AmazonS3Config();
        if (!string.IsNullOrWhiteSpace(storage.ServiceUrl))
        {
            // S3-compatible endpoint (e.g. MinIO): path-style, no region routing.
            config.ServiceURL = storage.ServiceUrl;
            config.ForcePathStyle = true;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(storage.Region);
        }

        return string.IsNullOrWhiteSpace(storage.AccessKey)
            ? new AmazonS3Client(config)                                      // default AWS credential chain
            : new AmazonS3Client(storage.AccessKey, storage.SecretKey, config);
    }
}
