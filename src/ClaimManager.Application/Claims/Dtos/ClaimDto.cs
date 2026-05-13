namespace ClaimManager.Application.Claims.Dtos;

using ClaimManager.Domain.Audit;
using ClaimManager.Domain.Claims;

public sealed record ClaimAuditDto(string Action, string Summary, DateTime PerformedAtUtc, string PerformedByUserId)
{
    public static ClaimAuditDto FromAudit(ClaimAudit audit) =>
        new(audit.Action, audit.Summary, audit.PerformedAtUtc, audit.PerformedByUserId);
}

public sealed record ClaimNoteDto(Guid Id, string Content, DateTime CreatedAtUtc, string CreatedByUserId)
{
    public static ClaimNoteDto FromNote(ClaimNote note) =>
        new(note.Id, note.Content, note.CreatedAtUtc, note.CreatedByUserId);
}

public sealed record ClaimDocumentDto(
    Guid Id,
    string FileName,
    string FileType,
    string? ContentType,
    long FileSizeBytes,
    DateTime UploadedAtUtc,
    string UploadedByUserId)
{
    public static ClaimDocumentDto FromDocument(ClaimDocument document) =>
        new(
            document.Id,
            document.FileName,
            document.FileType,
            document.ContentType,
            document.FileSizeBytes,
            document.UploadedAtUtc,
            document.UploadedByUserId);
}

public sealed record ClaimSummaryDto(
    Guid Id,
    string ClaimNumber,
    string Status,
    string ClaimantName,
    string PolicyNumber,
    DateTime LossDateUtc,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string? BlockerType,
    string? BlockerReason,
    string? OwnedByUserId,
    bool HasDataIntegrityWarning)
{
    public static ClaimSummaryDto FromClaim(Claim claim) =>
        new(claim.Id, claim.ClaimNumber, claim.Status, claim.ClaimantName, claim.PolicyNumber, claim.LossDateUtc, claim.CreatedAtUtc, claim.UpdatedAtUtc, claim.BlockerType, claim.BlockerReason, claim.OwnedByUserId, claim.HasDataIntegrityWarning);
}

public sealed record ClaimDto(
    Guid Id,
    string ClaimNumber,
    string Status,
    string ClaimantName,
    string ClaimantEmail,
    string ClaimantPhone,
    string PolicyNumber,
    DateTime LossDateUtc,
    string LossType,
    string LossDescription,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    string CreatedByUserId,
    string? UpdatedByUserId,
    string? BlockerType,
    string? BlockerReason,
    string? OwnedByUserId,
    string? NextExpectedAction,
    bool HasDataIntegrityWarning,
    string? DataIntegrityWarningMessage,
    IReadOnlyList<ClaimAuditDto> AuditHistory,
    IReadOnlyList<ClaimNoteDto> Notes,
    IReadOnlyList<ClaimDocumentDto> Documents)
{
    public static ClaimDto FromClaim(
        Claim claim,
        IReadOnlyList<ClaimAuditDto>? auditHistory = null,
        IReadOnlyList<ClaimNoteDto>? notes = null,
        IReadOnlyList<ClaimDocumentDto>? documents = null) =>
        new(
            claim.Id,
            claim.ClaimNumber,
            claim.Status,
            claim.ClaimantName,
            claim.ClaimantEmail,
            claim.ClaimantPhone,
            claim.PolicyNumber,
            claim.LossDateUtc,
            claim.LossType,
            claim.LossDescription,
            claim.CreatedAtUtc,
            claim.UpdatedAtUtc,
            claim.CreatedByUserId,
            claim.UpdatedByUserId,
            claim.BlockerType,
            claim.BlockerReason,
            claim.OwnedByUserId,
            claim.NextExpectedAction,
            claim.HasDataIntegrityWarning,
            claim.DataIntegrityWarningMessage,
            auditHistory ?? [],
            notes ?? [],
            documents ?? []);
}