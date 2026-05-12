namespace ClaimManager.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class ClaimManagerDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ClaimManagerDbContext>
{
    public ClaimManagerDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__postgresdb")
            ?? Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRESDB")
            ?? throw new InvalidOperationException(
                "Set the ConnectionStrings__postgresdb environment variable before invoking EF Core design-time commands.");

        var optionsBuilder = new DbContextOptionsBuilder<ClaimManagerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ClaimManagerDbContext(optionsBuilder.Options);
    }
}