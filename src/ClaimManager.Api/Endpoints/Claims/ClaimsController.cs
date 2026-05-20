namespace ClaimManager.Api.Endpoints.Claims;

using ClaimManager.Application.Audit.Commands;
using ClaimManager.Application.ClaimantCommunication.Commands;
using ClaimManager.Application.ClaimantCommunication.Transformers;
using ClaimManager.Application.ClaimantCommunication.Validators;
using ClaimManager.Application.Claims.Commands;
using ClaimManager.Application.Claims.Dtos;
using ClaimManager.Application.Claims.Validators;
using ClaimManager.Application.Security;
using ClaimManager.Domain.ClaimantCommunication;
using ClaimManager.Infrastructure.Integrations.DocumentRepository;
using ClaimManager.Infrastructure.Integrations.Messaging;
using ClaimManager.Infrastructure.Integrations.PaymentSystem;
using ClaimManager.Infrastructure.Integrations.PolicySystem;
using ClaimManager.Infrastructure.Identity;
using ClaimManager.Infrastructure.Persistence;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;
using System.Text.Json;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;
using ClaimEntity = ClaimManager.Domain.Claims.Claim;

/// <summary>
/// Query parameters for retrieving claims with filtering and pagination.
/// </summary>
public sealed record GetClaimsQueryParams(
    string? Search = null,
    string? Status = null,
    string? BlockerType = null,
    bool? HasBlocker = null,
    string? OwnedByUserId = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// Provides endpoints for managing claims operations including creation, retrieval, updates, synchronization, and notifications.
/// All endpoints require Adjuster authorization or higher.
/// </summary>
/// <remarks>
/// This controller handles comprehensive claim management operations including:
/// - CRUD operations for claims
/// - Claim data synchronization with external systems (Policy, Payment, Documents)
/// - Claim state reconciliation
/// - Claim notes and document management
/// - Notification and communication handling
/// </remarks>
public static class ClaimsController
{
    /// <summary>
    /// Maps all claim endpoints to the route builder.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The configured endpoint route builder.</returns>
    public static IEndpointRouteBuilder MapClaimEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/claims")
            .WithTags("Claims")
            .RequireAuthorization(ClaimManagerPolicies.Adjuster);

        group.MapGet("/", GetClaimsAsync)
            .WithName("GetClaims")
            .WithOpenApi()
            .WithDescription("Retrieve a paginated list of claims with optional filtering");

        group.MapGet("/{id:guid}", GetClaimDetailsAsync)
            .WithName("GetClaimDetails")
            .WithOpenApi()
            .WithDescription("Retrieve detailed information about a specific claim");

        group.MapPost("/", CreateClaimAsync)
            .WithName("CreateClaim")
            .WithOpenApi()
            .WithDescription("Create a new claim");

        group.MapPut("/{id:guid}", UpdateClaimAsync)
            .WithName("UpdateClaim")
            .WithOpenApi()
            .WithDescription("Update an existing claim");

        group.MapPost("/{id:guid}/notes", AddClaimNoteAsync)
            .WithName("AddClaimNote")
            .WithOpenApi()
            .WithDescription("Add a note to a claim");

        group.MapPost("/{id:guid}/documents", UploadClaimDocumentAsync)
            .WithName("UploadClaimDocument")
            .WithOpenApi()
            .WithDescription("Upload a document to a claim");

        group.MapPost("/{id:guid}/advance", AdvanceClaimWorkflowAsync)
            .WithName("AdvanceClaimWorkflow")
            .WithOpenApi()
            .WithDescription("Advance the claim to the next workflow state");

        group.MapPost("/{id:guid}/route-for-approval", RouteClaimForApprovalAsync)
            .WithName("RouteClaimForApproval")
            .WithOpenApi()
            .WithDescription("Route a claim for payment approval");

        group.MapPost("/{id:guid}/sync-policy", SyncClaimPolicyDataAsync)
            .WithName("SyncClaimPolicyData")
            .WithOpenApi()
            .WithDescription("Synchronize policy data for a claim");

        group.MapPost("/{id:guid}/sync-payment", SyncClaimPaymentDataAsync)
            .WithName("SyncClaimPaymentData")
            .WithOpenApi()
            .WithDescription("Synchronize payment data for a claim");

        group.MapPost("/{id:guid}/sync-documents", SyncClaimDocumentDataAsync)
            .WithName("SyncClaimDocumentData")
            .WithOpenApi()
            .WithDescription("Synchronize document data for a claim");

        group.MapPost("/{id:guid}/reconcile", ReconcileClaimStateAsync)
            .WithName("ReconcileClaimState")
            .WithOpenApi()
            .WithDescription("Reconcile all claim integration dependencies");

        group.MapPost("/{id:guid}/notifications", SendClaimNotificationAsync)
            .WithName("SendClaimNotification")
            .WithOpenApi()
            .WithDescription("Send a notification for a claim");

        group.MapPost("/{id:guid}/notifications/{notificationId:guid}/retry", RetryClaimNotificationAsync)
            .WithName("RetryClaimNotification")
            .WithOpenApi()
            .WithDescription("Retry sending a failed notification");

        return endpoints;
    }

    /// <summary>
    /// Retrieves a paginated list of claims with optional filtering by search term, status, blocker type, and owner.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated response containing claim summaries.</returns>
    /// <response code="200">Claims retrieved successfully.</response>
    private static async Task<Ok<ClaimSummaryPagedResponseDto>> GetClaimsAsync(
        [AsParameters] GetClaimsQueryParams query,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        IQueryable<ClaimEntity> q = dbContext.Claims;

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            q = q.Where(c =>
                EF.Functions.ILike(c.ClaimNumber, pattern) ||
                EF.Functions.ILike(c.ClaimantName, pattern) ||
                EF.Functions.ILike(c.PolicyNumber, pattern));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            q = q.Where(c => c.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.BlockerType))
        {
            q = q.Where(c => c.BlockerType == query.BlockerType);
        }

        if (query.HasBlocker.HasValue)
        {
            q = query.HasBlocker.Value
                ? q.Where(c => c.BlockerType != null)
                : q.Where(c => c.BlockerType == null);
        }

        if (!string.IsNullOrWhiteSpace(query.OwnedByUserId))
        {
            q = q.Where(c => c.OwnedByUserId == query.OwnedByUserId);
        }

        var totalCount = await q.CountAsync(cancellationToken);

        var items = await q
            .OrderByDescending(c => c.UpdatedAtUtc ?? c.CreatedAtUtc)
            .ThenBy(c => c.ClaimNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => ClaimSummaryDto.FromClaim(c))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(new ClaimSummaryPagedResponseDto(items, page, pageSize, totalCount));
    }

    /// <summary>
    /// Retrieves detailed information about a specific claim including notes, documents, audit history, and communications.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Detailed claim information or not found result.</returns>
    /// <response code="200">Claim details retrieved successfully.</response>
    /// <response code="404">Claim not found.</response>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> GetClaimDetailsAsync(
        Guid id,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        CancellationToken cancellationToken)
    {
        var claim = await dbContext.Claims
            .AsSplitQuery()
            .Include(existingClaim => existingClaim.Notes)
            .Include(existingClaim => existingClaim.Documents)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        var auditHistory = await GetAuditHistoryAsync(dbContext, claim.Id, cancellationToken);
        var communications = await GetCommunicationsAsync(dbContext, claim.Id, cancellationToken);
        return TypedResults.Ok(ClaimDto.FromClaim(
            claim,
            auditHistory,
            claim.Notes
                .OrderByDescending(note => note.CreatedAtUtc)
                .Select(ClaimNoteDto.FromNote)
                .ToArray(),
            claim.Documents
                .OrderByDescending(document => document.UploadedAtUtc)
                .Select(ClaimDocumentDto.FromDocument)
                .ToArray(),
            communications));
    }

    /// <summary>
    /// Creates a new claim with initial policy and payment data synchronization.
    /// </summary>
    /// <param name="command">The create claim command containing claim details.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="policyClient">The policy system client.</param>
    /// <param name="paymentClient">The payment system client.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created claim details or validation/conflict result.</returns>
    /// <response code="201">Claim created successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="409">Claim number conflict.</response>
    private static async Task<Results<Created<ClaimDto>, ValidationProblem, ProblemHttpResult>> CreateClaimAsync(
        CreateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        var validator = new CreateClaimCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(
                ToLocalizedValidationDictionary(validationResult, localizer));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        PolicySummary? initialPolicyData = null;
        string? initialSyncFailReason = null;

        try
        {
            initialPolicyData = await policyClient.GetPolicyByNumberAsync(command.PolicyNumber, cancellationToken);
            if (initialPolicyData is null)
            {
                initialSyncFailReason = localizer["Sync_Error_PolicyNotFound"];
            }
        }
        catch (Exception ex)
        {
            initialSyncFailReason = BuildSyncFailureReason(localizer, "Sync_Error_PolicySystemUnreachable", ex.Message);
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            var createdAtUtc = DateTime.UtcNow;
            var claim = ClaimEntity.Create(
                await GenerateClaimNumberAsync(dbContext, cancellationToken),
                command.ClaimantName,
                command.ClaimantEmail,
                command.ClaimantPhone,
                command.PolicyNumber,
                command.LossDateUtc,
                command.LossType,
                command.LossDescription,
                user.Id.ToString(),
                createdAtUtc);

            var paymentSyncedAtUtc = DateTime.UtcNow;
            string paymentAuditAction;
            string paymentAuditSummary;
            PaymentRecord? initialPaymentData = null;
            string? initialPaymentSyncFailReason = null;

            try
            {
                initialPaymentData = await paymentClient.GetPaymentStatusByClaimAsync(claim.ClaimNumber, cancellationToken);
            }
            catch (Exception ex)
            {
                initialPaymentSyncFailReason = BuildSyncFailureReason(localizer, "Sync_Error_PaymentSystemUnreachable", ex.Message);
            }

            if (initialPaymentSyncFailReason is not null)
            {
                claim.MarkPaymentSyncFailed(initialPaymentSyncFailReason);
                paymentAuditSummary = string.Format(localizer["Audit_Payment_SyncFailed"], initialPaymentSyncFailReason);
                paymentAuditAction = "payment-sync-failed";
            }
            else
            {
                paymentAuditSummary = initialPaymentData is not null
                    ? claim.ApplyPaymentData(
                        initialPaymentData.PaymentReference,
                        initialPaymentData.Status,
                        initialPaymentData.Amount,
                        initialPaymentData.Currency,
                        initialPaymentData.SettledAt,
                        paymentSyncedAtUtc)
                    : claim.ApplyPaymentData(null, null, null, null, null, paymentSyncedAtUtc);
                paymentAuditAction = "payment-synced";
            }

            var policySyncedAtUtc = DateTime.UtcNow;
            string policyAuditAction;
            string policyAuditSummary;

            if (initialPolicyData is not null)
            {
                policyAuditSummary = claim.ApplyPolicyData(
                    initialPolicyData.PolicyHolder,
                    initialPolicyData.CoverageType,
                    initialPolicyData.EffectiveDate,
                    initialPolicyData.ExpirationDate,
                    policySyncedAtUtc);
                policyAuditAction = "policy-synced";
            }
            else
            {
                claim.MarkPolicySyncFailed(initialSyncFailReason!);
                policyAuditSummary = string.Format(localizer["Audit_Policy_SyncFailed"], initialSyncFailReason);
                policyAuditAction = "policy-sync-failed";
            }

            dbContext.Claims.Add(claim);
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                "created",
                localizer["Audit_Claim_Created"],
                user.Id.ToString(),
                createdAtUtc).ToEntity());
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                policyAuditAction,
                policyAuditSummary,
                user.Id.ToString(),
                policySyncedAtUtc).ToEntity());
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                paymentAuditAction,
                paymentAuditSummary,
                user.Id.ToString(),
                paymentSyncedAtUtc).ToEntity());

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return TypedResults.Created($"/api/claims/{claim.Id}", ClaimDto.FromClaim(claim, []));
            }
            catch (DbUpdateException ex) when (IsClaimNumberConflict(ex) && attempt < 2)
            {
                dbContext.ChangeTracker.Clear();
            }
        }

        return TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: localizer["Claim_Create_Conflict_Title"],
            detail: localizer["Claim_Create_Conflict_Detail"]);
    }

    /// <summary>
    /// Synchronizes policy data for a claim from the policy system.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="policyClient">The policy system client.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated claim details or not found/error result.</returns>
    /// <response code="200">Policy data synchronized successfully.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPolicyDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPolicySystemClient policyClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        await ExecutePolicySyncAsync(claim, user.Id.ToString(), policyClient, dbContext, DateTime.UtcNow, localizer, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Claim_Update_Conflict_Title"],
                detail: localizer["Claim_Update_Conflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Synchronizes payment data for a claim from the payment system.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="paymentClient">The payment system client.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated claim details or not found/error result.</returns>
    /// <response code="200">Payment data synchronized successfully.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPaymentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPaymentSystemClient paymentClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        await ExecutePaymentSyncAsync(claim, user.Id.ToString(), paymentClient, dbContext, DateTime.UtcNow, localizer, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Claim_Update_Conflict_Title"],
                detail: localizer["Claim_Update_Conflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Synchronizes document data for a claim from the document repository.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="documentRepository">The document repository.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated claim details or not found/error result.</returns>
    /// <response code="200">Document data synchronized successfully.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimDocumentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claim = await dbContext.Claims
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        await ExecuteDocumentSyncAsync(claim, user.Id.ToString(), documentRepository, dbContext, DateTime.UtcNow, localizer, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Claim_Update_Conflict_Title"],
                detail: localizer["Claim_Update_Conflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Reconciles all claim integration dependencies by attempting to sync policy, payment, and document data.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="policyClient">The policy system client.</param>
    /// <param name="paymentClient">The payment system client.</param>
    /// <param name="documentRepository">The document repository.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated claim details with reconciliation summary or error result.</returns>
    /// <response code="200">Claim reconciled successfully.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    /// <response code="409">Concurrency conflict.</response>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> ReconcileClaimStateAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        var attemptedAtUtc = DateTime.UtcNow;
        var unresolvedBefore = claim.GetActiveDataIntegrityIssues()
            .Select(issue => issue.Dependency)
            .ToHashSet(StringComparer.Ordinal);

        var syncResults = new[]
        {
            await ExecutePolicySyncAsync(claim, user.Id.ToString(), policyClient, dbContext, attemptedAtUtc, localizer, cancellationToken),
            await ExecutePaymentSyncAsync(claim, user.Id.ToString(), paymentClient, dbContext, attemptedAtUtc, localizer, cancellationToken),
            await ExecuteDocumentSyncAsync(claim, user.Id.ToString(), documentRepository, dbContext, attemptedAtUtc, localizer, cancellationToken),
        };

        var unresolvedAfter = claim.GetActiveDataIntegrityIssues()
            .Select(issue => issue.Dependency)
            .ToHashSet(StringComparer.Ordinal);
        var recoveredDependencies = unresolvedBefore
            .Where(dependency => !unresolvedAfter.Contains(dependency))
            .OrderBy(static dependency => dependency, StringComparer.Ordinal)
            .ToArray();
        var reconciliationSummary = BuildReconciliationSummary(syncResults, recoveredDependencies, unresolvedAfter, localizer);

        claim.RecordReconciliationOutcome(
            attemptedAtUtc,
            syncResults.Select(result => result.Dependency).ToArray(),
            recoveredDependencies,
            reconciliationSummary);

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            "claim-reconciled",
            reconciliationSummary,
            user.Id.ToString(),
            attemptedAtUtc).ToEntity());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Claim_Update_Conflict_Title"],
                detail: localizer["Claim_Update_Conflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Updates the core details of an existing claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The update claim command.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated claim details or validation/error result.</returns>
    /// <response code="200">Claim updated successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    /// <response code="409">Concurrency conflict.</response>
    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> UpdateClaimAsync(
        Guid id,
        UpdateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };

        var validator = new UpdateClaimCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(
                ToLocalizedValidationDictionary(validationResult, localizer));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        dbContext.Entry(claim).Property(c => c.RowVersion).OriginalValue = command.RowVersion;

        var auditSummary = BuildUpdateSummary(claim, command, localizer);
        var changed = claim.UpdateCoreDetails(
            command.ClaimantName,
            command.ClaimantEmail,
            command.ClaimantPhone,
            command.PolicyNumber,
            command.LossDateUtc,
            command.LossType,
            command.LossDescription,
            user.Id.ToString(),
            DateTime.UtcNow);

        if (changed)
        {
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                "updated",
                auditSummary,
                user.Id.ToString(),
                claim.UpdatedAtUtc ?? DateTime.UtcNow).ToEntity());

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: localizer["Claim_Update_Conflict_Title"],
                    detail: localizer["Claim_Update_Conflict_Detail"]);
            }
        }

        var auditHistory = await GetAuditHistoryAsync(dbContext, claim.Id, cancellationToken);
        return TypedResults.Ok(ClaimDto.FromClaim(claim, auditHistory));
    }

    /// <summary>
    /// Adds a note to a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The add claim note command.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created note details or validation/error result.</returns>
    /// <response code="201">Note added successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    private static async Task<Results<Created<ClaimNoteDto>, ValidationProblem, ProblemHttpResult>> AddClaimNoteAsync(
        Guid id,
        AddClaimNoteCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        var validator = new AddClaimNoteCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(
                ToLocalizedValidationDictionary(validationResult, localizer));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        var createdAtUtc = DateTime.UtcNow;
        var note = claim.AddNote(command.Content, user.Id.ToString(), createdAtUtc);

        dbContext.ClaimNotes.Add(note);
        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            "note-added",
            BuildNoteAuditSummary(note.Content, localizer),
            user.Id.ToString(),
            createdAtUtc).ToEntity());

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/claims/{claim.Id}/notes/{note.Id}", ClaimNoteDto.FromNote(note));
    }

    /// <summary>
    /// Uploads a document to a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="file">The file to upload.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="documentRepository">The document repository.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created document details or validation/error result.</returns>
    /// <response code="201">Document uploaded successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    private static async Task<Results<Created<ClaimDocumentDto>, ValidationProblem, ProblemHttpResult>> UploadClaimDocumentAsync(
        Guid id,
        IFormFile? file,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        byte[] content = [];
        if (file is not null)
        {
            await using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream, cancellationToken);
            content = memoryStream.ToArray();
        }

        var command = new UploadClaimDocumentCommand(
            file?.FileName ?? string.Empty,
            file?.ContentType,
            file?.Length ?? 0,
            content);

        var validator = new UploadClaimDocumentCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(
                ToLocalizedValidationDictionary(validationResult, localizer));
        }

        StoredClaimDocument? storedDocument = null;

        try
        {
            storedDocument = await documentRepository.SaveAsync(
                new DocumentRepositorySaveRequest(command.FileName, command.Content, command.ContentType),
                cancellationToken);

            var uploadedAtUtc = DateTime.UtcNow;
            var document = claim.AddDocument(
                storedDocument.FileName,
                storedDocument.FileType,
                storedDocument.StorageIdentifier,
                user.Id.ToString(),
                uploadedAtUtc,
                storedDocument.ContentType,
                storedDocument.FileSizeBytes);

            dbContext.ClaimDocuments.Add(document);
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                "document-uploaded",
                BuildDocumentAuditSummary(document.FileName, document.FileType, localizer),
                user.Id.ToString(),
                uploadedAtUtc).ToEntity());

            await dbContext.SaveChangesAsync(cancellationToken);

            return TypedResults.Created($"/api/claims/{claim.Id}/documents/{document.Id}", ClaimDocumentDto.FromDocument(document));
        }
        catch
        {
            if (storedDocument is not null)
            {
                try
                {
                    await documentRepository.DeleteAsync(storedDocument.StorageIdentifier, cancellationToken);
                }
                catch
                {
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Advances a claim to the next workflow state.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The advance claim workflow command.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated claim details or error result.</returns>
    /// <response code="200">Workflow advanced successfully.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    /// <response code="409">Invalid workflow transition or concurrency conflict.</response>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> AdvanceClaimWorkflowAsync(
        Guid id,
        AdvanceClaimWorkflowCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        dbContext.Entry(claim).Property(c => c.RowVersion).OriginalValue = command.RowVersion;
        var advancedAtUtc = DateTime.UtcNow;

        string auditSummary;
        try
        {
            auditSummary = claim.AdvanceWorkflow(user.Id.ToString(), advancedAtUtc);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Workflow_InvalidTransition_Title"],
                detail: ex.Message);
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            "workflow-advanced",
            auditSummary,
            user.Id.ToString(),
            advancedAtUtc).ToEntity());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Claim_Update_Conflict_Title"],
                detail: localizer["Claim_Update_Conflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Routes a claim for payment approval.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The route claim for approval command.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated claim details or validation/error result.</returns>
    /// <response code="200">Claim routed for approval successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    /// <response code="409">Invalid workflow transition or concurrency conflict.</response>
    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> RouteClaimForApprovalAsync(
        Guid id,
        RouteClaimForApprovalCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };

        var validator = new RouteClaimForApprovalCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(
                ToLocalizedValidationDictionary(validationResult, localizer));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        dbContext.Entry(claim).Property(c => c.RowVersion).OriginalValue = command.RowVersion;
        var routedAtUtc = DateTime.UtcNow;

        try
        {
            claim.RouteForPaymentApproval(command.Rationale, user.Id.ToString(), routedAtUtc);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Workflow_InvalidTransition_Title"],
                detail: ex.Message);
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            "routed-for-approval",
            string.Format(localizer["Audit_RoutedForApproval"], claim.BlockerReason),
            user.Id.ToString(),
            routedAtUtc).ToEntity());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Claim_Update_Conflict_Title"],
                detail: localizer["Claim_Update_Conflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Sends a notification for a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The send claim notification command.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="messagingClient">The messaging client.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Created communication details or validation/error result.</returns>
    /// <response code="201">Notification sent successfully.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Claim not found.</response>
    private static async Task<Results<Created<ClaimCommunicationDto>, ValidationProblem, ProblemHttpResult>> SendClaimNotificationAsync(
        Guid id,
        SendClaimNotificationCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        var validator = new SendClaimNotificationCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(
                ToLocalizedValidationDictionary(validationResult, localizer));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var claimExists = await dbContext.Claims.AnyAsync(c => c.Id == id, cancellationToken);
        if (!claimExists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Claim_NotFound_Title"],
                detail: localizer["Claim_NotFound_Detail"]);
        }

        var (subject, body) = command.CommunicationType == "claimant-safe"
            ? ClaimantSafeTransformer.Transform(command.Subject, command.Body)
            : (command.Subject.Trim(), command.Body.Trim());

        var createdAtUtc = DateTime.UtcNow;
        var communication = ClaimCommunication.Create(
            id,
            command.CommunicationType,
            command.Channel,
            command.Recipient,
            subject,
            body,
            correlationId: null,
            user.Id.ToString(),
            createdAtUtc);

        dbContext.ClaimCommunications.Add(communication);

        var attemptAtUtc = DateTime.UtcNow;
        var outbound = new OutboundMessage(
            communication.Recipient,
            communication.Subject,
            communication.Body,
            communication.Id.ToString());

        MessageDeliveryResult deliveryResult;
        try
        {
            deliveryResult = await messagingClient.SendAsync(outbound, cancellationToken);
        }
        catch (Exception ex)
        {
            deliveryResult = new MessageDeliveryResult(false, null, $"Messaging client threw an unexpected error: {ex.Message}");
        }

        if (deliveryResult.Success && deliveryResult.DeliveryId is not null)
        {
            communication.RecordSent(deliveryResult.DeliveryId, attemptAtUtc);
        }
        else
        {
            communication.RecordFailed(deliveryResult.FailureReason ?? "Delivery failed without a reason.", attemptAtUtc);
        }

        var auditSummary = communication.Status == "sent"
            ? string.Format(localizer["Audit_NotificationSent"], communication.CommunicationType, communication.Channel, communication.Recipient, communication.DeliveryId)
            : string.Format(localizer["Audit_NotificationFailed"], communication.CommunicationType, communication.Channel, communication.Recipient, communication.FailureReason);

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            id,
            communication.Status == "sent" ? "notification-sent" : "notification-failed",
            auditSummary,
            user.Id.ToString(),
            attemptAtUtc).ToEntity());

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/claims/{id}/notifications/{communication.Id}",
            ClaimCommunicationDto.FromCommunication(communication));
    }

    /// <summary>
    /// Retries sending a failed notification.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="notificationId">The unique identifier of the notification.</param>
    /// <param name="principal">The current user principal.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="messagingClient">The messaging client.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for claims controller.</param>
    /// <param name="sharedLocalizer">The string localizer for shared messages.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Updated communication details or error result.</returns>
    /// <response code="200">Notification retry completed.</response>
    /// <response code="401">User not authenticated.</response>
    /// <response code="404">Notification not found.</response>
    /// <response code="409">Retry not allowed for notification in current state.</response>
    private static async Task<Results<Ok<ClaimCommunicationDto>, ProblemHttpResult>> RetryClaimNotificationAsync(
        Guid id,
        Guid notificationId,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<SharedMessages> localizer,
        IStringLocalizer<SharedMessages> sharedLocalizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: sharedLocalizer["Error_Unauthorized_Title"],
                detail: sharedLocalizer["Error_Unauthorized_Detail"]);
        }

        var communication = await dbContext.ClaimCommunications
            .SingleOrDefaultAsync(c => c.Id == notificationId && c.ClaimId == id, cancellationToken);

        if (communication is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Notification_NotFound_Title"],
                detail: localizer["Notification_NotFound_Detail"]);
        }

        if (!communication.IsRetryEligible())
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Notification_RetryNotAllowed_Title"],
                detail: string.Format(localizer["Notification_RetryNotAllowed_Detail"], communication.Status));
        }

        communication.PrepareRetry();

        var attemptAtUtc = DateTime.UtcNow;
        var outbound = new OutboundMessage(
            communication.Recipient,
            communication.Subject,
            communication.Body,
            communication.Id.ToString());

        MessageDeliveryResult deliveryResult;
        try
        {
            deliveryResult = await messagingClient.SendAsync(outbound, cancellationToken);
        }
        catch (Exception ex)
        {
            deliveryResult = new MessageDeliveryResult(false, null, $"Messaging client threw an unexpected error: {ex.Message}");
        }

        if (deliveryResult.Success && deliveryResult.DeliveryId is not null)
        {
            communication.RecordSent(deliveryResult.DeliveryId, attemptAtUtc);
        }
        else
        {
            communication.RecordFailed(deliveryResult.FailureReason ?? "Delivery failed without a reason.", attemptAtUtc);
        }

        var successStatus = communication.Status == "sent" ? "succeeded" : "failed";
        var auditSummary = string.Format(
            localizer["Audit_NotificationRetried"],
            communication.CommunicationType,
            successStatus,
            communication.Channel,
            communication.Recipient,
            communication.DeliveryId ?? "N/A");

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            id,
            communication.Status == "sent" ? "notification-sent" : "notification-failed",
            auditSummary,
            user.Id.ToString(),
            attemptAtUtc).ToEntity());

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ClaimCommunicationDto.FromCommunication(communication));
    }

    /// <summary>
    /// Builds a complete claim DTO with all related data.
    /// </summary>
    private static async Task<ClaimDto> BuildClaimDtoAsync(
        ClaimManagerDbContext dbContext,
        ClaimEntity claim,
        CancellationToken cancellationToken)
    {
        var auditHistory = await GetAuditHistoryAsync(dbContext, claim.Id, cancellationToken);
        var notes = await dbContext.ClaimNotes
            .Where(note => note.ClaimId == claim.Id)
            .OrderByDescending(note => note.CreatedAtUtc)
            .Select(note => ClaimNoteDto.FromNote(note))
            .ToArrayAsync(cancellationToken);
        var documents = await dbContext.ClaimDocuments
            .Where(document => document.ClaimId == claim.Id)
            .OrderByDescending(document => document.UploadedAtUtc)
            .Select(document => ClaimDocumentDto.FromDocument(document))
            .ToArrayAsync(cancellationToken);
        var communications = await GetCommunicationsAsync(dbContext, claim.Id, cancellationToken);

        return ClaimDto.FromClaim(claim, auditHistory, notes, documents, communications);
    }

    /// <summary>
    /// Retrieves claim communications ordered by creation date.
    /// </summary>
    private static async Task<IReadOnlyList<ClaimCommunicationDto>> GetCommunicationsAsync(
        ClaimManagerDbContext dbContext,
        Guid claimId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ClaimCommunications
            .Where(c => c.ClaimId == claimId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Select(c => ClaimCommunicationDto.FromCommunication(c))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Retrieves claim audit history ordered by timestamp.
    /// </summary>
    private static async Task<IReadOnlyList<ClaimAuditDto>> GetAuditHistoryAsync(
        ClaimManagerDbContext dbContext,
        Guid claimId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ClaimAudits
            .Where(audit => audit.ClaimId == claimId)
            .OrderByDescending(audit => audit.PerformedAtUtc)
            .Select(audit => ClaimAuditDto.FromAudit(audit))
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Generates a unique claim number using sequential numbering.
    /// </summary>
    private static async Task<string> GenerateClaimNumberAsync(ClaimManagerDbContext dbContext, CancellationToken cancellationToken)
    {
        var latestClaimNumber = await dbContext.Claims
            .OrderByDescending(claim => claim.ClaimNumber)
            .Select(claim => claim.ClaimNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestClaimNumber is null)
        {
            return "CLM-0001";
        }

        var suffix = latestClaimNumber.Split('-').LastOrDefault();
        return int.TryParse(suffix, out var sequence)
            ? $"CLM-{sequence + 1:0000}"
            : $"CLM-{DateTime.UtcNow:yyyyMMddHHmmss}";
    }

    /// <summary>
    /// Determines if a database update exception is due to claim number uniqueness conflict.
    /// </summary>
    private static bool IsClaimNumberConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgresException.ConstraintName, "ix_claims_claim_number", StringComparison.Ordinal);
    }

    /// <summary>
    /// Converts validation errors to a localized dictionary for problem responses.
    /// </summary>
    private static Dictionary<string, string[]> ToLocalizedValidationDictionary(
        ValidationResult validationResult,
        IStringLocalizer<SharedMessages> localizer)
    {
        return validationResult.Errors
            .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => LocalizeValidationError(error.PropertyName, error.ErrorMessage, localizer)).ToArray());
    }

    /// <summary>
    /// Localizes a single validation error message.
    /// </summary>
    private static string LocalizeValidationError(string propertyName, string errorMessage, IStringLocalizer<SharedMessages> localizer)
    {
        var localizedKey = $"Claim_Validation_{propertyName}";
        var localizedValue = localizer[localizedKey];
        return !localizedValue.ResourceNotFound ? localizedValue.Value : errorMessage;
    }

    /// <summary>
    /// Builds a localized update summary describing changes to a claim.
    /// </summary>
    private static string BuildUpdateSummary(
        ClaimEntity claim,
        UpdateClaimCommand command,
        IStringLocalizer<SharedMessages> localizer)
    {
        var changes = new List<string>();

        AddChange(changes, "Claimant name", claim.ClaimantName, command.ClaimantName, localizer);
        AddChange(changes, "Claimant email", claim.ClaimantEmail, command.ClaimantEmail, localizer);
        AddChange(changes, "Claimant phone", claim.ClaimantPhone, command.ClaimantPhone, localizer);
        AddChange(changes, "Policy number", claim.PolicyNumber, command.PolicyNumber, localizer);
        AddChange(changes, "Loss type", claim.LossType, command.LossType, localizer);
        AddChange(changes, "Loss description", claim.LossDescription, command.LossDescription, localizer);

        if (claim.LossDateUtc != command.LossDateUtc)
        {
            changes.Add($"Loss date updated from {claim.LossDateUtc:yyyy-MM-dd} to {command.LossDateUtc:yyyy-MM-dd}.");
        }

        return changes.Count == 0
            ? localizer["Claim_UpdateSummary_NoChanges"]
            : string.Join(' ', changes);
    }

    /// <summary>
    /// Adds a change description to the update summary if the values differ.
    /// </summary>
    private static void AddChange(
        List<string> changes,
        string label,
        string currentValue,
        string newValue,
        IStringLocalizer<SharedMessages> localizer)
    {
        var normalizedNewValue = newValue.Trim();
        if (!string.Equals(currentValue, normalizedNewValue, StringComparison.Ordinal))
        {
            changes.Add(string.Format(localizer["Claim_UpdateSummary_Changed"], label, currentValue, normalizedNewValue));
        }
    }

    /// <summary>
    /// Executes policy data synchronization.
    /// </summary>
    private static async Task<ClaimSyncResult> ExecutePolicySyncAsync(
        ClaimEntity claim,
        string userId,
        IPolicySystemClient policyClient,
        ClaimManagerDbContext dbContext,
        DateTime syncedAtUtc,
        IStringLocalizer<SharedMessages> localizer,
        CancellationToken cancellationToken)
    {
        PolicySummary? policyData = null;
        string? syncFailReason = null;

        try
        {
            policyData = await policyClient.GetPolicyByNumberAsync(claim.PolicyNumber, cancellationToken);
            if (policyData is null)
            {
                syncFailReason = localizer["Sync_Error_PolicyNotFound"];
            }
        }
        catch (Exception ex)
        {
            syncFailReason = BuildSyncFailureReason(localizer, "Sync_Error_PolicySystemUnreachable", ex.Message);
        }

        string auditAction;
        string auditSummary;
        var succeeded = false;

        if (policyData is not null)
        {
            auditSummary = claim.ApplyPolicyData(
                policyData.PolicyHolder,
                policyData.CoverageType,
                policyData.EffectiveDate,
                policyData.ExpirationDate,
                syncedAtUtc);
            auditAction = "policy-synced";
            succeeded = true;
        }
        else
        {
            claim.MarkPolicySyncFailed(syncFailReason!);
            auditSummary = string.Format(localizer["Audit_Policy_SyncFailed"], syncFailReason);
            auditAction = "policy-sync-failed";
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(claim.Id, auditAction, auditSummary, userId, syncedAtUtc).ToEntity());
        return new ClaimSyncResult("policy", succeeded);
    }

    /// <summary>
    /// Executes payment data synchronization.
    /// </summary>
    private static async Task<ClaimSyncResult> ExecutePaymentSyncAsync(
        ClaimEntity claim,
        string userId,
        IPaymentSystemClient paymentClient,
        ClaimManagerDbContext dbContext,
        DateTime syncedAtUtc,
        IStringLocalizer<SharedMessages> localizer,
        CancellationToken cancellationToken)
    {
        PaymentRecord? paymentRecord = null;
        string? syncFailReason = null;

        try
        {
            paymentRecord = await paymentClient.GetPaymentStatusByClaimAsync(claim.ClaimNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            syncFailReason = BuildSyncFailureReason(localizer, "Sync_Error_PaymentSystemUnreachable", ex.Message);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkPaymentSyncFailed(syncFailReason);
            auditSummary = string.Format(localizer["Audit_Payment_SyncFailed"], syncFailReason);
            auditAction = "payment-sync-failed";
        }
        else if (paymentRecord is not null)
        {
            auditSummary = claim.ApplyPaymentData(
                paymentRecord.PaymentReference,
                paymentRecord.Status,
                paymentRecord.Amount,
                paymentRecord.Currency,
                paymentRecord.SettledAt,
                syncedAtUtc);
            auditAction = "payment-synced";
        }
        else
        {
            auditSummary = claim.ApplyPaymentData(null, null, null, null, null, syncedAtUtc);
            auditAction = "payment-synced";
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            auditAction,
            auditSummary,
            userId,
            syncedAtUtc).ToEntity());

        return new ClaimSyncResult("payment", syncFailReason is null);
    }

    /// <summary>
    /// Executes document data synchronization.
    /// </summary>
    private static async Task<ClaimSyncResult> ExecuteDocumentSyncAsync(
        ClaimEntity claim,
        string userId,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        DateTime syncedAtUtc,
        IStringLocalizer<SharedMessages> localizer,
        CancellationToken cancellationToken)
    {
        var documentsEntry = dbContext.Entry(claim).Collection(current => current.Documents);
        if (!documentsEntry.IsLoaded)
        {
            await documentsEntry.LoadAsync(cancellationToken);
        }

        IReadOnlyList<StoredClaimDocument>? repoDocuments = null;
        string? syncFailReason = null;

        try
        {
            repoDocuments = await documentRepository.GetDocumentListAsync(claim.ClaimNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            syncFailReason = BuildSyncFailureReason(localizer, "Sync_Error_DocumentRepositoryUnreachable", ex.Message);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkDocumentSyncFailed(syncFailReason);
            auditSummary = string.Format(localizer["Audit_Documents_SyncFailed"], syncFailReason);
            auditAction = "documents-sync-failed";
        }
        else
        {
            var existingIds = claim.Documents.Select(document => document.StorageIdentifier).ToHashSet();
            var importedCount = 0;

            foreach (var storedDocument in repoDocuments ?? [])
            {
                if (existingIds.Contains(storedDocument.StorageIdentifier))
                {
                    continue;
                }

                var imported = claim.AddDocument(
                    storedDocument.FileName,
                    storedDocument.FileType,
                    storedDocument.StorageIdentifier,
                    userId,
                    syncedAtUtc,
                    storedDocument.ContentType,
                    storedDocument.FileSizeBytes,
                    source: "repository-sync");

                dbContext.ClaimDocuments.Add(imported);
                existingIds.Add(storedDocument.StorageIdentifier);
                importedCount++;
            }

            auditSummary = claim.ApplyDocumentSync(syncedAtUtc, importedCount);
            auditAction = "documents-synced";
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            auditAction,
            auditSummary,
            userId,
            syncedAtUtc).ToEntity());

        return new ClaimSyncResult("documents", syncFailReason is null);
    }

    /// <summary>
    /// Builds a localized reconciliation summary describing sync outcomes.
    /// </summary>
    private static string BuildReconciliationSummary(
        IReadOnlyList<ClaimSyncResult> syncResults,
        IReadOnlyList<string> recoveredDependencies,
        IReadOnlySet<string> unresolvedDependencies,
        IStringLocalizer<SharedMessages> localizer)
    {
        var retried = string.Join(", ", syncResults.Select(result => GetDependencyDisplayName(result.Dependency)));
        var summaryParts = new List<string>
        {
            string.Format(localizer["Reconciliation_Retried"], retried)
        };

        summaryParts.Add(recoveredDependencies.Count == 0
            ? localizer["Reconciliation_NoRecovered"].Value
            : string.Format(localizer["Reconciliation_Recovered"], string.Join(", ", recoveredDependencies.Select(GetDependencyDisplayName)))
        );

        summaryParts.Add(unresolvedDependencies.Count == 0
            ? localizer["Reconciliation_AllResolved"].Value
            : string.Format(localizer["Reconciliation_StillUnresolved"], string.Join(", ", unresolvedDependencies.OrderBy(static dependency => dependency, StringComparer.Ordinal).Select(GetDependencyDisplayName)))
        );

        return string.Join(' ', summaryParts);
    }

    /// <summary>
    /// Builds a localized sync failure reason with truncation support.
    /// </summary>
    private static string BuildSyncFailureReason(
        IStringLocalizer<SharedMessages> localizer,
        string resourceKey,
        string exceptionMessage)
    {
        var reason = string.Format(localizer[resourceKey], exceptionMessage);
        return reason.Length > 200 ? reason[..200] : reason;
    }

    /// <summary>
    /// Gets the localized display name for a sync dependency.
    /// </summary>
    private static string GetDependencyDisplayName(string dependency)
    {
        return dependency switch
        {
            "policy" => "Policy",
            "payment" => "Payment",
            "documents" => "Documents",
            _ => dependency,
        };
    }

    /// <summary>
    /// Represents a synchronization result for a specific dependency.
    /// </summary>
    private sealed record ClaimSyncResult(string Dependency, bool Succeeded);

    /// <summary>
    /// Builds a localized audit summary for a note addition.
    /// </summary>
    private static string BuildNoteAuditSummary(string content, IStringLocalizer<SharedMessages> localizer)
    {
        const int maxPreviewLength = 80;
        var preview = content.Length <= maxPreviewLength
            ? content
            : $"{content[..maxPreviewLength].TrimEnd()}...";

        return string.Format(localizer["Audit_NoteAdded"], preview);
    }

    /// <summary>
    /// Builds a localized audit summary for a document upload.
    /// </summary>
    private static string BuildDocumentAuditSummary(string fileName, string fileType, IStringLocalizer<SharedMessages> localizer)
    {
        return string.Format(localizer["Audit_DocumentUploaded"], fileName, fileType);
    }
}

/// <summary>
/// Marker class for localizing shared messages.
/// </summary>
internal class SharedMessages
{
}

