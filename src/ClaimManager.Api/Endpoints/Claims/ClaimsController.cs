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
        group.MapPost("/{id:guid}/intervene", InterveneOnClaimAsync)
            .RequireAuthorization(ClaimManagerPolicies.Supervisor);
        group.MapPost("/{id:guid}/notifications", SendClaimNotificationAsync);
        group.MapPost("/{id:guid}/notifications/{notificationId:guid}/retry", RetryClaimNotificationAsync);

        return endpoints;
    }

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

    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> GetClaimDetailsAsync(
        Guid id,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var claim = await dbContext.Claims
            .AsSplitQuery()
            .Include(existingClaim => existingClaim.Notes)
            .Include(existingClaim => existingClaim.Documents)
            .SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
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

    private static async Task<Results<Created<ClaimDto>, ValidationProblem, ProblemHttpResult>> CreateClaimAsync(
        CreateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
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
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        PolicySummary? initialPolicyData = null;
        string? initialSyncFailReason = null;

        try
        {
            initialPolicyData = await policyClient.GetPolicyByNumberAsync(command.PolicyNumber, cancellationToken);
            if (initialPolicyData is null)
            {
                initialSyncFailReason = "Policy not found for the recorded policy number.";
            }
        }
        catch (Exception ex)
        {
            initialSyncFailReason = $"Policy system was unreachable or returned an unexpected error: {ex.Message}";
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
                initialPaymentSyncFailReason = $"Payment system was unreachable or returned an unexpected error: {ex.Message}";
                if (initialPaymentSyncFailReason.Length > 200)
                {
                    initialPaymentSyncFailReason = initialPaymentSyncFailReason[..200];
                }
            }

            if (initialPaymentSyncFailReason is not null)
            {
                claim.MarkPaymentSyncFailed(initialPaymentSyncFailReason);
                paymentAuditSummary = $"Initial payment data sync failed: {initialPaymentSyncFailReason}";
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
                policyAuditSummary = $"Initial policy data sync failed: {initialSyncFailReason}";
                policyAuditAction = "policy-sync-failed";
            }

            dbContext.Claims.Add(claim);
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                "created",
                "Claim file created with claimant, claim, and loss information.",
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
            title: "Claim creation conflict",
            detail: "The claim number could not be reserved. Please retry the request.");
    }

    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPolicyDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPolicySystemClient policyClient,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
        }

        await ExecutePolicySyncAsync(claim, user.Id.ToString(), policyClient, dbContext, DateTime.UtcNow, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Concurrency conflict",
                detail: "The claim has been modified by another user. Please reload the claim and try again.");
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimPaymentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPaymentSystemClient paymentClient,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
        }

        await ExecutePaymentSyncAsync(claim, user.Id.ToString(), paymentClient, dbContext, DateTime.UtcNow, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Concurrency conflict",
                detail: "The claim has been modified by another user. Please reload the claim and try again.");
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> SyncClaimDocumentDataAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
        }

        await ExecuteDocumentSyncAsync(claim, user.Id.ToString(), documentRepository, dbContext, DateTime.UtcNow, cancellationToken);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Concurrency conflict",
                detail: "The claim has been modified by another user. Please reload the claim and try again.");
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> ReconcileClaimStateAsync(
        Guid id,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IPolicySystemClient policyClient,
        IPaymentSystemClient paymentClient,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
        }

        var attemptedAtUtc = DateTime.UtcNow;
        var unresolvedBefore = claim.GetActiveDataIntegrityIssues()
            .Select(issue => issue.Dependency)
            .ToHashSet(StringComparer.Ordinal);

        var syncResults = new[]
        {
            await ExecutePolicySyncAsync(claim, user.Id.ToString(), policyClient, dbContext, attemptedAtUtc, cancellationToken),
            await ExecutePaymentSyncAsync(claim, user.Id.ToString(), paymentClient, dbContext, attemptedAtUtc, cancellationToken),
            await ExecuteDocumentSyncAsync(claim, user.Id.ToString(), documentRepository, dbContext, attemptedAtUtc, cancellationToken),
        };

        var unresolvedAfter = claim.GetActiveDataIntegrityIssues()
            .Select(issue => issue.Dependency)
            .ToHashSet(StringComparer.Ordinal);
        var recoveredDependencies = unresolvedBefore
            .Where(dependency => !unresolvedAfter.Contains(dependency))
            .OrderBy(static dependency => dependency, StringComparer.Ordinal)
            .ToArray();
        var reconciliationSummary = BuildReconciliationSummary(syncResults, recoveredDependencies, unresolvedAfter);

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
                title: "Concurrency conflict",
                detail: "The claim has been modified by another user. Please reload the claim and try again.");
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> UpdateClaimAsync(
        Guid id,
        UpdateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
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
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
        }

        dbContext.Entry(claim).Property(c => c.RowVersion).OriginalValue = command.RowVersion;

        var auditSummary = BuildUpdateSummary(claim, command);
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
                    title: "Concurrency conflict",
                    detail: "The claim has been modified by another user. Please reload the claim and try again.");
            }
        }

        var auditHistory = await GetAuditHistoryAsync(dbContext, claim.Id, cancellationToken);
        return TypedResults.Ok(ClaimDto.FromClaim(claim, auditHistory));
    }

    private static async Task<Results<Created<ClaimNoteDto>, ValidationProblem, ProblemHttpResult>> AddClaimNoteAsync(
        Guid id,
        AddClaimNoteCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
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
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
        }

        var createdAtUtc = DateTime.UtcNow;
        var note = claim.AddNote(command.Content, user.Id.ToString(), createdAtUtc);

        dbContext.ClaimNotes.Add(note);
        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            "note-added",
            BuildNoteAuditSummary(note.Content),
            user.Id.ToString(),
            createdAtUtc).ToEntity());

        await dbContext.SaveChangesAsync(cancellationToken);

        return TypedResults.Created($"/api/claims/{claim.Id}/notes/{note.Id}", ClaimNoteDto.FromNote(note));
    }

    private static async Task<Results<Created<ClaimDocumentDto>, ValidationProblem, ProblemHttpResult>> UploadClaimDocumentAsync(
        Guid id,
        IFormFile? file,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IDocumentRepository documentRepository,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
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
                BuildDocumentAuditSummary(document.FileName, document.FileType),
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

    private static async Task<Results<Ok<ClaimDto>, ProblemHttpResult>> AdvanceClaimWorkflowAsync(
        Guid id,
        AdvanceClaimWorkflowCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
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
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Invalid workflow transition", detail: ex.Message);
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
                title: "Concurrency conflict",
                detail: "The claim has been modified by another user. Please reload the claim and try again.");
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> RouteClaimForApprovalAsync(
        Guid id,
        RouteClaimForApprovalCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
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
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
        }

        dbContext.Entry(claim).Property(c => c.RowVersion).OriginalValue = command.RowVersion;
        var routedAtUtc = DateTime.UtcNow;

        try
        {
            claim.RouteForPaymentApproval(command.Rationale, user.Id.ToString(), routedAtUtc);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Invalid workflow transition", detail: ex.Message);
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            "routed-for-approval",
            $"Claim routed for payment approval. Rationale: {claim.BlockerReason}",
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
                title: "Concurrency conflict",
                detail: "The claim has been modified by another user. Please reload the claim and try again.");
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    private static async Task<Results<Ok<ClaimDto>, ValidationProblem, ProblemHttpResult>> InterveneOnClaimAsync(
        Guid id,
        InterveneOnClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        command = command with { Id = id };

        var validator = new InterveneOnClaimCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);
        if (!validationResult.IsValid)
        {
            return TypedResults.ValidationProblem(ToValidationDictionary(validationResult));
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claim = await dbContext.Claims.SingleOrDefaultAsync(existingClaim => existingClaim.Id == id, cancellationToken);
        if (claim is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
        }

        dbContext.Entry(claim).Property(c => c.RowVersion).OriginalValue = command.RowVersion;
        var intervenedAtUtc = DateTime.UtcNow;

        try
        {
            claim.Intervene(command.NewOwnerId, command.TargetStatus, user.Id.ToString(), intervenedAtUtc);
        }
        catch (InvalidOperationException ex)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Intervention criteria not met", detail: ex.Message);
        }

        dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
            claim.Id,
            "supervisor-intervention",
            $"Supervisor intervention performed. New owner: {command.NewOwnerId}, New status: {command.TargetStatus}",
            user.Id.ToString(),
            intervenedAtUtc).ToEntity());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Concurrency conflict",
                detail: "The claim has been modified by another user. Please reload the claim and try again.");
        }

        return TypedResults.Ok(await BuildClaimDtoAsync(dbContext, claim, cancellationToken));
    }

    private static async Task<Results<Created<ClaimCommunicationDto>, ValidationProblem, ProblemHttpResult>> SendClaimNotificationAsync(
        Guid id,
        SendClaimNotificationCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
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
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var claimExists = await dbContext.Claims.AnyAsync(c => c.Id == id, cancellationToken);
        if (!claimExists)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Claim not found", detail: "The requested claim could not be found.");
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
            ? $"Outbound {communication.CommunicationType} notification sent via {communication.Channel} to {communication.Recipient}. Delivery ID: {communication.DeliveryId}."
            : $"Outbound {communication.CommunicationType} notification failed via {communication.Channel} to {communication.Recipient}. Reason: {communication.FailureReason}";

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

    private static async Task<Results<Ok<ClaimCommunicationDto>, ProblemHttpResult>> RetryClaimNotificationAsync(
        Guid id,
        Guid notificationId,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        IMessagingClient messagingClient,
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "The current request does not have a valid authenticated user.");
        }

        var communication = await dbContext.ClaimCommunications
            .SingleOrDefaultAsync(c => c.Id == notificationId && c.ClaimId == id, cancellationToken);

        if (communication is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Notification not found", detail: "The requested notification could not be found for this claim.");
        }

        if (!communication.IsRetryEligible())
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Retry not allowed",
                detail: $"Cannot retry a notification in '{communication.Status}' state. Only failed notifications can be retried.");
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
            ? $"Retry of {communication.CommunicationType} notification succeeded via {communication.Channel} to {communication.Recipient}. Delivery ID: {communication.DeliveryId}."
            : $"Retry of {communication.CommunicationType} notification failed via {communication.Channel} to {communication.Recipient}. Reason: {communication.FailureReason}";

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
        CancellationToken cancellationToken)
    {
        PolicySummary? policyData = null;
        string? syncFailReason = null;

        try
        {
            policyData = await policyClient.GetPolicyByNumberAsync(claim.PolicyNumber, cancellationToken);
            if (policyData is null)
            {
                syncFailReason = "Policy not found for the recorded policy number.";
            }
        }
        catch (Exception ex)
        {
            syncFailReason = BuildSyncFailureReason("Policy system", ex.Message);
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
            auditSummary = $"Policy data synchronization failed: {syncFailReason}";
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
            syncFailReason = BuildSyncFailureReason("Payment system", ex.Message);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkPaymentSyncFailed(syncFailReason);
            auditSummary = $"Payment data synchronization failed: {syncFailReason}";
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
            syncFailReason = BuildSyncFailureReason("Document repository", ex.Message);
        }

        string auditAction;
        string auditSummary;

        if (syncFailReason is not null)
        {
            claim.MarkDocumentSyncFailed(syncFailReason);
            auditSummary = $"Document data synchronization failed: {syncFailReason}";
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
        IReadOnlySet<string> unresolvedDependencies)
    {
        var retried = string.Join(", ", syncResults.Select(result => GetDependencyDisplayName(result.Dependency)));
        var summaryParts = new List<string>
        {
            $"Reconciliation retried {retried}."
        };

        summaryParts.Add(recoveredDependencies.Count == 0
            ? "No previously unresolved dependencies were recovered during this attempt."
            : $"Recovered: {string.Join(", ", recoveredDependencies.Select(GetDependencyDisplayName))}."
        );

        summaryParts.Add(unresolvedDependencies.Count == 0
            ? "All claim integration dependencies are now aligned."
            : $"Still unresolved: {string.Join(", ", unresolvedDependencies.OrderBy(static dependency => dependency, StringComparer.Ordinal).Select(GetDependencyDisplayName))}."
        );

        return string.Join(' ', summaryParts);
    }

    private static string BuildSyncFailureReason(string systemName, string exceptionMessage)
    {
        var reason = $"{systemName} was unreachable or returned an unexpected error: {exceptionMessage}";
        return reason.Length > 200 ? reason[..200] : reason;
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

    private static string BuildNoteAuditSummary(string content)
    {
        const int maxPreviewLength = 80;
        var preview = content.Length <= maxPreviewLength
            ? content
            : $"{content[..maxPreviewLength].TrimEnd()}...";

        return $"Claim note added: '{preview}'.";
    }

    private static string BuildDocumentAuditSummary(string fileName, string fileType)
    {
        return $"Document uploaded: {fileName} ({fileType}).";
    }
}