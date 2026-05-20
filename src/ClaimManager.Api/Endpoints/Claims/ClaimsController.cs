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
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Npgsql;
using System.Text.Json;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;
using ClaimEntity = ClaimManager.Domain.Claims.Claim;

public sealed record GetClaimsQueryParams(
    string? Search = null,
    string? Status = null,
    string? BlockerType = null,
    bool? HasBlocker = null,
    string? OwnedByUserId = null,
    int Page = 1,
    int PageSize = 20);

/// <summary>
/// Defines endpoints for managing insurance claims throughout their lifecycle.
/// Includes operations for claim creation, updates, workflow progression, document management,
/// third-party system synchronization, and claimant communication.
/// </summary>
/// <remarks>
/// All endpoints require Adjuster authorization policy and support localized responses via IStringLocalizer.
/// </remarks>
public static class ClaimsController
{
    /// <summary>
    /// Registers all claim-related endpoints with the ASP.NET Core routing system.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder to register routes with.</param>
    /// <returns>The endpoint route builder for method chaining.</returns>
    /// <remarks>
    /// Maps the following endpoint groups:
    /// - GET /api/claims: List claims with filtering
    /// - GET /api/claims/{id}: Retrieve claim details
    /// - POST /api/claims: Create new claim
    /// - PUT /api/claims/{id}: Update claim information
    /// - Workflow operations: Advance, route for approval
    /// - Document operations: Upload, sync
    /// - Communication operations: Send notifications, retry delivery
    /// </remarks>
    public static IEndpointRouteBuilder MapClaimEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/claims")
            .WithTags("Claims")
            .RequireAuthorization(ClaimManagerPolicies.Adjuster);

        group.MapGet("/", GetClaimsAsync)
            .WithName("GetClaims")
            .Produces<ClaimSummaryPagedResponseDto>(StatusCodes.Status200OK);

