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

public sealed record GetClaimsQueryParams(
    string? Search = null,
    string? Status = null,
    string? BlockerType = null,
    bool? HasBlocker = null,
    string? OwnedByUserId = null,
    int Page = 1,
    int PageSize = 20);

public static class ClaimsController
{
    /// <summary>
    /// Maps all claim-related endpoints to the route builder.
    /// </summary>
    /// <param name="endpoints">The endpoint route builder.</param>
    /// <returns>The endpoint route builder for chaining.</returns>
    public static IEndpointRouteBuilder MapClaimEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/claims")
            .WithTags("Claims")
            .RequireAuthorization(ClaimManagerPolicies.Adjuster);

        group.MapGet("/", GetClaimsAsync)
            .WithName("GetClaims")
            .WithOpenApi();
        group.MapGet("/{id:guid}", GetClaimDetailsAsync)
            .WithName("GetClaimDetails")
            .WithOpenApi();
        group.MapPost("/", CreateClaimAsync)
            .WithName("CreateClaim")
            .WithOpenApi();
        group.MapPut("/{id:guid}", UpdateClaimAsync)
            .WithName("UpdateClaim")
            .WithOpenApi();
        group.MapPost("/{id:guid}/notes", AddClaimNoteAsync)
            .WithName("AddClaimNote")
            .WithOpenApi();
        group.MapPost("/{id:guid}/documents", UploadClaimDocumentAsync)
            .WithName("UploadClaimDocument")
            .WithOpenApi();
        group.MapPost("/{id:guid}/advance", AdvanceClaimWorkflowAsync)
            .WithName("AdvanceClaimWorkflow")
            .WithOpenApi();
        group.MapPost("/{id:guid}/route-for-approval", RouteClaimForApprovalAsync)
            .WithName("RouteClaimForApproval")
            .WithOpenApi();
        group.MapPost("/{id:guid}/sync-policy", SyncClaimPolicyDataAsync)
            .WithName("SyncClaimPolicyData")
            .WithOpenApi();
        group.MapPost("/{id:guid}/sync-payment", SyncClaimPaymentDataAsync)
            .WithName("SyncClaimPaymentData")
            .WithOpenApi();
        group.MapPost("/{id:guid}/sync-documents", SyncClaimDocumentDataAsync)
            .WithName("SyncClaimDocumentData")
            .WithOpenApi();
        group.MapPost("/{id:guid}/reconcile", ReconcileClaimStateAsync)
            .WithName("ReconcileClaimState")
            .WithOpenApi();
        group.MapPost("/{id:guid}/notifications", SendClaimNotificationAsync)
            .WithName("SendClaimNotification")
            .WithOpenApi();
        group.MapPost("/{id:guid}/notifications/{notificationId:guid}/retry", RetryClaimNotificationAsync)
            .WithName("RetryClaimNotification")
            .WithOpenApi();

