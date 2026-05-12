using ClaimManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace ClaimManager.Infrastructure.IntegrationTests.Persistence;

public sealed class MigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:18")
        .WithImage("postgres:18")
        .WithDatabase("claimmanager")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    [Fact]
    public async Task Explicit_migrations_create_identity_and_claim_tables()
    {
        await using var dbContext = CreateDbContext();

        await dbContext.Database.MigrateAsync();

        Assert.True(await dbContext.Database.CanConnectAsync());

        await using var connection = new NpgsqlConnection(_database.GetConnectionString());
        await connection.OpenAsync();

        Assert.Equal(1L, await CountTablesAsync(connection, "users"));
        Assert.Equal(1L, await CountTablesAsync(connection, "roles"));
        Assert.Equal(1L, await CountTablesAsync(connection, "claims"));
    }

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    private ClaimManagerDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ClaimManagerDbContext>()
            .UseNpgsql(_database.GetConnectionString(), npgsqlOptions => npgsqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ClaimManagerDbContext(options);
    }

    private static async Task<long> CountTablesAsync(NpgsqlConnection connection, string tableName)
    {
        await using var command = new NpgsqlCommand(
            "select count(*) from information_schema.tables where table_schema = 'public' and table_name = @tableName;",
            connection);
        command.Parameters.AddWithValue("tableName", tableName);

        return (long)(await command.ExecuteScalarAsync())!;
    }
}