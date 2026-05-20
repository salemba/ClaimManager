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
using System.Reflection;
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
    private static IStringLocalizer GetLocalizer(IStringLocalizerFactory factory) 
        => factory.Create(typeof(ClaimsController));

    public static IEndpointRouteBuilder MapClaimEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/claims")
            .WithTags("Claims")
            .RequireAuthorization(ClaimManagerPolicies.Adjuster);

        group.MapGet("/", GetClaimsAsync);
        group.MapGet("/{id:guid}", GetClaimDetailsAsync);
        group.MapPost("/", CreateClaimAsync);
        group.MapPut("/{id:guid}", UpdateClaimAsync);
        group.MapPost("/{id:guid}/notes", AddClaimNoteAsync);
        group.MapPost("/{id:guid}/documents", UploadClaimDocumentAsync);
        group.MapPost("/{id:guid}/advance", AdvanceClaimWorkflowAsync);
        group.MapPost("/{id:guid}/route-for-approval", RouteClaimForApprovalAsync);
        group.MapPost("/{id:guid}/sync-policy", SyncClaimPolicyDataAsync);
        group.MapPost("/{id:guid}/sync-payment", SyncClaimPaymentDataAsync);
        group.MapPost("/{id:guid}/sync-documents", SyncClaimDocumentDataAsync);
        group.MapPost("/{id:guid}/reconcile", ReconcileClaimStateAsync);
        group.MapPost("/{id:guid}/notifications", SendClaimNotificationAsync);
        group.MapPost("/{id:guid}/notifications/{notificationId:guid}/retry", RetryClaimNotificationAsync);

        return endpoints;
    }

    /// <summary>
    /// Retrieves a paginated list of claims with optional filtering by search term, status, blocker type, and owner.
    /// </summary>
    /// <param name="query">The query parameters including search, status, blocker type, owner filter, page, and page size.</param>
    /// <param name="dbContext">The database context for accessing claim data.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Ok{ClaimSummaryPagedResponseDto}"/> containing the paginated list of claim summaries
    /// with total count and pagination metadata.
    /// </returns>
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
    /// Retrieves the detailed information of a specific claim, including audit history, notes, documents, and communications.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to retrieve.</param>
    /// <param name="localizerFactory">The string localizer factory for retrieving localized messages.</param>
    /// <param name="dbContext">The database context for accessing claim data.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Ok{ClaimDto}"/>: The complete claim details with history, notes, documents, and communications.
    /// - <see cref="ProblemHttpResult"/>: A 404 error if the claim is not found.
    /// </returns>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> GetClaimDetailsAsync(
        Guid id,
        IStringLocalizerFactory localizerFactory,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var localizer = GetLocalizer(localizerFactory);
        var claim = await dbContext.Claims
            .AsSplitQuery()
            .Include(existingClaim => existingClaim.Notes)
            .Include(existingClaim => existingClaim.Documents)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
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
    /// Creates a new claim with claimant information, policy data, and initial payment status synchronization.
    /// </summary>
    /// <param name="command">The command containing claim details (claimant name, email, phone, policy number, loss information).</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizerFactory">The string localizer factory for retrieving localized messages.</param>
    /// <param name="dbContext">The database context for persisting claim data.</param>
    /// <param name="policyClient">The policy system client for retrieving policy information.</param>
    /// <param name="paymentClient">The payment system client for retrieving payment status.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Created{ClaimDto}"/>: The newly created claim.
    /// - <see cref="ValidationProblem"/>: Validation errors if the command is invalid.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails or a 409 error if claim number reservation fails.
    /// </returns>
    private static async Task<Results<Created<ClaimDto>, ValidationProblem, ProblemHttpResult>> CreateClaimAsync(
        CreateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        ClaimManagerDbContext dbContext,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
        CancellationToken cancellationToken)
    {
        var localizer = GetLocalizer(localizerFactory);
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
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        PolicySummary? initialPolicyData = null;
        string? initialSyncFailReason = null;

        try
        {
            initialPolicyData = await policyClient.GetPolicyByNumberAsync(command.PolicyNumber, cancellationToken);
            if (initialPolicyData is null)
            {
                initialSyncFailReason = localizer["PolicyNotFound"];
            }
        }
        catch (Exception ex)
        {
            initialSyncFailReason = localizer["PolicySystemError", ex.Message];
            if (initialSyncFailReason?.Length > 200)
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
                initialPaymentSyncFailReason = localizer["PaymentSystemError", ex.Message];
                if (initialPaymentSyncFailReason?.Length > 200)
                {
                    initialPaymentSyncFailReason = initialPaymentSyncFailReason[..200];
                }
            }

            if (initialPaymentSyncFailReason is not null)
            {
                claim.MarkPaymentSyncFailed(initialPaymentSyncFailReason);
                paymentAuditSummary = localizer["PaymentSyncFailed_Audit", initialPaymentSyncFailReason];
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
                policyAuditSummary = localizer["PolicySyncFailed_Audit", initialSyncFailReason];
                policyAuditAction = "policy-sync-failed";
            }

            dbContext.Claims.Add(claim);
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                "created",
                localizer["ClaimCreated_Audit"],
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
            title: localizer["ClaimCreationConflict_Title"],
            detail: localizer["ClaimCreationConflict_Detail"]);
    }

    /// <summary>
    /// Synchronizes policy data for an existing claim from the policy system.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to synchronize.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizerFactory">The string localizer factory for retrieving localized messages.</param>
    /// <param name="policyClient">The policy system client for retrieving policy information.</param>
    /// <param name="dbContext">The database context for persisting synchronization results.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Ok{ClaimDto}"/>: The updated claim with synchronized policy data.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails, a 404 error if the claim is not found, or a 409 error on concurrency conflict.
    /// </returns>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPolicyDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        IPolicySystemClient policyClient,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var localizer = GetLocalizer(localizerFactory);
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized, 
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
        }

        await ExecutePolicySyncAsync(claim, user.Id.ToString(), policyClient, dbContext, localizerFactory, DateTime.UtcNow, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["ConcurrencyConflict_Title"],
                detail: localizer["ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Synchronizes payment data for an existing claim from the payment system.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to synchronize.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="paymentClient">The payment system client for retrieving payment information.</param>
    /// <param name="dbContext">The database context for persisting synchronization results.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Ok{ClaimDto}"/>: The updated claim with synchronized payment data.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails, a 404 error if the claim is not found, or a 409 error on concurrency conflict.
    /// </returns>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPaymentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        IPaymentSystemClient paymentClient,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        var localizer = GetLocalizer(localizerFactory);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized, 
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
        }

        await ExecutePaymentSyncAsync(claim, user.Id.ToString(), paymentClient, dbContext, localizerFactory, DateTime.UtcNow, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["ConcurrencyConflict_Title"],
                detail: localizer["ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Synchronizes document data for an existing claim from the document repository.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to synchronize.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="documentRepository">The document repository client for retrieving document information.</param>
    /// <param name="dbContext">The database context for persisting synchronization results.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Ok{ClaimDto}"/>: The updated claim with synchronized document data.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails, a 404 error if the claim is not found, or a 409 error on concurrency conflict.
    /// </returns>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimDocumentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        var localizer = GetLocalizer(localizerFactory);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized, 
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
        }

        await ExecuteDocumentSyncAsync(claim, user.Id.ToString(), documentRepository, dbContext, localizerFactory, DateTime.UtcNow, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["ConcurrencyConflict_Title"],
                detail: localizer["ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Reconciles the state of a claim by attempting to synchronize all dependent data sources (policy, payment, documents).
    /// </summary>
    /// <param name="id">The unique identifier of the claim to reconcile.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="policyClient">The policy system client for retrieving policy information.</param>
    /// <param name="paymentClient">The payment system client for retrieving payment information.</param>
    /// <param name="documentRepository">The document repository client for retrieving document information.</param>
    /// <param name="dbContext">The database context for persisting reconciliation results.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Ok{ClaimDto}"/>: The updated claim with reconciliation audit information.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails, a 404 error if the claim is not found, or a 409 error on concurrency conflict.
    /// </returns>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> ReconcileClaimStateAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        var localizer = GetLocalizer(localizerFactory);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized, 
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
        }

        var attemptedAtUtc = DateTime.UtcNow;
        var unresolvedBefore = claim.GetActiveDataIntegrityIssues()
            .Select(issue => issue.Dependency)
            .ToHashSet(StringComparer.Ordinal);

        var syncResults = new[]
        {
            await ExecutePolicySyncAsync(claim, user.Id.ToString(), policyClient, dbContext, localizerFactory, attemptedAtUtc, cancellationToken),
            await ExecutePaymentSyncAsync(claim, user.Id.ToString(), paymentClient, dbContext, localizerFactory, attemptedAtUtc, cancellationToken),
            await ExecuteDocumentSyncAsync(claim, user.Id.ToString(), documentRepository, dbContext, localizerFactory, attemptedAtUtc, cancellationToken),
        };

        var unresolvedAfter = claim.GetActiveDataIntegrityIssues()
            .Select(issue => issue.Dependency)
            .ToHashSet(StringComparer.Ordinal);
        var recoveredDependencies = unresolvedBefore
            .Where(dependency => !unresolvedAfter.Contains(dependency))
            .OrderBy(static dependency => dependency, StringComparer.Ordinal)
            .ToArray();
        var reconciliationSummary = BuildReconciliationSummary(syncResults, recoveredDependencies, unresolvedAfter, localizerFactory);

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
                title: localizer["ConcurrencyConflict_Title"],
                detail: localizer["ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Updates the core details of an existing claim (claimant information, policy number, loss information).
    /// </summary>
    /// <param name="id">The unique identifier of the claim to update.</param>
    /// <param name="command">The command containing the updated claim details.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="dbContext">The database context for persisting claim updates.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Ok{ClaimDto}"/>: The updated claim.
    /// - <see cref="ValidationProblem"/>: Validation errors if the command is invalid.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails, a 404 error if the claim is not found, or a 409 error on concurrency conflict.
    /// </returns>
    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> UpdateClaimAsync(
        Guid id,
        UpdateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var localizer = GetLocalizer(localizerFactory);
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
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
        }

        dbContext.Entry(claim).Property(c => c.RowVersion).OriginalValue = command.RowVersion;

        var auditSummary = BuildUpdateSummary(claim, command, localizerFactory);
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
                    title: localizer["ConcurrencyConflict_Title"],
                    detail: localizer["ConcurrencyConflict_Detail"]);
            }
        }

        var auditHistory = await GetAuditHistoryAsync(dbContext, claim.Id, cancellationToken);
        return TypedResults.Ok(ClaimDto.FromClaim(claim, auditHistory));
    }

    /// <summary>
    /// Adds a new note to an existing claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to add a note to.</param>
    /// <param name="command">The command containing the note content.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="dbContext">The database context for persisting the new note.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Created{ClaimNoteDto}"/>: The newly created claim note.
    /// - <see cref="ValidationProblem"/>: Validation errors if the command is invalid.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails or a 404 error if the claim is not found.
    /// </returns>
    private static async Task<Results<Created<ClaimNoteDto>, ValidationProblem, ProblemHttpResult>> AddClaimNoteAsync(
        Guid id,
        AddClaimNoteCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var localizer = GetLocalizer(localizerFactory);
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
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
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
    /// Uploads a document to an existing claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to upload a document to.</param>
    /// <param name="file">The document file to upload.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="documentRepository">The document repository for storing the uploaded document.</param>
    /// <param name="dbContext">The database context for persisting document metadata.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Created{ClaimDocumentDto}"/>: The newly uploaded document metadata.
    /// - <see cref="ValidationProblem"/>: Validation errors if the file is invalid.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails or a 404 error if the claim is not found.
    /// </returns>
    private static async Task<Results<Created<ClaimDocumentDto>, ValidationProblem, ProblemHttpResult>> UploadClaimDocumentAsync(
        Guid id,
        IFormFile? file,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var localizer = GetLocalizer(localizerFactory);
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized, 
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
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
    /// Advances a claim through its workflow to the next state.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to advance.</param>
    /// <param name="command">The command containing the row version for concurrency control.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="dbContext">The database context for persisting workflow changes.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Ok{ClaimDto}"/>: The updated claim with new workflow state.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails, a 404 error if the claim is not found, a 409 error on workflow transition error or concurrency conflict.
    /// </returns>
    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> AdvanceClaimWorkflowAsync(
        Guid id,
        AdvanceClaimWorkflowCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };
        var localizer = GetLocalizer(localizerFactory);
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized, 
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
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
                title: localizer["InvalidWorkflowTransition_Title"], 
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
                title: localizer["ConcurrencyConflict_Title"],
                detail: localizer["ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Routes a claim for payment approval with a rationale.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to route.</param>
    /// <param name="command">The command containing the rationale and row version for concurrency control.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="dbContext">The database context for persisting routing changes.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Ok{ClaimDto}"/>: The updated claim with approval route status.
    /// - <see cref="ValidationProblem"/>: Validation errors if the command is invalid.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails, a 404 error if the claim is not found, a 409 error on workflow transition error or concurrency conflict.
    /// </returns>
    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> RouteClaimForApprovalAsync(
        Guid id,
        RouteClaimForApprovalCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };
        var localizer = GetLocalizer(localizerFactory);

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
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
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
                title: localizer["InvalidWorkflowTransition_Title"], 
                detail: ex.Message);
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            "routed-for-approval",
            localizer["RouteForApproval_Audit", claim.BlockerReason],
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
                title: localizer["ConcurrencyConflict_Title"],
                detail: localizer["ConcurrencyConflict_Detail"]);
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    /// <summary>
    /// Sends a notification to a claimant about their claim via email, SMS, or other channels.
    /// </summary>
    /// <param name="id">The unique identifier of the claim to send a notification for.</param>
    /// <param name="command">The command containing notification details (type, channel, recipient, subject, body).</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="messagingClient">The messaging client for sending the notification.</param>
    /// <param name="dbContext">The database context for persisting the notification record.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Created{ClaimCommunicationDto}"/>: The created communication record with delivery status.
    /// - <see cref="ValidationProblem"/>: Validation errors if the command is invalid.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails or a 404 error if the claim is not found.
    /// </returns>
    private static async Task<Results<Created<ClaimCommunicationDto>, ValidationProblem, ProblemHttpResult>> SendClaimNotificationAsync(
        Guid id,
        SendClaimNotificationCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var localizer = GetLocalizer(localizerFactory);
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
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var claimExists = await dbContext.Claims.AnyAsync(c => c.Id == id, cancellationToken);
        if (!claimExists)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["ClaimNotFound_Title"], 
                detail: localizer["ClaimNotFound_Detail"]);
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
            deliveryResult = new MessageDeliveryResult(false, null, localizer["MessagingClientError", ex.Message]);
        }

        if (deliveryResult.Success && deliveryResult.DeliveryId is not null)
        {
            communication.RecordSent(deliveryResult.DeliveryId, attemptAtUtc);
        }
        else
        {
            communication.RecordFailed(deliveryResult.FailureReason ?? localizer["DeliveryFailedNoReason"], attemptAtUtc);
        }

        var auditSummary = communication.Status == "sent"
            ? localizer["NotificationSent_Audit", communication.CommunicationType, communication.Channel, communication.Recipient, communication.DeliveryId]
            : localizer["NotificationFailed_Audit", communication.CommunicationType, communication.Channel, communication.Recipient, communication.FailureReason];

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
    /// Retries sending a previously failed notification for a claim.
    /// </summary>
    /// <param name="id">The unique identifier of the claim.</param>
    /// <param name="notificationId">The unique identifier of the notification to retry.</param>
    /// <param name="principal">The current user's principal for authentication.</param>
    /// <param name="userManager">The user manager for retrieving the current user.</param>
    /// <param name="localizer">The string localizer for retrieving localized messages.</param>
    /// <param name="messagingClient">The messaging client for resending the notification.</param>
    /// <param name="dbContext">The database context for persisting retry results.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="Results"/> containing:
    /// - <see cref="Ok{ClaimCommunicationDto}"/>: The updated communication record with new delivery status.
    /// - <see cref="ProblemHttpResult"/>: A 401 error if authentication fails, a 404 error if the notification is not found, or a 409 error if the notification is not eligible for retry or on concurrency conflict.
    /// </returns>
    private static async Task<Results<Ok<ClaimCommunicationDto>, ProblemHttpResult>> RetryClaimNotificationAsync(
        Guid id,
        Guid notificationId,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IStringLocalizerFactory localizerFactory,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        var localizer = GetLocalizer(localizerFactory);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized, 
                title: localizer["AuthenticationRequired_Title"], 
                detail: localizer["AuthenticationRequired_Detail"]);
        }

        var communication = await dbContext.ClaimCommunications
            .SingleOrDefaultAsync(c => c.Id == notificationId && c.ClaimId == id, cancellationToken);

        if (communication is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound, 
                title: localizer["NotificationNotFound_Title"], 
                detail: localizer["NotificationNotFound_Detail"]);
        }

        if (!communication.IsRetryEligible())
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: localizer["RetryNotAllowed_Title"],
                detail: localizer["RetryNotAllowed_Detail", communication.Status]);
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
            deliveryResult = new MessageDeliveryResult(false, null, localizer["MessagingClientError", ex.Message]);
        }

        if (deliveryResult.Success && deliveryResult.DeliveryId is not null)
        {
            communication.RecordSent(deliveryResult.DeliveryId, attemptAtUtc);
        }
        else
        {
            communication.RecordFailed(deliveryResult.FailureReason ?? localizer["DeliveryFailedNoReason"], attemptAtUtc);
        }

        var auditSummary = communication.Status == "sent"
            ? localizer["NotificationRetrySucceeded_Audit", communication.CommunicationType, communication.Channel, communication.Recipient, communication.DeliveryId]
            : localizer["NotificationRetryFailed_Audit", communication.CommunicationType, communication.Channel, communication.Recipient, communication.FailureReason];

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

    private static string BuildUpdateSummary(ClaimEntity claim, UpdateClaimCommand command, IStringLocalizerFactory localizerFactory)
    {
        var changes = new List<string>();

        var localizer = GetLocalizer(localizerFactory);
        AddChange(changes, "Claimant name", claim.ClaimantName, command.ClaimantName, localizer);
        AddChange(changes, "Claimant email", claim.ClaimantEmail, command.ClaimantEmail, localizer);
        AddChange(changes, "Claimant phone", claim.ClaimantPhone, command.ClaimantPhone, localizer);
        AddChange(changes, "Policy number", claim.PolicyNumber, command.PolicyNumber, localizer);
        AddChange(changes, "Loss type", claim.LossType, command.LossType, localizer);
        AddChange(changes, "Loss description", claim.LossDescription, command.LossDescription, localizer);

        if (claim.LossDateUtc != command.LossDateUtc)
        {
            changes.Add(localizer["LossDateUpdated_Audit", claim.LossDateUtc, command.LossDateUtc]);
        }

        return changes.Count == 0
            ? localizer["ClaimUpdated_Audit"]
            : string.Join(' ', changes);
    }

    private static void AddChange(List<string> changes, string label, string currentValue, string newValue, IStringLocalizer localizer)
    {
        var normalizedNewValue = newValue.Trim();
        if (!string.Equals(currentValue, normalizedNewValue, StringComparison.Ordinal))
        {
            changes.Add(localizer["FieldUpdated_Audit", label, currentValue, normalizedNewValue]);
        }
    }

    private static async Task<ClaimSyncResult> ExecutePolicySyncAsync(
        ClaimEntity claim,
        string userId,
        IPolicySystemClient policyClient,
        ClaimManagerDbContext dbContext,
        IStringLocalizerFactory localizerFactory,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
         var localizer = GetLocalizer(localizerFactory);
        PolicySummary? policyData = null;
        string? syncFailReason = null;

        try
        {           
            policyData = await policyClient.GetPolicyByNumberAsync(claim.PolicyNumber, cancellationToken);
            if (policyData is null)
            {
                syncFailReason = localizer["PolicyNotFound"];
            }
        }
        catch (Exception ex)
        {
            syncFailReason = BuildSyncFailureReason("Policy", ex.Message, localizerFactory);
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
            auditSummary = localizer["PolicyDataSyncFailed_Audit", syncFailReason];
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
        IStringLocalizerFactory localizerFactory,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var localizer = GetLocalizer(localizerFactory);
        PaymentRecord? paymentRecord = null;
        string? syncFailReason = null;        
        try
        {
            paymentRecord = await paymentClient.GetPaymentStatusByClaimAsync(claim.ClaimNumber, cancellationToken);
        }
        catch (Exception ex)
        {
            syncFailReason = BuildSyncFailureReason("Payment", ex.Message, localizerFactory);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkPaymentSyncFailed(syncFailReason);
            auditSummary = localizer["PaymentDataSyncFailed_Audit", syncFailReason];
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
        IStringLocalizerFactory localizerFactory,
        DateTime syncedAtUtc,
        CancellationToken cancellationToken)
    {
        var localizer = GetLocalizer(localizerFactory);
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
            syncFailReason = BuildSyncFailureReason("Document Repository", ex.Message, localizerFactory);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkDocumentSyncFailed(syncFailReason);
            auditSummary = localizer["DocumentDataSyncFailed_Audit", syncFailReason];
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
        IStringLocalizerFactory localizerFactory)
    {
        var localizer = GetLocalizer(localizerFactory);
        var retried = string.Join(", ", syncResults.Select(result => GetDependencyDisplayName(result.Dependency, localizer)));
        var summaryParts = new List<string>
        {
            localizer["ClaimReconciled_Audit", retried, "", ""]
        };

        summaryParts.Add(recoveredDependencies.Count == 0
            ? localizer["ReconciliationNoRecovery_Audit"]
            : localizer["ReconciliationRecovered_Audit", string.Join(", ", recoveredDependencies.Select(d => GetDependencyDisplayName(d, localizer)))]
        );

        summaryParts.Add(unresolvedDependencies.Count == 0
            ? localizer["ReconciliationAllResolved_Audit"]
            : localizer["ReconciliationStillUnresolved_Audit", string.Join(", ", unresolvedDependencies.OrderBy(static dependency => dependency, StringComparer.Ordinal).Select(d => GetDependencyDisplayName(d, localizer)))]
        );

        return string.Join(' ', summaryParts);
    }

    private static string BuildSyncFailureReason(string systemName, string exceptionMessage, IStringLocalizerFactory localizerFactory)
    {
        var localizer = GetLocalizer(localizerFactory);
        var messageTemplate = systemName switch
        {
            "Policy" => localizer["PolicySystemError"],
            "Payment" => localizer["PaymentSystemError"],
            "Document Repository" => localizer["DocumentRepositoryError"],
            _ => $"{systemName} was unreachable or returned an unexpected error: {{0}}"
        };

        var reason = string.Format(messageTemplate ?? "", exceptionMessage);
        return reason.Length > 200 ? reason[..200] : reason;
    }

    private static string GetDependencyDisplayName(string dependency, IStringLocalizer localizer)
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

    private static string BuildNoteAuditSummary(string content, IStringLocalizer localizer)
    {
        const int maxPreviewLength = 80;
        var preview = content.Length <= maxPreviewLength
            ? content
            : $"{content[..maxPreviewLength].TrimEnd()}...";

        return $"{localizer["NoteAdded_Audit_Prefix"]}{preview}{localizer["NoteAdded_Audit_Suffix"]}";
    }

    private static string BuildDocumentAuditSummary(string fileName, string fileType, IStringLocalizer localizer)
    {
        return localizer["DocumentUploaded_Audit", fileName, fileType];
    }
}