        return endpoints;
    }

    /// <summary>
    /// Retrieves a paginated list of claims with optional filtering by search term, status, blocker type, and owner.
    /// </summary>
    /// <param name="query">The query parameters including search, status, blocker type, pagination info, and owner ID.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A paged response containing claim summaries.</returns>
    /// <remarks>
    /// This endpoint returns all claims visible to the authenticated user based on their authorization level.
    /// Results are sorted by most recently updated (or created) first, then by claim number.
    /// Supports filtering by:
    /// - Search term (matches claim number, claimant name, or policy number)
    /// - Claim status
    /// - Blocker type
    /// - Ownership (optional, filters to specific user)
    /// </remarks>
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
    /// Retrieves detailed information about a specific claim, including notes, documents, communications, and audit history.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A <see cref="ClaimDto"/> containing full claim details, or a 404 problem response if the claim is not found.
    /// </returns>
    /// <remarks>
    /// Returns HTTP 200 with complete claim data including:
    /// - Basic claim information (claimant, policy, loss details)
    /// - Associated notes and documents
    /// - Communication history
    /// - Complete audit trail
    /// Returns HTTP 404 if the claim does not exist.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> GetClaimDetailsAsync(
        Guid id,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
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
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
    /// Creates a new claim with initial claimant, claim, and loss information.
    /// Automatically synchronizes policy and payment data from external systems.
    /// </summary>
    /// <param name="command">The create claim command containing claim details.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="policyClient">The policy system client.</param>
    /// <param name="paymentClient">The payment system client.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// A 201 Created response with the newly created claim, a validation problem if input is invalid,
    /// or a 401/409 problem response for authentication or conflict errors.
    /// </returns>
    /// <remarks>
    /// The endpoint performs the following:
    /// 1. Validates input using <see cref="CreateClaimCommandValidator"/>
    /// 2. Generates a unique claim number (CLM-NNNN format)
    /// 3. Creates initial claim record with claimant and loss information
    /// 4. Attempts to synchronize policy data from the policy system
    /// 5. Attempts to synchronize payment data from the payment system
    /// 6. Records audit trail entries for all operations
    /// 7. Returns HTTP 201 with the created claim
    /// 
    /// May return HTTP 409 if claim number generation fails after 3 retry attempts.
    /// </remarks>
    private static async Task<Results<Created<ClaimDto>, ValidationProblem, ProblemHttpResult>> CreateClaimAsync(
        CreateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
        IStringLocalizer<ClaimsController> localizer,
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
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        PolicySummary? initialPolicyData = null;
        string? initialSyncFailReason = null;

        try
        {
            initialPolicyData = await policyClient.GetPolicyByNumberAsync(command.PolicyNumber, cancellationToken);
            if (initialPolicyData is null)
            {
                initialSyncFailReason = localizer["Summary_PolicyNotFound"].Value;
            }
        }
        catch (Exception ex)
        {
            initialSyncFailReason = BuildSyncFailureReason(
                localizer,
                "Policy system",
                ex.Message);
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
                initialPaymentSyncFailReason = BuildSyncFailureReason(
                    localizer,
                    "Payment system",
                    ex.Message);
            }

            if (initialPaymentSyncFailReason is not null)
            {
                claim.MarkPaymentSyncFailed(initialPaymentSyncFailReason);
                paymentAuditSummary = $"{localizer["Summary_PaymentSyncFailed"]}: {initialPaymentSyncFailReason}";
                paymentAuditAction = localizer["AuditAction_PaymentSyncFailed"];
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
                paymentAuditAction = localizer["AuditAction_PaymentSynced"];
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
                policyAuditAction = localizer["AuditAction_PolicySynced"];
            }
            else
            {
                claim.MarkPolicySyncFailed(initialSyncFailReason!);
                policyAuditSummary = $"{localizer["Summary_PolicySyncFailed"]}: {initialSyncFailReason}";
                policyAuditAction = localizer["AuditAction_PolicySyncFailed"];
            }

            dbContext.Claims.Add(claim);
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                localizer["AuditAction_ClaimCreated"],
                localizer["Summary_AuditClaimCreated"],
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
            title: localizer["Errors_ClaimCreationConflict_Title"],
            detail: localizer["Errors_ClaimCreationConflict_Detail"]);
    }

    /// <summary>
    /// Synchronizes policy data for a claim from the external policy system.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="policyClient">The policy system client.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated claim with synchronized policy data, or an error response.</returns>
    /// <remarks>
    /// Attempts to retrieve policy data from the external policy system and apply it to the claim.
    /// Records success or failure in the audit trail. Returns HTTP 409 if another user modified the claim concurrently.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPolicyDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPolicySystemClient policyClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
                title: localizer["Errors_ConcurrencyConflict_Title"],
                detail: localizer["Errors_ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Synchronizes payment data for a claim from the external payment system.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="paymentClient">The payment system client.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated claim with synchronized payment data, or an error response.</returns>
    /// <remarks>
    /// Attempts to retrieve payment data from the external payment system and apply it to the claim.
    /// Records success or failure in the audit trail. Returns HTTP 409 if another user modified the claim concurrently.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPaymentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPaymentSystemClient paymentClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
                title: localizer["Errors_ConcurrencyConflict_Title"],
                detail: localizer["Errors_ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Synchronizes documents for a claim from the external document repository.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="documentRepository">The document repository.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated claim with synchronized documents, or an error response.</returns>
    /// <remarks>
    /// Attempts to retrieve document list from the external repository and import any new documents.
    /// Records success or failure in the audit trail. Returns HTTP 409 if another user modified the claim concurrently.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimDocumentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
                title: localizer["Errors_ConcurrencyConflict_Title"],
                detail: localizer["Errors_ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Reconciles the claim state by synchronizing all external data sources (policy, payment, documents).
    /// Tracks recovered dependencies and unresolved issues.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="policyClient">The policy system client.</param>
    /// <param name="paymentClient">The payment system client.</param>
    /// <param name="documentRepository">The document repository.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The reconciled claim with updated status, or an error response.</returns>
    /// <remarks>\n    /// Performs a comprehensive reconciliation attempt:
    /// 1. Records current unresolved dependencies before sync
    /// 2. Synchronizes policy, payment, and document data
    /// 3. Identifies recovered dependencies
    /// 4. Records reconciliation outcome in audit trail
    /// Returns HTTP 409 if another user modified the claim concurrently.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> ReconcileClaimStateAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
            localizer["AuditAction_ClaimReconciled"],
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
                title: localizer["Errors_ConcurrencyConflict_Title"],
                detail: localizer["Errors_ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Updates core claim details (claimant information, policy, loss details).
    /// Only changes material information; trivial updates are not recorded.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The update claim command containing new claim values.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The updated claim, a validation problem if input is invalid,
    /// or an error response for authentication or concurrency issues.
    /// </returns>
    /// <remarks>
    /// Updates the following fields:
    /// - Claimant name, email, phone
    /// - Policy number
    /// - Loss date, type, description
    /// 
    /// Changes are recorded only if material changes are detected.
    /// Returns HTTP 409 if another user modified the claim concurrently.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> UpdateClaimAsync(
        Guid id,
        UpdateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
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
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
                localizer["AuditAction_Updated"],
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
                    title: localizer["Errors_ConcurrencyConflict_Title"],
                    detail: localizer["Errors_ConcurrencyConflict_Detail"]);
            }
        }

        var auditHistory = await GetAuditHistoryAsync(dbContext, claim.Id, cancellationToken);
        return TypedResults.Ok(ClaimDto.FromClaim(claim, auditHistory));
    }

    /// <summary>
    /// Adds a note to a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The add note command containing note content.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The newly created note, a validation problem if invalid, or an error response.</returns>
    private static async Task<Results<Created<ClaimNoteDto>, ValidationProblem, ProblemHttpResult>> AddClaimNoteAsync(
        Guid id,
        AddClaimNoteCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
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
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
        }

        var createdAtUtc = DateTime.UtcNow;
        var note = claim.AddNote(command.Content, user.Id.ToString(), createdAtUtc);

        dbContext.ClaimNotes.Add(note);
        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            localizer["AuditAction_NoteAdded"],
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
    /// <param name="file">The file to upload (optional; can be null).</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="documentRepository">The document repository.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The newly created document reference, a validation problem if invalid, or an error response.</returns>
    private static async Task<Results<Created<ClaimDocumentDto>, ValidationProblem, ProblemHttpResult>> UploadClaimDocumentAsync(
        Guid id,
        IFormFile? file,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
                localizer["AuditAction_DocumentUploaded"],
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
    /// Advances the claim workflow to the next state.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The advance workflow command.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated claim with new workflow state, or an error response.</returns>
    /// <remarks>
    /// Attempts to transition the claim to the next valid workflow state.
    /// Returns HTTP 409 if the workflow transition is invalid or if another user modified the claim concurrently.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> AdvanceClaimWorkflowAsync(
        Guid id,
        AdvanceClaimWorkflowCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
                title: localizer["Errors_InvalidWorkflowTransition_Title"],
                detail: ex.Message);
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            localizer["AuditAction_WorkflowAdvanced"],
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
                title: localizer["Errors_ConcurrencyConflict_Title"],
                detail: localizer["Errors_ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Routes a claim for payment approval with a rationale.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The route for approval command containing the rationale.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated claim, a validation problem if invalid, or an error response.</returns>
    /// <remarks>
    /// Routes the claim for approval in the payment workflow.
    /// Returns HTTP 409 if the workflow transition is invalid or if another user modified the claim concurrently.
    /// </remarks>
    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> RouteClaimForApprovalAsync(
        Guid id,
        RouteClaimForApprovalCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
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
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
                title: localizer["Errors_InvalidWorkflowTransition_Title"],
                detail: ex.Message);
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            localizer["AuditAction_RoutedForApproval"],
            string.Format(localizer["Summary_RoutedForApproval"].Value, claim.BlockerReason),
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
                title: localizer["Errors_ConcurrencyConflict_Title"],
                detail: localizer["Errors_ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Sends a notification (message) to the claimant regarding the claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="command">The send notification command.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="messagingClient">The messaging client.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The created communication record, a validation problem if invalid, or an error response.</returns>
    /// <remarks>
    /// Creates a communication record and attempts to send via the specified channel.
    /// For claimant-safe communications, applies safety transformations to the subject and body.
    /// Records whether the send succeeded or failed in the communication and audit trail.
    /// </remarks>
    private static async Task<Results<Created<ClaimCommunicationDto>, ValidationProblem, ProblemHttpResult>> SendClaimNotificationAsync(
        Guid id,
        SendClaimNotificationCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
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
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var claimExists = await dbContext.Claims.AnyAsync(c => c.Id == id, cancellationToken);
        if (!claimExists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_ClaimNotFound_Title"],
                detail: localizer["Errors_ClaimNotFound_Detail"]);
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
            ? string.Format(
                localizer["Summary_NotificationSent"].Value,
                communication.CommunicationType,
                communication.Channel,
                communication.Recipient,
                communication.DeliveryId)
            : string.Format(
                localizer["Summary_NotificationFailed"].Value,
                communication.CommunicationType,
                communication.Channel,
                communication.Recipient,
                communication.FailureReason);

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            id,
            communication.Status == "sent" ? localizer["AuditAction_NotificationSent"] : localizer["AuditAction_NotificationFailed"],
            auditSummary,
            user.Id.ToString(),
            attemptAtUtc).ToEntity());

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/claims/{id}/notifications/{communication.Id}",
            ClaimCommunicationDto.FromCommunication(communication));
    }

    /// <summary>
    /// Retries a failed notification for a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="notificationId">The unique identifier of the notification to retry.</param>
    /// <param name="principal">The claims principal of the authenticated user.</param>
    /// <param name="userManager">The user manager.</param>
    /// <param name="messagingClient">The messaging client.</param>
    /// <param name="dbContext">The database context.</param>
    /// <param name="localizer">The string localizer for translating messages.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The updated communication record, or an error response.</returns>
    /// <remarks>
    /// Only notifications in the 'failed' state can be retried.
    /// Returns HTTP 409 if the notification is not in a retry-eligible state.
    /// </remarks>
    private static async Task<Results<Ok<ClaimCommunicationDto>, ProblemHttpResult>> RetryClaimNotificationAsync(
        Guid id,
        Guid notificationId,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizer<ClaimsController> localizer,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: localizer["Errors_AuthenticationRequired_Title"],
                detail: localizer["Errors_AuthenticationRequired_Detail"]);
        }

        var communication = await dbContext.ClaimCommunications
            .SingleOrDefaultAsync(c => c.Id == notificationId && c.ClaimId == id, cancellationToken);

        if (communication is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: localizer["Errors_NotificationNotFound_Title"],
                detail: localizer["Errors_NotificationNotFound_Detail"]);
        }

        if (!communication.IsRetryEligible())
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["Errors_NotificationRetryNotAllowed_Title"],
                detail: string.Format(
                    localizer["Errors_NotificationRetryNotAllowed_Detail"].Value,
                    communication.Status));
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

        var auditSummary = communication.Status == "sent"
            ? string.Format(
                localizer["Summary_NotificationRetrySent"].Value,
                communication.CommunicationType,
                communication.Channel,
                communication.Recipient,
                communication.DeliveryId)
            : string.Format(
                localizer["Summary_NotificationRetryFailed"].Value,
                communication.CommunicationType,
                communication.Channel,
                communication.Recipient,
                communication.FailureReason);

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            id,
            communication.Status == "sent" ? localizer["AuditAction_NotificationSent"] : localizer["AuditAction_NotificationFailed"],
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

    private static string BuildUpdateSummary(
        ClaimEntity claim,
        UpdateClaimCommand command,
        IStringLocalizer<ClaimsController> localizer)
    {
        var changes = new List<string>();

        AddChange(
            changes,
            localizer["Update_ClaimantName"].Value,
            claim.ClaimantName,
            command.ClaimantName,
            localizer);
        AddChange(
            changes,
            localizer["Update_ClaimantEmail"].Value,
            claim.ClaimantEmail,
            command.ClaimantEmail,
            localizer);
        AddChange(
            changes,
            localizer["Update_ClaimantPhone"].Value,
            claim.ClaimantPhone,
            command.ClaimantPhone,
            localizer);
        AddChange(
            changes,
            localizer["Update_PolicyNumber"].Value,
            claim.PolicyNumber,
            command.PolicyNumber,
            localizer);
        AddChange(
            changes,
            localizer["Update_LossType"].Value,
            claim.LossType,
            command.LossType,
            localizer);
        AddChange(
            changes,
            localizer["Update_LossDescription"].Value,
            claim.LossDescription,
            command.LossDescription,
            localizer);

        if (claim.LossDateUtc != command.LossDateUtc)
        {
            changes.Add(string.Format(
                localizer["Summary_UpdateLossDateChanged"].Value,
                claim.LossDateUtc,
                command.LossDateUtc));
        }

        return changes.Count == 0
            ? localizer["Summary_UpdateNoChanges"].Value
            : string.Join(' ', changes);
    }

    private static void AddChange(
        List<string> changes,
        string label,
        string currentValue,
        string newValue,
        IStringLocalizer<ClaimsController> localizer)
    {
        var normalizedNewValue = newValue.Trim();
        if (!string.Equals(currentValue, normalizedNewValue, StringComparison.Ordinal))
        {
            changes.Add(string.Format(
                localizer["Summary_UpdateFieldChanged"].Value,
                label,
                currentValue,
                normalizedNewValue));
        }
    }

    private static async Task<ClaimSyncResult> ExecutePolicySyncAsync(
        ClaimEntity claim,
        string userId,
        IPolicySystemClient policyClient,
        ClaimManagerDbContext dbContext,
        DateTime syncedAtUtc,
        IStringLocalizer<ClaimsController> localizer,
        CancellationToken cancellationToken)
    {
        PolicySummary? policyData = null;
        string? syncFailReason = null;

        try
        {
            policyData = await policyClient.GetPolicyByNumberAsync(claim.PolicyNumber, cancellationToken);
            if (policyData is null)
            {
                syncFailReason = localizer["Summary_PolicyNotFound"].Value;
            }
        }
        catch (Exception ex)
        {
            syncFailReason = BuildSyncFailureReason(localizer, "Policy system", ex.Message);
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
            auditAction = localizer["AuditAction_PolicySynced"];
            succeeded = true;
        }
        else
        {
            claim.MarkPolicySyncFailed(syncFailReason!);
            auditSummary = string.Format(localizer["Summary_PolicySyncFailed"].Value, syncFailReason);
            auditAction = localizer["AuditAction_PolicySyncFailed"];
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
        IStringLocalizer<ClaimsController> localizer,
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
            syncFailReason = BuildSyncFailureReason(localizer, "Payment system", ex.Message);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkPaymentSyncFailed(syncFailReason);
            auditSummary = string.Format(localizer["Summary_PaymentSyncFailed"].Value, syncFailReason);
            auditAction = localizer["AuditAction_PaymentSyncFailed"];
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
            auditAction = localizer["AuditAction_PaymentSynced"];
        }
        else
        {
            auditSummary = claim.ApplyPaymentData(null, null, null, null, null, syncedAtUtc);
            auditAction = localizer["AuditAction_PaymentSynced"];
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
        IStringLocalizer<ClaimsController> localizer,
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
            syncFailReason = BuildSyncFailureReason(localizer, "Document repository", ex.Message);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkDocumentSyncFailed(syncFailReason);
            auditSummary = string.Format(localizer["Summary_DocumentSyncFailed"].Value, syncFailReason);
            auditAction = localizer["AuditAction_DocumentsSyncFailed"];
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
            auditAction = localizer["AuditAction_DocumentsSynced"];
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
        IStringLocalizer<ClaimsController> localizer)
    {
        var retried = string.Join(", ", syncResults.Select(result => GetDependencyDisplayName(result.Dependency, localizer)));
        var summaryParts = new List<string>
        {
            string.Format(localizer["Summary_ReconciliationRetried"].Value, retried)
        };

        summaryParts.Add(recoveredDependencies.Count == 0
            ? localizer["Summary_ReconciliationNoRecovered"].Value
            : string.Format(
                localizer["Summary_ReconciliationRecovered"].Value,
                string.Join(", ", recoveredDependencies.Select(dep => GetDependencyDisplayName(dep, localizer))))
        );

        summaryParts.Add(unresolvedDependencies.Count == 0
            ? localizer["Summary_ReconciliationUnresolvedFixed"].Value
            : string.Format(
                localizer["Summary_ReconciliationStillUnresolved"].Value,
                string.Join(", ", unresolvedDependencies.OrderBy(static dependency => dependency, StringComparer.Ordinal).Select(dep => GetDependencyDisplayName(dep, localizer))))
        );

        return string.Join(' ', summaryParts);
    }

    private static string BuildSyncFailureReason(
        IStringLocalizer<ClaimsController> localizer,
        string systemName,
        string exceptionMessage)
    {
        var reason = string.Format(
            localizer["Summary_SyncFailureReason"].Value,
            systemName,
            exceptionMessage);
        return reason.Length > 200 ? reason[..200] : reason;
    }

    private static string GetDependencyDisplayName(string dependency, IStringLocalizer<ClaimsController> localizer)
    {
        return dependency switch
        {
            "policy" => localizer["Dependency_Policy"],
            "payment" => localizer["Dependency_Payment"],
            "documents" => localizer["Dependency_Documents"],
            _ => dependency,
        };
    }

    private sealed record ClaimSyncResult(string Dependency, bool Succeeded);

    private static string BuildNoteAuditSummary(
        string content,
        IStringLocalizer<ClaimsController> localizer)
    {
        const int maxPreviewLength = 80;
        var preview = content.Length <= maxPreviewLength
            ? content
            : $"{content[..maxPreviewLength].TrimEnd()}...";

        return string.Format(localizer["Summary_NoteAuditSuffix"].Value, preview);
    }

    private static string BuildDocumentAuditSummary(
        string fileName,
        string fileType,
        IStringLocalizer<ClaimsController> localizer)
    {
        return string.Format(localizer["Summary_DocumentAuditSuffix"].Value, fileName, fileType);
    }
}