namespace ClaimManager.Domain.Claims;

public sealed class Claim
{
    public Guid Id { get; set; }

    public List<ClaimNote> Notes { get; private set; } = [];

    public List<ClaimDocument> Documents { get; private set; } = [];

    public string ClaimNumber { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string ClaimantName { get; set; } = string.Empty;

    public string ClaimantEmail { get; set; } = string.Empty;

    public string ClaimantPhone { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public DateTime LossDateUtc { get; set; }

    public string LossType { get; set; } = string.Empty;

    public string LossDescription { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? UpdatedByUserId { get; set; }

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
        DateTime createdAtUtc)
    {
        return new Claim
        {
            Id = Guid.NewGuid(),
            ClaimNumber = NormalizeRequired(claimNumber, nameof(claimNumber)),
            Status = "new",
            ClaimantName = NormalizeRequired(claimantName, nameof(claimantName)),
            ClaimantEmail = NormalizeRequired(claimantEmail, nameof(claimantEmail)),
            ClaimantPhone = NormalizeRequired(claimantPhone, nameof(claimantPhone)),
            PolicyNumber = NormalizeRequired(policyNumber, nameof(policyNumber)),
            LossDateUtc = EnsureLossDate(lossDateUtc),
            LossType = NormalizeRequired(lossType, nameof(lossType)),
            LossDescription = NormalizeRequired(lossDescription, nameof(lossDescription)),
            CreatedByUserId = NormalizeRequired(createdByUserId, nameof(createdByUserId)),
            CreatedAtUtc = createdAtUtc
        };
    }

    public bool UpdateCoreDetails(
        string claimantName,
        string claimantEmail,
        string claimantPhone,
        string policyNumber,
        DateTime lossDateUtc,
        string lossType,
        string lossDescription,
        string updatedByUserId,
        DateTime updatedAtUtc)
    {
        var normalizedClaimantName = NormalizeRequired(claimantName, nameof(claimantName));
        var normalizedClaimantEmail = NormalizeRequired(claimantEmail, nameof(claimantEmail));
        var normalizedClaimantPhone = NormalizeRequired(claimantPhone, nameof(claimantPhone));
        var normalizedPolicyNumber = NormalizeRequired(policyNumber, nameof(policyNumber));
        var normalizedLossType = NormalizeRequired(lossType, nameof(lossType));
        var normalizedLossDescription = NormalizeRequired(lossDescription, nameof(lossDescription));
        var normalizedLossDateUtc = EnsureLossDate(lossDateUtc);
        var normalizedUpdatedByUserId = NormalizeRequired(updatedByUserId, nameof(updatedByUserId));

        var hasChanged =
            !string.Equals(ClaimantName, normalizedClaimantName, StringComparison.Ordinal) ||
            !string.Equals(ClaimantEmail, normalizedClaimantEmail, StringComparison.Ordinal) ||
            !string.Equals(ClaimantPhone, normalizedClaimantPhone, StringComparison.Ordinal) ||
            !string.Equals(PolicyNumber, normalizedPolicyNumber, StringComparison.Ordinal) ||
            LossDateUtc != normalizedLossDateUtc ||
            !string.Equals(LossType, normalizedLossType, StringComparison.Ordinal) ||
            !string.Equals(LossDescription, normalizedLossDescription, StringComparison.Ordinal);

        if (!hasChanged)
        {
            return false;
        }

        ClaimantName = normalizedClaimantName;
        ClaimantEmail = normalizedClaimantEmail;
        ClaimantPhone = normalizedClaimantPhone;
        PolicyNumber = normalizedPolicyNumber;
        LossDateUtc = normalizedLossDateUtc;
        LossType = normalizedLossType;
        LossDescription = normalizedLossDescription;
        UpdatedByUserId = normalizedUpdatedByUserId;
        UpdatedAtUtc = updatedAtUtc;

        return true;
    }

    public ClaimNote AddNote(string content, string createdByUserId, DateTime createdAtUtc)
    {
        var note = new ClaimNote
        {
            Id = Guid.NewGuid(),
            ClaimId = Id,
            Content = NormalizeRequired(content, nameof(content)),
            CreatedByUserId = NormalizeRequired(createdByUserId, nameof(createdByUserId)),
            CreatedAtUtc = EnsurePastOrPresentUtc(createdAtUtc, nameof(createdAtUtc))
        };

        Notes.Add(note);
        return note;
    }

    public ClaimDocument AddDocument(
        string fileName,
        string fileType,
        string storageIdentifier,
        string uploadedByUserId,
        DateTime uploadedAtUtc,
        string? contentType = null,
        long fileSizeBytes = 0)
    {
        if (fileSizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileSizeBytes), "File size cannot be negative.");
        }

        var document = new ClaimDocument
        {
            Id = Guid.NewGuid(),
            ClaimId = Id,
            FileName = NormalizeRequired(fileName, nameof(fileName)),
            FileType = NormalizeRequired(fileType, nameof(fileType)),
            StorageIdentifier = NormalizeRequired(storageIdentifier, nameof(storageIdentifier)),
            UploadedByUserId = NormalizeRequired(uploadedByUserId, nameof(uploadedByUserId)),
            UploadedAtUtc = EnsurePastOrPresentUtc(uploadedAtUtc, nameof(uploadedAtUtc)),
            ContentType = string.IsNullOrWhiteSpace(contentType) ? null : contentType.Trim(),
            FileSizeBytes = fileSizeBytes
        };

        Documents.Add(document);
        return document;
    }

    private static string NormalizeRequired(string value, string paramName)
    {
        var normalized = value.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        return normalized;
    }

    private static DateTime EnsureLossDate(DateTime value)
    {
        var normalized = EnsureUtc(value);

        if (normalized > DateTime.UtcNow)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Loss date cannot be in the future.");
        }

        return normalized;
    }

    private static DateTime EnsurePastOrPresentUtc(DateTime value, string paramName)
    {
        var normalized = EnsureUtc(value);
        if (normalized > DateTime.UtcNow)
        {
            throw new ArgumentOutOfRangeException(paramName, "Timestamp cannot be in the future.");
        }

        return normalized;
    }

    private static DateTime EnsureUtc(DateTime value)
    {
        return value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}