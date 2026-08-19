using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortYard.Api.Data;
using Xunit;

namespace PortYard.Tests.Integration;

/// <summary>
/// Hosts the API against a real SQL Server database — the same engine the app runs against in
/// every real environment now, not a stand-in — so relational constraints, and the T-SQL the
/// committed migrations actually contain, are both genuinely exercised. Each test class
/// (one instance per class via <see cref="IClassFixture{TFixture}"/>) gets its own uniquely
/// named database, created fresh via <c>Database.MigrateAsync()</c> and dropped on disposal, so
/// test classes can never see each other's data.
///
/// Needs a reachable SQL Server: set <c>PORTYARD_TEST_CONNECTION_STRING</c> to point at one, or
/// run the default Microsoft image locally —
/// <c>docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest</c>
/// — which is exactly what CI's services container also runs. The password above is Microsoft's
/// own documented local/CI-only placeholder for this exact image, never a real credential.
/// </summary>
public class PortYardApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = $"PortYardTests_{Guid.NewGuid():N}";

    private string ConnectionString
    {
        get
        {
            var baseConnectionString = Environment.GetEnvironmentVariable("PORTYARD_TEST_CONNECTION_STRING")
                ?? "Server=localhost,1433;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True";

            var builder = new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = _databaseName };
            return builder.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<YardDbContext>>();

            services.AddDbContext<YardDbContext>(options => options.UseSqlServer(ConnectionString));
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YardDbContext>();

        // Schema is applied via the same Database.Migrate() call Program.cs makes in
        // Development, not EnsureCreated — a green test run proves the committed migration is
        // correct against real SQL Server, not just that the model can build some database.
        await db.Database.MigrateAsync();
        YardSeeder.Seed(db);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YardDbContext>();
        await db.Database.EnsureDeletedAsync();

        await base.DisposeAsync();
    }
}
