using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using Quorid.Application.Common.Interfaces;

namespace Quorid.Infrastructure.Persistence;

/// <summary>
/// Enables <c>dotnet ef migrations add …</c> to construct the context at design
/// time without booting the API or opening a database connection. The connection
/// string comes from <c>ConnectionStrings__Default</c> (env) or a local default;
/// the tenant context is a no-op because the global query filter is irrelevant to
/// schema generation.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? "server=localhost;port=3306;database=quorid;user=quorid;password=quorid_dev;";

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36)))
            .Options;

        return new ApplicationDbContext(options, new DesignTimeTenantContext());
    }

    private sealed class DesignTimeTenantContext : ITenantContext
    {
        public Guid? TenantId => null;
        public bool HasTenant => false;
        public void SetTenant(Guid tenantId) { }
    }
}
