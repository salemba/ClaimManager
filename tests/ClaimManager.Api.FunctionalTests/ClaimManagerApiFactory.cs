using ClaimManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace ClaimManager.Api.FunctionalTests;

public sealed class ClaimManagerApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("claimmanager")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();
    private readonly SemaphoreSlim _resetGate = new(1, 1);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:postgresdb"] = _database.GetConnectionString()
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__postgresdb", _database.GetConnectionString());

        await ResetDatabaseAsync();

        _ = base.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    public new HttpClient CreateClient(WebApplicationFactoryClientOptions options)
    {
        _resetGate.Wait();

        try
        {
            ResetDatabaseAsync().GetAwaiter().GetResult();
            return base.CreateClient(options);
        }
        finally
        {
            _resetGate.Release();
        }
    }

    private async Task ResetDatabaseAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__postgresdb", _database.GetConnectionString());

        var dbContextOptions = new DbContextOptionsBuilder<ClaimManagerDbContext>()
            .UseNpgsql(_database.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        await using var dbContext = new ClaimManagerDbContext(dbContextOptions);
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__postgresdb", null);
        await _database.DisposeAsync().AsTask();
    }
}