        group.MapGet("/{id:guid}", GetClaimDetailsAsync)
            .WithName("GetClaimDetails")
            .Produces<ClaimDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateClaimAsync)
            .WithName("CreateClaim")
            .Produces<ClaimDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", UpdateClaimAsync)
            .WithName("UpdateClaim")
            .Produces<ClaimDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/notes", AddClaimNoteAsync)
            .WithName("AddClaimNote")
            .Produces<ClaimNoteDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/documents", UploadClaimDocumentAsync)
            .WithName("UploadClaimDocument")
            .Produces<ClaimDocumentDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/advance", AdvanceClaimWorkflowAsync)
            .WithName("AdvanceClaimWorkflow")
            .Produces<ClaimDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/route-for-approval", RouteClaimForApprovalAsync)
            .WithName("RouteClaimForApproval")
            .Produces<ClaimDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/sync-policy", SyncClaimPolicyDataAsync)
            .WithName("SyncClaimPolicyData")
            .Produces<ClaimDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/sync-payment", SyncClaimPaymentDataAsync)
            .WithName("SyncClaimPaymentData")
            .Produces<ClaimDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/sync-documents", SyncClaimDocumentDataAsync)
            .WithName("SyncClaimDocumentData")
            .Produces<ClaimDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reconcile", ReconcileClaimStateAsync)
            .WithName("ReconcileClaimState")
            .Produces<ClaimDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/notifications", SendClaimNotificationAsync)
            .WithName("SendClaimNotification")
            .Produces<ClaimCommunicationDto>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/notifications/{notificationId:guid}/retry", RetryClaimNotificationAsync)
            .WithName("RetryClaimNotification")
            .Produces<ClaimCommunicationDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        return endpoints;
    }

    /// <summary>
    /// Retrieves a paginated list of claims with optional filtering and search.
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination.</param>
    /// <param name="dbContext">The database context for data access.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>A paginated response containing claim summaries.</returns>
    /// <remarks>
    /// Supports filtering by:
    /// - Search: Claims matching number, claimant name, or policy number (case-insensitive)
    /// - Status: Specific claim workflow status
    /// - BlockerType: Claims with or without specific blocker types
    /// - OwnedByUserId: Claims assigned to a specific user
    /// Pagination defaults to page 1, max 100 items per page.
    /// </remarks>
    private static async Task<Ok<ClaimSummaryPagedResponseDto>> GetClaimsAsync(
        [AsParameters] GetClaimsQueryParams query,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
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
    /// Retrieves complete details for a specific claim including notes, documents, communications, and audit history.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to retrieve.</param>
    /// <param name="dbContext">The database context for data access.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The complete claim details with all related data, or 404 if not found.</returns>
    /// <remarks>
    /// Includes:
    /// - Basic claim information (claimant, policy, loss details)
    /// - All associated notes in chronological order
    /// - All uploaded documents with metadata
    /// - Communication history (notifications sent/received)
    /// - Full audit trail of all operations performed on the claim
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> GetClaimDetailsAsync(
        Guid id,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
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
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
    /// Creates a new insurance claim with initial policy and payment data synchronization.
    /// </summary>
    /// <param name="command">The command containing claimant and loss information.</param>
    /// <param name="principal">The authenticated user creating the claim.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="policyClient">Client for policy system integration.</param>
    /// <param name="paymentClient">Client for payment system integration.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The created claim with generated ID and initial sync status, or 409 if claim number cannot be reserved.</returns>
    /// <remarks>
    /// This operation performs the following sequence:
    /// 1. Validates input command
    /// 2. Retrieves authenticated user context
    /// 3. Attempts initial policy data retrieval and synchronization
    /// 4. Attempts initial payment status retrieval
    /// 5. Persists claim with retry logic for claim number uniqueness (3 attempts)
    /// 6. Records audit trail for creation and initial synchronizations
    /// 
    /// If initial sync fails, claim is still created with failure reasons recorded in audit trail.
    /// </remarks>
    private static async Task<Results<Created<ClaimDto>, ValidationProblem, ProblemHttpResult>> CreateClaimAsync(
        CreateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var validator = new CreateClaimCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(ToValidationDictionary(validationResult));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        PolicySummary? initialPolicyData = null;
        string? initialSyncFailReason = null;

        try
        {
            initialPolicyData = await policyClient.GetPolicyByNumberAsync(command.PolicyNumber, cancellationToken);
            if (initialPolicyData is null)
            {
                initialSyncFailReason = localizer["Sync_PolicyNotFound"].Value;
            }
        }
        catch (Exception ex)
        {
            initialSyncFailReason = localizer["Sync_PolicySystemError", ex.Message].Value;
            if (initialSyncFailReason.Length > 200)
            {
                initialSyncFailReason = initialSyncFailReason[..200];
            }
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
                initialPaymentSyncFailReason = localizer["Sync_PaymentSystemError", ex.Message].Value;
                if (initialPaymentSyncFailReason.Length > 200)
                {
                    initialPaymentSyncFailReason = initialPaymentSyncFailReason[..200];
                }
            }

            if (initialPaymentSyncFailReason is not null)
            {
                claim.MarkPaymentSyncFailed(initialPaymentSyncFailReason);
                paymentAuditSummary = localizer["Audit_InitialPaymentSyncFailed", initialPaymentSyncFailReason].Value;
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
                policyAuditSummary = localizer["Audit_InitialPolicySyncFailed", initialSyncFailReason ?? string.Empty].Value;
                policyAuditAction = "policy-sync-failed";
            }

            dbContext.Claims.Add(claim);
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                "created",
                localizer["Audit_ClaimCreated"].Value,
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
            title: localizer["Err_ClaimCreationConflict"].Value,
            detail: localizer["Err_ClaimCreationConflictDetail"].Value);
    }

    /// <summary>
    /// Synchronizes policy data from the policy system for a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The authenticated user requesting the synchronization.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="policyClient">Client for policy system integration.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The updated claim with synchronized policy data, or appropriate error response.</returns>
    /// <remarks>
    /// Retrieves current policy information from the policy system and updates the claim record.
    /// Creates audit entries for successful or failed synchronization attempts.
    /// Handles concurrency conflicts when the claim has been modified by another user.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPolicyDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPolicySystemClient policyClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
                title: localizer["Err_ConcurrencyConflict"].Value,
                detail: localizer["Err_ConcurrencyConflictDetail"].Value);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Synchronizes payment data from the payment system for a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The authenticated user requesting the synchronization.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="paymentClient">Client for payment system integration.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The updated claim with synchronized payment data, or appropriate error response.</returns>
    /// <remarks>
    /// Retrieves current payment status and records from the payment system and updates the claim.
    /// Creates audit entries for successful or failed synchronization attempts.
    /// Handles concurrency conflicts when the claim has been modified by another user.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPaymentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPaymentSystemClient paymentClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
                title: localizer["Err_ConcurrencyConflict"].Value,
                detail: localizer["Err_ConcurrencyConflictDetail"].Value);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Synchronizes documents from the document repository for a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The authenticated user requesting the synchronization.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="documentRepository">The document repository service.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The updated claim with synchronized documents, or appropriate error response.</returns>
    /// <remarks>
    /// Retrieves the list of documents from the document repository and imports any new documents
    /// that are not already tracked in the claim. Creates audit entries for the synchronization operation.
    /// Handles concurrency conflicts when the claim has been modified by another user.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimDocumentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claim = await dbContext.Claims
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
                title: localizer["Err_ConcurrencyConflict"].Value,
                detail: localizer["Err_ConcurrencyConflictDetail"].Value);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Reconciles a claim's state by synchronizing all third-party data dependencies.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The authenticated user requesting the reconciliation.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="policyClient">Client for policy system integration.</param>
    /// <param name="paymentClient">Client for payment system integration.</param>
    /// <param name="documentRepository">The document repository service.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The updated claim with reconciled state, or appropriate error response.</returns>
    /// <remarks>
    /// This is a comprehensive operation that:
    /// 1. Synchronizes policy, payment, and document data in a single transaction
    /// 2. Tracks which dependencies were previously unresolved
    /// 3. Records which dependencies recovered during this attempt
    /// 4. Creates detailed audit trail of reconciliation results
    /// Useful for resolving data integrity issues when external systems have been restored.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> ReconcileClaimStateAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
                title: localizer["Err_ConcurrencyConflict"].Value,
                detail: localizer["Err_ConcurrencyConflictDetail"].Value);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Updates core claim information including claimant details and loss information.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The command containing updated claim information.</param>
    /// <param name="principal">The authenticated user performing the update.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The updated claim, or appropriate error response.</returns>
    /// <remarks>
    /// Updates only core claim information. Does not affect workflow state, documents, or communications.
    /// Creates audit trail only if changes are actually made.
    /// Uses optimistic concurrency control to detect conflicting modifications.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> UpdateClaimAsync(
        Guid id,
        UpdateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };

        var validator = new UpdateClaimCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(ToValidationDictionary(validationResult));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
                    title: localizer["Err_ConcurrencyConflict"].Value,
                    detail: localizer["Err_ConcurrencyConflictDetail"].Value);
            }
        }

        var auditHistory = await GetAuditHistoryAsync(dbContext, claim.Id, cancellationToken);
        return TypedResults.Ok(ClaimDto.FromClaim(claim, auditHistory));
    }

    /// <summary>
    /// Adds a note (comment) to a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The command containing note content.</param>
    /// <param name="principal">The authenticated user adding the note.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The created note, or appropriate error response.</returns>
    /// <remarks>
    /// Notes are internal comments not visible to claimants. Created with timestamp and user attribution.
    /// Creates audit trail entry for the note addition.
    /// </remarks>
    private static async Task<Results<Created<ClaimNoteDto>, ValidationProblem, ProblemHttpResult>> AddClaimNoteAsync(
        Guid id,
        AddClaimNoteCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var validator = new AddClaimNoteCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(ToValidationDictionary(validationResult));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
    /// <param name="file">The file being uploaded.</param>
    /// <param name="principal">The authenticated user uploading the document.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="documentRepository">The document repository service for persistent storage.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The created document metadata, or appropriate error response.</returns>
    /// <remarks>
    /// Documents are stored in the configured document repository and referenced in the claim record.
    /// If storage fails, the document entry is automatically rolled back.
    /// Creates audit trail entry for the document upload.
    /// </remarks>
    private static async Task<Results<Created<ClaimDocumentDto>, ValidationProblem, ProblemHttpResult>> UploadClaimDocumentAsync(
        Guid id,
        IFormFile? file,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
            return TypedResults.ValidationProblem(ToValidationDictionary(validationResult));
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
    /// Advances a claim to the next workflow stage.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The command containing workflow progression details.</param>
    /// <param name="principal">The authenticated user advancing the claim.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The updated claim with new workflow state, or appropriate error response.</returns>
    /// <remarks>
    /// Validates that the transition is allowed based on current claim state.
    /// Only advances if the claim is not blocked and meets preconditions for the next stage.
    /// Creates audit trail with workflow transition details.
    /// Uses optimistic concurrency control to detect conflicting modifications.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> AdvanceClaimWorkflowAsync(
        Guid id,
        AdvanceClaimWorkflowCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
                title: localizer["Err_InvalidWorkflowTransition"].Value,
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
                title: localizer["Err_ConcurrencyConflict"].Value,
                detail: localizer["Err_ConcurrencyConflictDetail"].Value);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Routes a claim for payment approval.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The command containing approval routing details and rationale.</param>
    /// <param name="principal">The authenticated user routing the claim.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The updated claim with approval routing status, or appropriate error response.</returns>
    /// <remarks>
    /// Moves the claim into an approval queue with documented rationale.
    /// Can only be performed on claims in specific states that allow approval routing.
    /// Creates audit trail with routing rationale.
    /// Uses optimistic concurrency control to detect conflicting modifications.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> RouteClaimForApprovalAsync(
        Guid id,
        RouteClaimForApprovalCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };

        var validator = new RouteClaimForApprovalCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(ToValidationDictionary(validationResult));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
                title: localizer["Err_InvalidWorkflowTransition"].Value,
                detail: ex.Message);
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            "routed-for-approval",
            localizer["Audit_RoutedForApproval", claim.BlockerReason ?? string.Empty].Value,
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
                title: localizer["Err_ConcurrencyConflict"].Value,
                detail: localizer["Err_ConcurrencyConflictDetail"].Value);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Sends a notification to the claimant.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The command containing notification content and delivery settings.</param>
    /// <param name="principal">The authenticated user sending the notification.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="messagingClient">Client for message delivery integration.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The created communication record, or appropriate error response.</returns>
    /// <remarks>
    /// Supports multiple communication types and channels. If type is "claimant-safe", applies transformations.
    /// Records delivery results (success with delivery ID or failure with reason) in the communication record.
    /// Creates audit entries for both sent and failed notifications.
    /// </remarks>
    private static async Task<Results<Created<ClaimCommunicationDto>, ValidationProblem, ProblemHttpResult>> SendClaimNotificationAsync(
        Guid id,
        SendClaimNotificationCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var validator = new SendClaimNotificationCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(ToValidationDictionary(validationResult));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var claimExists = await dbContext.Claims.AnyAsync(c => c.Id == id, cancellationToken);
        if (!claimExists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_ClaimNotFound"].Value,
                detail: localizer["Err_ClaimNotFoundDetail"].Value);
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
            deliveryResult = new MessageDeliveryResult(false, null, localizer["Sync_MessagingClientError", ex.Message].Value);
        }

        if (deliveryResult.Success && deliveryResult.DeliveryId is not null)
        {
            communication.RecordSent(deliveryResult.DeliveryId, attemptAtUtc);
        }
        else
        {
            communication.RecordFailed(deliveryResult.FailureReason ?? localizer["Sync_DeliveryFailedNoReason"].Value, attemptAtUtc);
        }

        var auditSummary = communication.Status == "sent"
            ? localizer["Audit_NotificationSent", communication.CommunicationType, communication.Channel, communication.Recipient, communication.DeliveryId ?? string.Empty].Value
            : localizer["Audit_NotificationFailed", communication.CommunicationType, communication.Channel, communication.Recipient, communication.FailureReason ?? string.Empty].Value;

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
    /// Retries sending a previously failed notification.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="notificationId">The unique identifier of the notification to retry.</param>
    /// <param name="principal">The authenticated user retrying the notification.</param>
    /// <param name="userManager">Service for user account management.</param>
    /// <param name="messagingClient">Client for message delivery integration.</param>
    /// <param name="dbContext">The database context for data persistence.</param>
    /// <param name="localizer">The string localizer for localized messages.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The updated communication record after retry attempt, or appropriate error response.</returns>
    /// <remarks>
    /// Only notifications in failed state are eligible for retry.
    /// Records the new delivery attempt result and creates audit entry.
    /// </remarks>
    private static async Task<Results<Ok<ClaimCommunicationDto>, ProblemHttpResult>> RetryClaimNotificationAsync(
        Guid id,
        Guid notificationId,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Err_AuthenticationRequired"].Value,
                detail: localizer["Err_AuthenticationRequiredDetail"].Value);
        }

        var communication = await dbContext.ClaimCommunications
            .SingleOrDefaultAsync(c => c.Id == notificationId && c.ClaimId == id, cancellationToken);

        if (communication is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Err_NotificationNotFound"].Value,
                detail: localizer["Err_NotificationNotFoundDetail"].Value);
        }

        if (!communication.IsRetryEligible())
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Err_RetryNotAllowed"].Value,
                detail: localizer["Err_RetryNotAllowedDetail", communication.Status].Value);
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
            deliveryResult = new MessageDeliveryResult(false, null, localizer["Sync_MessagingClientError", ex.Message].Value);
        }

        if (deliveryResult.Success && deliveryResult.DeliveryId is not null)
        {
            communication.RecordSent(deliveryResult.DeliveryId, attemptAtUtc);
        }
        else
        {
            communication.RecordFailed(deliveryResult.FailureReason ?? localizer["Sync_DeliveryFailedNoReason"].Value, attemptAtUtc);
        }

        var auditSummary = communication.Status == "sent"
            ? localizer["Audit_NotificationRetrySucceeded", communication.CommunicationType, communication.Channel, communication.Recipient, communication.DeliveryId ?? string.Empty].Value
            : localizer["Audit_NotificationRetryFailed", communication.CommunicationType, communication.Channel, communication.Recipient, communication.FailureReason ?? string.Empty].Value;

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            id,
            communication.Status == "sent" ? "notification-sent" : "notification-failed",
            auditSummary,
            user.Id.ToString(),
            attemptAtUtc).ToEntity());

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(ClaimCommunicationDto.FromCommunication(communication));
    }

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

    private static bool IsClaimNumberConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
            && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
            && string.Equals(postgresException.ConstraintName, "ix_claims_claim_number", StringComparison.Ordinal);
    }

    private static Dictionary<string, string[]> ToValidationDictionary(ValidationResult validationResult)
    {
        return validationResult.Errors
            .GroupBy(error => JsonNamingPolicy.CamelCase.ConvertName(error.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
    }

    private static string BuildUpdateSummary(ClaimEntity claim, UpdateClaimCommand command)
    {
        var changes = new List<string>();

        AddChange(changes, "Claimant name", claim.ClaimantName, command.ClaimantName);
        AddChange(changes, "Claimant email", claim.ClaimantEmail, command.ClaimantEmail);
        AddChange(changes, "Claimant phone", claim.ClaimantPhone, command.ClaimantPhone);
        AddChange(changes, "Policy number", claim.PolicyNumber, command.PolicyNumber);
        AddChange(changes, "Loss type", claim.LossType, command.LossType);
        AddChange(changes, "Loss description", claim.LossDescription, command.LossDescription);

        if (claim.LossDateUtc != command.LossDateUtc)
        {
            changes.Add($"Loss date updated from {claim.LossDateUtc:yyyy-MM-dd} to {command.LossDateUtc:yyyy-MM-dd}.");
        }

        return changes.Count == 0
            ? "Claim file reviewed with no material changes."
            : string.Join(' ', changes);
    }

    private static void AddChange(List<string> changes, string label, string currentValue, string newValue)
    {
        var normalizedNewValue = newValue.Trim();
        if (!string.Equals(currentValue, normalizedNewValue, StringComparison.Ordinal))
        {
            changes.Add($"{label} updated from '{currentValue}' to '{normalizedNewValue}'.");
        }
    }

    private static async Task<ClaimSyncResult> ExecutePolicySyncAsync(
        ClaimEntity claim,
        string userId,
        IPolicySystemClient policyClient,
        ClaimManagerDbContext dbContext,
        DateTime syncedAtUtc,
        IStringLocalizer localizer,
        CancellationToken cancellationToken)
    {
        PolicySummary? policyData = null;
        string? syncFailReason = null;

        try
        {
            policyData = await policyClient.GetPolicyByNumberAsync(claim.PolicyNumber, cancellationToken);
            if (policyData is null)
            {
                syncFailReason = localizer["Sync_PolicyNotFound"].Value;
            }
        }
        catch (Exception ex)
        {
            syncFailReason = BuildSyncFailureReason(localizer["Sync_PolicySystemError", ex.Message].Value);
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
            auditSummary = localizer["Audit_PolicySyncFailed", syncFailReason ?? string.Empty].Value;
            auditAction = "policy-sync-failed";
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(claim.Id, auditAction, auditSummary, userId, syncedAtUtc).ToEntity());
        return new ClaimSyncResult("policy", succeeded);
    }

    private static async Task<ClaimSyncResult> ExecutePaymentSyncAsync(
        ClaimEntity claim,
        string userId,
        IPaymentSystemClient paymentClient,
        ClaimManagerDbContext dbContext,
        DateTime syncedAtUtc,
        IStringLocalizer localizer,
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
            syncFailReason = BuildSyncFailureReason(localizer["Sync_PaymentSystemError", ex.Message].Value);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkPaymentSyncFailed(syncFailReason);
            auditSummary = localizer["Audit_PaymentSyncFailed", syncFailReason].Value;
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

    private static async Task<ClaimSyncResult> ExecuteDocumentSyncAsync(
        ClaimEntity claim,
        string userId,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        DateTime syncedAtUtc,
        IStringLocalizer localizer,
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
            syncFailReason = BuildSyncFailureReason(localizer["Sync_PolicySystemError", ex.Message].Value);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkDocumentSyncFailed(syncFailReason);
            auditSummary = localizer["Audit_DocumentSyncFailed", syncFailReason].Value;
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

    private static string BuildReconciliationSummary(
        IReadOnlyList<ClaimSyncResult> syncResults,
        IReadOnlyList<string> recoveredDependencies,
        IReadOnlySet<string> unresolvedDependencies,
        IStringLocalizer localizer)
    {
        var retried = string.Join(", ", syncResults.Select(result => GetDependencyDisplayName(result.Dependency)));
        var summaryParts = new List<string>
        {
            localizer["Audit_ReconciliationRetried", retried].Value
        };

        summaryParts.Add(recoveredDependencies.Count == 0
            ? localizer["Audit_ReconciliationNoRecovery"].Value
            : localizer["Audit_ReconciliationRecovered", string.Join(", ", recoveredDependencies.Select(GetDependencyDisplayName))].Value
        );

        summaryParts.Add(unresolvedDependencies.Count == 0
            ? localizer["Audit_ReconciliationAllResolved"].Value
            : localizer["Audit_ReconciliationStillUnresolved", string.Join(", ", unresolvedDependencies.OrderBy(static dependency => dependency, StringComparer.Ordinal).Select(GetDependencyDisplayName))].Value
        );

        return string.Join(' ', summaryParts);
    }

    private static string BuildSyncFailureReason(string errorMessage)
    {
        return errorMessage.Length > 200 ? errorMessage[..200] : errorMessage;
    }

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

    private sealed record ClaimSyncResult(string Dependency, bool Succeeded);

    private static string BuildUpdateSummary(
        ClaimEntity claim,
        UpdateClaimCommand command,
        IStringLocalizer localizer)
    {
        var changes = new List<string>();

        AddChange(changes, "Claimant name", claim.ClaimantName, command.ClaimantName);
        AddChange(changes, "Claimant email", claim.ClaimantEmail, command.ClaimantEmail);
        AddChange(changes, "Claimant phone", claim.ClaimantPhone, command.ClaimantPhone);
        AddChange(changes, "Policy number", claim.PolicyNumber, command.PolicyNumber);
        AddChange(changes, "Loss type", claim.LossType, command.LossType);
        AddChange(changes, "Loss description", claim.LossDescription, command.LossDescription);

        if (claim.LossDateUtc != command.LossDateUtc)
        {
            changes.Add($"Loss date updated from {claim.LossDateUtc:yyyy-MM-dd} to {command.LossDateUtc:yyyy-MM-dd}.");
        }

        return changes.Count == 0
            ? localizer["Audit_ClaimReviewedNoChanges"].Value
            : string.Join(' ', changes);
    }

    private static string BuildNoteAuditSummary(string content, IStringLocalizer localizer)
    {
        const int maxPreviewLength = 80;
        var preview = content.Length <= maxPreviewLength
            ? content
            : $"{content[..maxPreviewLength].TrimEnd()}...";

        return localizer["Audit_NoteAdded", preview].Value;
    }

    private static string BuildDocumentAuditSummary(string fileName, string fileType, IStringLocalizer localizer)
    {
        return localizer["Audit_DocumentUploaded", fileName, fileType].Value;
    }
}