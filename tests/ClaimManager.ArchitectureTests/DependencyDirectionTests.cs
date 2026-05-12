namespace ClaimManager.ArchitectureTests;

public sealed class DependencyDirectionTests
{
    [Fact]
    public void Layer_dependencies_follow_the_approved_direction()
    {
        var repoRoot = FindRepositoryRoot();
        var apiProject = ReadProjectFile(repoRoot, "src", "ClaimManager.Api", "ClaimManager.Api.csproj");
        var applicationProject = ReadProjectFile(repoRoot, "src", "ClaimManager.Application", "ClaimManager.Application.csproj");
        var infrastructureProject = ReadProjectFile(repoRoot, "src", "ClaimManager.Infrastructure", "ClaimManager.Infrastructure.csproj");
        var domainProject = ReadProjectFile(repoRoot, "src", "ClaimManager.Domain", "ClaimManager.Domain.csproj");

        Assert.Contains("..\\ClaimManager.Application\\ClaimManager.Application.csproj", apiProject);
        Assert.Contains("..\\ClaimManager.Infrastructure\\ClaimManager.Infrastructure.csproj", apiProject);

        Assert.Contains("..\\ClaimManager.Domain\\ClaimManager.Domain.csproj", applicationProject);
        Assert.DoesNotContain("ClaimManager.Infrastructure", applicationProject);
        Assert.DoesNotContain("ClaimManager.Api", applicationProject);

        Assert.Contains("..\\ClaimManager.Application\\ClaimManager.Application.csproj", infrastructureProject);
        Assert.Contains("..\\ClaimManager.Domain\\ClaimManager.Domain.csproj", infrastructureProject);
        Assert.DoesNotContain("ClaimManager.Api", infrastructureProject);

        Assert.DoesNotContain("ProjectReference", domainProject);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ClaimManager.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the ClaimManager solution root.");
    }

    private static string ReadProjectFile(string repoRoot, params string[] segments) =>
        File.ReadAllText(Path.Combine([repoRoot, .. segments]));
}