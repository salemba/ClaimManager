namespace ClaimManager.Api.Endpoints.Claims;

using ClaimManager.Application.Audit.Commands;
using ClaimManager.Application.Claims.Commands;
using ClaimManager.Application.Claims.Dtos;
using ClaimManager.Application.Claims.Validators;
using ClaimManager.Application.Security;
using ClaimManager.Infrastructure.Integrations.DocumentRepository;
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

        return endpoints;
    }

    private static async Task<Ok<ClaimSummaryDto[]>> GetClaimsAsync(
        ClaimManagerDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var claims = await dbContext.Claims
            .OrderByDescending(claim => claim.UpdatedAtUtc ?? claim.CreatedAtUtc)
            .ThenBy(claim => claim.ClaimNumber)
            .Select(claim => ClaimSummaryDto.FromClaim(claim))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(claims);
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
                .ToArray()));
    }

    private static async Task<Results<Created<ClaimDto>, ValidationProblem, ProblemHttpResult>> CreateClaimAsync(
        CreateClaimCommand command,
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager,
        ClaimManagerDbContext dbContext,
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

            dbContext.Claims.Add(claim);
            dbContext.ClaimAudits.Add(new RecordClaimAuditCommand(
                claim.Id,
                "created",
                "Claim file created with claimant, claim, and loss information.",
                user.Id.ToString(),
                createdAtUtc).ToEntity());

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

            await dbContext.SaveChangesAsync(cancellationToken);
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