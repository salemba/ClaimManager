namespace ClaimManager.Application.Claims.Dtos;

using ClaimManager.Domain.Audit;
using ClaimManager.Domain.Claims;
using ClaimManager.Domain.ClaimantCommunication;

public sealed record ClaimAuditDto(string Action, string Summary, DateTime PerformedAtUtc, string PerformedByUserId)
{
    public static ClaimAuditDto FromAudit(ClaimAudit audit) =>
        new(audit.Action, audit.Summary, audit.PerformedAtUtc, audit.PerformedByUserId);
}

public sealed record ClaimDataIntegrityIssueDto(string Dependency, string Message)
{
    public static ClaimDataIntegrityIssueDto FromIssue(ClaimDataIntegrityIssue issue) =>
        new(issue.Dependency, issue.Message);
}

public sealed record ClaimReconciliationDto(
    DateTime AttemptedAtUtc,
    IReadOnlyList<string> RetriedDependencies,
    IReadOnlyList<string> RecoveredDependencies,
    IReadOnlyList<string> UnresolvedDependencies,
    string Summary,
    bool IsFullyReconciled)
{
    public static ClaimReconciliationDto FromDetails(ClaimReconciliationDetails details) =>
        new(
            details.AttemptedAtUtc,
            details.RetriedDependencies,
            details.RecoveredDependencies,
            details.UnresolvedDependencies,
            details.Summary,
            details.UnresolvedDependencies.Length == 0);
}

public sealed record ClaimNoteDto(Guid Id, string Content, DateTime CreatedAtUtc, string CreatedByUserId)
{
    public static ClaimNoteDto FromNote(ClaimNote note) =>
        new(note.Id, note.Content, note.CreatedAtUtc, note.CreatedByUserId);
}

public sealed record ClaimCommunicationDto(
    Guid Id,
    string CommunicationType,
    string Channel,
    string Recipient,
    string Subject,
    string Status,
    int AttemptCount,
    DateTime? LastAttemptAtUtc,
    string? DeliveryId,
    string? FailureReason,
    DateTime CreatedAtUtc,
    string CreatedByUserId)
{
    public static ClaimCommunicationDto FromCommunication(ClaimCommunication comm) =>
        new(
            comm.Id,
            comm.CommunicationType,
            comm.Channel,
            comm.Recipient,
            comm.Subject,
            comm.Status,
            comm.AttemptCount,
            comm.LastAttemptAtUtc,
            comm.DeliveryId,
            comm.FailureReason,
            comm.CreatedAtUtc,
            comm.CreatedByUserId);
}

public sealed record ClaimDocumentDto(
    Guid Id,
    string FileName,
    string FileType,
    string? ContentType,
    long FileSizeBytes,
    DateTime UploadedAtUtc,
    string UploadedByUserId,
    string Source)
{
    public static ClaimDocumentDto FromDocument(ClaimDocument document) =>
        new(
            document.Id,
            document.FileName,
            document.FileType,
            document.ContentType,
            document.FileSizeBytes,
            document.UploadedAtUtc,
            document.UploadedByUserId,
            document.Source);
}

public sealed record ClaimSummaryPagedResponseDto(
    IReadOnlyList<ClaimSummaryDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

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
    bool HasDataIntegrityWarning,
    DateTime? PolicySyncedAtUtc,
    DateTime? PaymentSyncedAtUtc,
    DateTime? DocumentSyncedAtUtc)
{
    public static ClaimSummaryDto FromClaim(Claim claim) =>
        new(claim.Id, claim.ClaimNumber, claim.Status, claim.ClaimantName, claim.PolicyNumber, claim.LossDateUtc, claim.CreatedAtUtc, claim.UpdatedAtUtc, claim.BlockerType, claim.BlockerReason, claim.OwnedByUserId, claim.HasDataIntegrityWarning, claim.PolicySyncedAtUtc, claim.PaymentSyncedAtUtc, claim.DocumentSyncedAtUtc);
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
    IReadOnlyList<ClaimDataIntegrityIssueDto> ActiveDataIntegrityIssues,
    ClaimReconciliationDto? Reconciliation,
    string? PolicyHolder,
    string? CoverageType,
    DateOnly? PolicyEffectiveDate,
    DateOnly? PolicyExpirationDate,
    DateTime? PolicySyncedAtUtc,
    string? PaymentReference,
    string? PaymentStatus,
    decimal? PaymentAmount,
    string? PaymentCurrency,
    DateTimeOffset? PaymentSettledAt,
    DateTime? PaymentSyncedAtUtc,
    DateTime? DocumentSyncedAtUtc,
    byte[] RowVersion,
    IReadOnlyList<string> AvailableActions,
    IReadOnlyList<ClaimAuditDto> AuditHistory,
    IReadOnlyList<ClaimNoteDto> Notes,
    IReadOnlyList<ClaimDocumentDto> Documents,
    IReadOnlyList<ClaimCommunicationDto> Communications)
{
    public static ClaimDto FromClaim(
        Claim claim,
        IReadOnlyList<ClaimAuditDto>? auditHistory = null,
        IReadOnlyList<ClaimNoteDto>? notes = null,
        IReadOnlyList<ClaimDocumentDto>? documents = null,
        IReadOnlyList<ClaimCommunicationDto>? communications = null) =>
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
            claim.GetActiveDataIntegrityIssues().Select(ClaimDataIntegrityIssueDto.FromIssue).ToArray(),
            claim.GetLastReconciliationDetails() is { } details ? ClaimReconciliationDto.FromDetails(details) : null,
            claim.PolicyHolder,
            claim.CoverageType,
            claim.PolicyEffectiveDate,
            claim.PolicyExpirationDate,
            claim.PolicySyncedAtUtc,
            claim.PaymentReference,
            claim.PaymentStatus,
            claim.PaymentAmount,
            claim.PaymentCurrency,
            claim.PaymentSettledAt,
            claim.PaymentSyncedAtUtc,
            claim.DocumentSyncedAtUtc,
            claim.RowVersion,
            claim.GetAvailableActions(),
            auditHistory ?? [],
            notes ?? [],
            documents ?? [],
            communications ?? []);
}