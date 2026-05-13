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

[Collection("Functional")]
public sealed class ClaimEndpointsTests(ClaimManagerApiFactory factory)
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
        var queue = await queueResponse.Content.ReadFromJsonAsync<ClaimSummaryPagedResponseDto>();

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.NotNull(createdClaim);
        Assert.NotEqual(Guid.Empty, createdClaim!.Id);
        Assert.Contains(queue?.Items ?? [], claim => claim.Id == createdClaim.Id && claim.ClaimantName == "Morgan Lee");
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
    public async Task New_claim_has_workflow_status_fields_initialized_on_get()
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

        var detailsResponse = await client.GetAsync($"/api/claims/{createdClaim!.Id}");
        var claimDetails = await detailsResponse.Content.ReadFromJsonAsync<ClaimDto>();

        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        Assert.NotNull(claimDetails);
        Assert.Equal(createdClaim!.CreatedByUserId, claimDetails!.OwnedByUserId);
        Assert.Equal("Initial review", claimDetails.NextExpectedAction);
        Assert.False(claimDetails.HasDataIntegrityWarning);
        Assert.Null(claimDetails.BlockerType);
        Assert.Null(claimDetails.BlockerReason);
        Assert.Null(claimDetails.DataIntegrityWarningMessage);
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

    [Fact]
    public async Task Adjuster_can_advance_claim_from_new_to_open_and_workflow_fields_are_updated()
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

        var addNoteResponse = await client.PostAsJsonAsync($"/api/claims/{createdClaim!.Id}/notes", new AddClaimNoteCommand("Claim reviewed before workflow advancement."));
        Assert.Equal(HttpStatusCode.Created, addNoteResponse.StatusCode);

        var advanceResponse = await client.PostAsJsonAsync($"/api/claims/{createdClaim.Id}/advance", new { });
        var advancedClaim = await advanceResponse.Content.ReadFromJsonAsync<ClaimDto>();

        Assert.Equal(HttpStatusCode.OK, advanceResponse.StatusCode);
        Assert.NotNull(advancedClaim);
        Assert.Equal("open", advancedClaim!.Status);
        Assert.NotNull(advancedClaim.OwnedByUserId);
        Assert.NotNull(advancedClaim.UpdatedAtUtc);
        Assert.NotNull(advancedClaim.UpdatedByUserId);
        Assert.Null(advancedClaim.BlockerType);
        Assert.Null(advancedClaim.BlockerReason);
        Assert.NotNull(advancedClaim.NextExpectedAction);
        Assert.Single(advancedClaim.Notes);
        Assert.Contains(advancedClaim.AuditHistory, entry => entry.Action == "workflow-advanced");
    }

    [Fact]
    public async Task Adjuster_can_route_claim_for_payment_approval_and_blocker_state_is_set()
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

        // Advance to open first
        await client.PostAsJsonAsync($"/api/claims/{createdClaim!.Id}/advance", new { });

        const string rationale = "Payment exceeds standard threshold, requires supervisor review.";
        var routeResponse = await client.PostAsJsonAsync($"/api/claims/{createdClaim.Id}/route-for-approval", new { rationale });
        var routedClaim = await routeResponse.Content.ReadFromJsonAsync<ClaimDto>();

        Assert.Equal(HttpStatusCode.OK, routeResponse.StatusCode);
        Assert.NotNull(routedClaim);
        Assert.Equal("pending", routedClaim!.Status);
        Assert.Equal("awaiting-payment-approval", routedClaim.BlockerType);
        Assert.Equal(rationale, routedClaim.BlockerReason);
        Assert.Equal("Awaiting payment approval decision", routedClaim.NextExpectedAction);
        Assert.NotNull(routedClaim.UpdatedAtUtc);
        Assert.NotNull(routedClaim.UpdatedByUserId);
        Assert.Contains(routedClaim.AuditHistory, entry => entry.Action == "routed-for-approval");
    }

    [Fact]
    public async Task Route_for_approval_trims_rationale_before_storing_and_auditing()
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

        await client.PostAsJsonAsync($"/api/claims/{createdClaim!.Id}/advance", new { });

        const string paddedRationale = "        exceeds threshold and needs review        ";
        var routeResponse = await client.PostAsJsonAsync($"/api/claims/{createdClaim.Id}/route-for-approval", new { rationale = paddedRationale });
        var routedClaim = await routeResponse.Content.ReadFromJsonAsync<ClaimDto>();

        Assert.Equal(HttpStatusCode.OK, routeResponse.StatusCode);
        Assert.Equal("exceeds threshold and needs review", routedClaim!.BlockerReason);
        Assert.Contains(routedClaim.AuditHistory, entry => entry.Summary.Contains("exceeds threshold and needs review", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Advance_from_invalid_state_returns_conflict()
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

        // Advance new → open → in-review
        await client.PostAsJsonAsync($"/api/claims/{createdClaim!.Id}/advance", new { });
        await client.PostAsJsonAsync($"/api/claims/{createdClaim.Id}/advance", new { });

        // Attempting to advance from in-review has no valid next state
        var conflictResponse = await client.PostAsJsonAsync($"/api/claims/{createdClaim.Id}/advance", new { });

        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
    }

    [Fact]
    public async Task Unfiltered_claims_list_returns_paginated_envelope_with_correct_structure()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "PaginationStructure User",
            "pagination.structure@example.com",
            "555-0201",
            "POL-PGSTRUCT-001",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Theft",
            "Vehicle stolen from parking lot."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        var listResponse = await client.GetAsync("/api/claims");
        var pagedResult = await listResponse.Content.ReadFromJsonAsync<ClaimSummaryPagedResponseDto>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(pagedResult);
        Assert.True(pagedResult!.TotalCount >= 1);
        Assert.True(pagedResult.Items.Count >= 1);
        Assert.Equal(1, pagedResult.Page);
        Assert.Equal(20, pagedResult.PageSize);
        Assert.Contains(pagedResult.Items, c => c.Id == createdClaim!.Id);
    }

    [Fact]
    public async Task Status_filter_returns_only_claims_with_matching_status()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "StatusFilter User",
            "statusfilter@example.com",
            "555-0202",
            "POL-STATUSFLT-001",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Flood",
            "Water damage from basement flooding."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        await client.PostAsJsonAsync($"/api/claims/{createdClaim!.Id}/advance", new { });

        var otherClaimResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "NewStatus User",
            "newstatus@example.com",
            "555-0207",
            "POL-STATUSFLT-002",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Flood",
            "Freshly created claim left in the new state."));
        var otherClaim = await otherClaimResponse.Content.ReadFromJsonAsync<ClaimDto>();

        var listResponse = await client.GetAsync("/api/claims?status=open");
        var pagedResult = await listResponse.Content.ReadFromJsonAsync<ClaimSummaryPagedResponseDto>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(pagedResult);
        Assert.Contains(pagedResult!.Items, c => c.Id == createdClaim!.Id);
        Assert.DoesNotContain(pagedResult.Items, c => c.Id == otherClaim!.Id);
        Assert.All(pagedResult.Items, c => Assert.Equal("open", c.Status));
    }

    [Fact]
    public async Task Search_by_claim_number_prefix_returns_matching_claim()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "SearchNumber User",
            "searchnumber@example.com",
            "555-0203",
            "POL-SEARCHNUM-001",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Fire",
            "Kitchen fire caused minor smoke damage."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        var claimNumberPrefix = createdClaim!.ClaimNumber[..Math.Min(7, createdClaim.ClaimNumber.Length)];
        var listResponse = await client.GetAsync($"/api/claims?search={claimNumberPrefix}");
        var pagedResult = await listResponse.Content.ReadFromJsonAsync<ClaimSummaryPagedResponseDto>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(pagedResult);
        Assert.Contains(pagedResult!.Items, c => c.Id == createdClaim.Id);
    }

    [Fact]
    public async Task HasBlocker_true_filter_returns_only_claims_with_active_blocker()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var createResponse = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "BlockerFilter User",
            "blockerfilter@example.com",
            "555-0204",
            "POL-BLKFLT-001",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Liability",
            "Slip-and-fall at commercial property."));
        var createdClaim = await createResponse.Content.ReadFromJsonAsync<ClaimDto>();

        await client.PostAsJsonAsync($"/api/claims/{createdClaim!.Id}/advance", new { });
        const string rationale = "Claim amount exceeds standard approval threshold.";
        await client.PostAsJsonAsync($"/api/claims/{createdClaim.Id}/route-for-approval", new { rationale });

        var listResponse = await client.GetAsync("/api/claims?hasBlocker=true");
        var pagedResult = await listResponse.Content.ReadFromJsonAsync<ClaimSummaryPagedResponseDto>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(pagedResult);
        Assert.Contains(pagedResult!.Items, c => c.Id == createdClaim.Id);
        Assert.All(pagedResult.Items, c => Assert.NotNull(c.BlockerType));
    }

    [Fact]
    public async Task Pagination_second_page_returns_the_second_ordered_claim()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        await LoginAsync(client);

        var create1 = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "PaginationSlice User",
            "pagslice1@example.com",
            "555-0205",
            "POL-PGSLICE-001",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Vandalism",
            "Windshield smashed in driveway."));
        var claim1 = await create1.Content.ReadFromJsonAsync<ClaimDto>();

        var create2 = await client.PostAsJsonAsync("/api/claims", new CreateClaimCommand(
            "PaginationSlice User",
            "pagslice2@example.com",
            "555-0206",
            "POL-PGSLICE-002",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Vandalism",
            "Side mirrors broken overnight."));
        var claim2 = await create2.Content.ReadFromJsonAsync<ClaimDto>();

        await client.PostAsJsonAsync($"/api/claims/{claim1!.Id}/advance", new { });

        var listResponse = await client.GetAsync("/api/claims?search=PaginationSlice+User&pageSize=1&page=2");
        var pagedResult = await listResponse.Content.ReadFromJsonAsync<ClaimSummaryPagedResponseDto>();

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        Assert.NotNull(pagedResult);
        Assert.Equal(2, pagedResult!.TotalCount);
        Assert.Single(pagedResult.Items);
        Assert.Equal(2, pagedResult.Page);
        Assert.Equal(1, pagedResult.PageSize);
        Assert.Equal(claim2!.Id, pagedResult.Items[0].Id);
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