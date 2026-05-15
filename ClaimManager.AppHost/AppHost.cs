var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithImageTag("18");
var postgresdb = postgres.AddDatabase("postgresdb");

var server = builder.AddProject<Projects.ClaimManager_Api>("api")
    .WithReference(postgresdb)
    .WaitFor(postgresdb)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../src/ClaimManager.Frontend")
    .WithReference(server)
    .WithEnvironment("SERVER_HTTPS", server.GetEndpoint("https"))
    .WithEnvironment("SERVER_HTTP", server.GetEndpoint("http"));

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
