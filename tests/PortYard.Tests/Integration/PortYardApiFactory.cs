using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PortYard.Api.Data;
using Xunit;

namespace PortYard.Tests.Integration;

/// <summary>
/// Hosts the API against a SQLite connection kept open for the lifetime of the factory (one
/// instance per test class via <see cref="IClassFixture{TFixture}"/>), so relational
/// constraints — foreign keys, unique indexes — are actually enforced. EF Core's
/// UseInMemoryDatabase provider does not enforce those, which would defeat the point of these
/// tests. Each test class gets its own fresh, freshly seeded database.
///
/// Schema is applied via <c>Database.MigrateAsync()</c>, the same call Program.cs makes in
/// Development, not <c>EnsureCreatedAsync()</c>. EnsureCreated builds straight from the current
/// model and would pass even if a migration file had drifted from it — a green test run here is
/// what actually proves the committed migrations are correct, not just that the model is.
/// </summary>
public class PortYardApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<YardDbContext>>();

            services.AddDbContext<YardDbContext>(options => options.UseSqlite(_connection));
        });
    }

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<YardDbContext>();
        await db.Database.MigrateAsync();
        YardSeeder.Seed(db);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }
}
