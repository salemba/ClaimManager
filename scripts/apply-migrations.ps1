param(
    [Parameter(Mandatory = $true)]
    [string]$ConnectionString
)

$env:ConnectionStrings__postgresdb = $ConnectionString

dotnet tool run dotnet-ef database update `
    --project src/ClaimManager.Infrastructure/ClaimManager.Infrastructure.csproj `
    --startup-project src/ClaimManager.Api/ClaimManager.Api.csproj