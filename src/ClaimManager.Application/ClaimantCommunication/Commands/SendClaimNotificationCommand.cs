namespace ClaimManager.Application.ClaimantCommunication.Commands;

public sealed record SendClaimNotificationCommand(
    Guid ClaimId,
    string CommunicationType,
    string Channel,
    string Recipient,
    string Subject,
    string Body);
