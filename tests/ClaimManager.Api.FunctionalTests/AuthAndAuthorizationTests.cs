using System.Net;
using System.Net.Http.Json;
using ClaimManager.Api.Endpoints.Auth;
using ClaimManager.Api.Endpoints.Workspace;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClaimManager.Api.FunctionalTests;

[Collection("Functional")]
public sealed class AuthAndAuthorizationTests(ClaimManagerApiFactory factory)
{
    [Fact]
    public async Task Health_endpoint_returns_success()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_me_endpoint_returns_problem_details_401()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/api/auth/me");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(401, problem?.Status);
    }

    [Fact]
    public async Task Adjuster_is_forbidden_from_admin_endpoint()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("adjuster@claimmanager.local", "Adjuster!2345"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.GetAsync("/api/admin/audit");
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(403, problem?.Status);
    }

    [Fact]
    public async Task Authenticated_adjuster_can_access_workspace()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("adjuster@claimmanager.local", "Adjuster!2345"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var response = await client.GetAsync("/api/workspace");
        var payload = await response.Content.ReadFromJsonAsync<WorkspaceEndpoints.WorkspaceResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(payload);
        Assert.True(payload!.DatabaseAvailable);
        Assert.Equal("adjuster@claimmanager.local", payload.User.Email);
        Assert.Contains(payload.Claims, claim => claim.ClaimNumber == "CLM-0001");
    }
}