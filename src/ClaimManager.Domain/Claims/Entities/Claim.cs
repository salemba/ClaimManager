namespace ClaimManager.Domain.Claims.Entities;

using System;
using System.Collections.Generic;
using ClaimManager.Domain.Claims.Enums;

public sealed class Claim
{
    private static readonly ClaimStatus[] _workflowOrder =
    [
        ClaimStatus.Received,
        ClaimStatus.UnderReview,
        ClaimStatus.PendingDocs,
        ClaimStatus.ExpertAssigned,
        ClaimStatus.Validated,
        ClaimStatus.Closed
    ];

    public Guid Id { get; private set; }
    public string AdjusterId { get; private set; }
    public ClaimStatus Status { get; private set; }
    public decimal Amount { get; private set; }
    public DateTime? BlockedSince { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public string ClaimNumber { get; private set; }
    public string ClaimantName { get; private set; }
    public string ClaimantEmail { get; private set; }
    public string ClaimantPhone { get; private set; }
    public string PolicyNumber { get; private set; }
    public DateTime LossDateUtc { get; private set; }
    public string LossType { get; private set; }
    public string LossDescription { get; private set; }
    public string CreatedByUserId { get; private set; }

    // Private constructor for immutability
    private Claim() { }

    public static Claim Create(
        string claimNumber,
        string claimantName,
        string claimantEmail,
        string claimantPhone,
        string policyNumber,
        DateTime lossDateUtc,
        string lossType,
        string lossDescription,
        string createdByUserId,
        string adjusterId,
        decimal amount,
        DateTime createdAtUtc)
    {
        return new Claim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = claimNumber,
            ClaimantName = claimantName,
            ClaimantEmail = claimantEmail,
            ClaimantPhone = claimantPhone,
            PolicyNumber = policyNumber,
            LossDateUtc = lossDateUtc,
            LossType = lossType,
            LossDescription = lossDescription,
            CreatedByUserId = createdByUserId,
            AdjusterId = adjusterId,
            Amount = amount,
            Status = ClaimStatus.Received,
            CreatedAtUtc = createdAtUtc
        };
    }

    public (bool Allowed, List<string> Reasons) CanForceReassign(DateTime now)
    {
        var reasons = new List<string>();

        if (BlockedSince.HasValue && (now - BlockedSince.Value).TotalHours > 48)
        {
            reasons.Add("Claim has been blocked for more than 48 hours.");
        }

        if (Amount > 10000m)
        {
            reasons.Add("Claim amount is over €10,000.");
        }

        return (reasons.Count > 0, reasons);
    }

    public ClaimStatus GetNextStatus()
    {
        if (Status == ClaimStatus.Closed)
        {
            throw new InvalidOperationException("No upper state possible for a closed claim.");
        }

        var currentIndex = Array.IndexOf(_workflowOrder, Status);
        if (currentIndex < 0 || currentIndex >= _workflowOrder.Length - 1)
        {
            // This should not happen if the Closed status check is done correctly.
            throw new InvalidOperationException($"Cannot determine the next status for '{Status}'.");
        }

        return _workflowOrder[currentIndex + 1];
    }

    public Claim With(string newAdjusterId, ClaimStatus newStatus)
    {
        return new Claim
        {
            Id = this.Id,
            AdjusterId = newAdjusterId,
            Status = newStatus,
            Amount = this.Amount,
            BlockedSince = null, // Unblock the claim after reassigning
            CreatedAtUtc = this.CreatedAtUtc,
            ClaimNumber = this.ClaimNumber,
            ClaimantName = this.ClaimantName,
            ClaimantEmail = this.ClaimantEmail,
            ClaimantPhone = this.ClaimantPhone,
            PolicyNumber = this.PolicyNumber,
            LossDateUtc = this.LossDateUtc,
            LossType = this.LossType,
            LossDescription = this.LossDescription,
            CreatedByUserId = this.CreatedByUserId,
        };
    }
}
