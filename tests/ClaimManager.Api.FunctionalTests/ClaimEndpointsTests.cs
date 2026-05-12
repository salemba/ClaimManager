using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text;
using ClaimManager.Application.Claims.Commands;
using ClaimManager.Application.Claims.Dtos;
using ClaimManager.Api.Endpoints.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ClaimManager.Api.FunctionalTests;

public sealed class ClaimEndpointsTests(ClaimManagerApiFactory factory) : IClassFixture<ClaimManagerApiFactory>
{
    [Fact]
    public async Task Authenticated_adjuster_can_create_claim_and_see_it_in_queue()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "Morgan Lee",
            "morgan.lee@example.com",
            "555-0135",
            "POL-0200",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Collision",
            "Rear-end collision during evening commute."));

        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();
        var queueResponse = await client.GetAsync("/api/claims");
        var queue = await queueResponse.Content.ReadFromJsonAsync<ClaimSummaryDto[]>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createdClaim);
        Assert.NotEqual(Guid.Empty, createdClaim!.Id);
        Assert.Contains(queue ?? [], claim => claim.Id == createdClaim.Id && claim.ClaimantName == "Morgan Lee");
    }

    [Fact]
    public async Task Authenticated_adjuster_can_update_claim_and_receive_audit_history()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "Morgan Lee",
            "morgan.lee@example.com",
            "555-0135",
            "POL-0200",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Collision",
            "Rear-end collision during evening commute."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        var updateResponse = await client.PutAsJsonAsync($"/api/claims/{createdClaim!.Id}", new UpdateClaimCommand(
            createdClaim.Id,
            "Morgan Lee",
            "morgan.updated@example.com",
            "555-0140",
            "POL-0200",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Collision",
            "Rear-end collision with bumper and trunk damage."));

        var updatedClaim = await updateResponse.Content.ReadFromJsonAsync<ClaimDto>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updatedClaim);
        Assert.Equal("morgan.updated@example.com", updatedClaim!.ClaimantEmail);
        Assert.True(updatedClaim.AuditHistory.Count >= 2);
        Assert.Contains(updatedClaim.AuditHistory, entry => entry.Action == "updated");
    }

    [Fact]
    public async Task Authenticated_adjuster_can_get_claim_details_with_updated_audit_summary()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "Morgan Lee",
            "morgan.lee@example.com",
            "555-0135",
            "POL-0200",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Collision",
            "Rear-end collision during evening commute."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        var updateResponse = await client.PutAsJsonAsync($"/api/claims/{createdClaim!.Id}", new UpdateClaimCommand(
            createdClaim.Id,
            "Morgan Lee",
            "morgan.updated@example.com",
            "555-0140",
            "POL-0200",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Collision",
            "Rear-end collision with bumper and trunk damage."));

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var detailsResponse = await client.GetAsync($"/api/claims/{createdClaim.Id}");
        var claimDetails = await detailsResponse.Content.ReadFromJsonAsync<ClaimDto>();

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.NotNull(claimDetails);
        Assert.Equal("morgan.updated@example.com", claimDetails!.ClaimantEmail);
        Assert.Contains(claimDetails.AuditHistory, entry =>
            entry.Action == "updated"
            && entry.Summary.Contains("Claimant email updated", StringComparison.Ordinal)
            && entry.Summary.Contains("Loss description updated", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Invalid_claim_payload_returns_field_level_validation_errors()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var response = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            string.Empty,
            "not-an-email",
            string.Empty,
            string.Empty,
            DateTime.UtcNow.AddDays(1),
            string.Empty,
            string.Empty));

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("claimantName", problem!.Errors.Keys);
        Assert.Contains("claimantEmail", problem.Errors.Keys);
        Assert.Contains("lossDateUtc", problem.Errors.Keys);
    }

    [Fact]
    public async Task Authenticated_adjuster_can_add_note_and_see_it_in_claim_details()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "Morgan Lee",
            "morgan.lee@example.com",
            "555-0135",
            "POL-0200",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Collision",
            "Rear-end collision during evening commute."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        var noteResponse = await client.PostAsJsonAsync($"/api/claims/{createdClaim!.Id}/notes", new AddClaimNoteCommand("  Customer called with vendor ETA.  "));
        var createdNote = await noteResponse.Content.ReadFromJsonAsync<ClaimNoteDto>();

        var detailsResponse = await client.GetAsync($"/api/claims/{createdClaim.Id}");
        var claimDetails = await detailsResponse.Content.ReadFromJsonAsync<ClaimDto>();

        Assert.Equal(HttpStatusCode.Created, noteResponse.StatusCode);
        Assert.NotNull(createdNote);
        Assert.Equal("Customer called with vendor ETA.", createdNote!.Content);
        Assert.NotNull(claimDetails);
        Assert.Contains(claimDetails!.Notes, note => note.Id == createdNote.Id && note.Content == createdNote.Content);
        Assert.Contains(claimDetails.AuditHistory, entry => entry.Action == "note-added");
    }

    [Fact]
    public async Task Authenticated_adjuster_can_upload_document_and_see_metadata_in_claim_details()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "Morgan Lee",
            "morgan.lee@example.com",
            "555-0135",
            "POL-0200",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Collision",
            "Rear-end collision during evening commute."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("estimate"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "estimate.pdf");

        var uploadResponse = await client.PostAsync($"/api/claims/{createdClaim!.Id}/documents", form);
        var uploadedDocument = await uploadResponse.Content.ReadFromJsonAsync<ClaimDocumentDto>();

        var detailsResponse = await client.GetAsync($"/api/claims/{createdClaim.Id}");
        var claimDetails = await detailsResponse.Content.ReadFromJsonAsync<ClaimDto>();

        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        Assert.NotNull(uploadedDocument);
        Assert.Equal("estimate.pdf", uploadedDocument!.FileName);
        Assert.Equal(".pdf", uploadedDocument.FileType);
        Assert.NotNull(claimDetails);
        Assert.Contains(claimDetails!.Documents, document => document.Id == uploadedDocument.Id && document.FileName == "estimate.pdf");
        Assert.Contains(claimDetails.AuditHistory, entry => entry.Action == "document-uploaded");
    }

    [Fact]
    public async Task Document_upload_without_csrf_header_is_rejected()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "Morgan Lee",
            "morgan.lee@example.com",
            "555-0135",
            "POL-0200",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Collision",
            "Rear-end collision during evening commute."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("estimate"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "estimate.pdf");

        var uploadResponse = await client.PostAsync($"/api/claims/{createdClaim!.Id}/documents", form);

        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
    }

    [Fact]
    public async Task Invalid_document_upload_returns_field_level_validation_errors()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "Morgan Lee",
            "morgan.lee@example.com",
            "555-0135",
            "POL-0200",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Collision",
            "Rear-end collision during evening commute."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        using var form = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("malware"));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "file", "payload.exe");

        var uploadResponse = await client.PostAsync($"/api/claims/{createdClaim!.Id}/documents", form);
        var problem = await uploadResponse.Content.ReadFromJsonAsync<ValidationProblemDetails>();

        Assert.Equal(HttpStatusCode.BadRequest, uploadResponse.StatusCode);
        Assert.NotNull(problem);
        Assert.Contains("file", problem!.Errors.Keys);
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new AuthEndpoints.LoginRequest("adjuster@claimmanager.local", "Adjuster!2345"));
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var csrfToken = GetCookieValue(loginResponse, "claimmanager.csrf");
        Assert.False(string.IsNullOrWhiteSpace(csrfToken));

        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrfToken);
    }

    private static string? GetCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
        {
            return null;
        }

        var prefix = $"{cookieName}=";
        foreach (var cookieHeader in cookieHeaders)
        {
            if (!cookieHeader.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var endIndex = cookieHeader.IndexOf(';');
            var value = endIndex >= 0
                ? cookieHeader[prefix.Length..endIndex]
                : cookieHeader[prefix.Length..];

            return Uri.UnescapeDataString(value);
        }

        return null;
    }
}