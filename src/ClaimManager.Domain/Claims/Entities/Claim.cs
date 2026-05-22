namespace ClaimManager.Domain.Claims;

using System.Text.Json;

public sealed record ClaimDataIntegrityIssue(string Dependency, string Message);

public sealed record ClaimReconciliationDetails(
    DateTime AttemptedAtUtc,
    string[] RetriedDependencies,
    string[] RecoveredDependencies,
    string[] UnresolvedDependencies,
    string Summary);

public sealed class Claim
{
    private static readonly Dictionary<string, (string NextState, string? NextExpectedAction)> _advanceTransitions = new()
    {
        ["new"] = ("open", "Investigate loss details"),
        ["open"] = ("in-review", "Review and document findings"),
        ["approved"] = ("closed", null),
    };

    private static readonly HashSet<string> _approvalRoutingStates = ["open", "in-review"];


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

    public string? BlockerType { get; set; }

    public string? BlockerReason { get; set; }

    public string? OwnedByUserId { get; set; }

    public string? NextExpectedAction { get; set; }

    public bool HasDataIntegrityWarning { get; set; }

    public string? DataIntegrityWarningMessage { get; set; }

    public string? ActiveDataIntegrityIssuesJson { get; set; }

    public string? LastReconciliationDetailsJson { get; set; }

    public string? PolicyHolder { get; set; }

    public string? CoverageType { get; set; }

    public DateOnly? PolicyEffectiveDate { get; set; }

    public DateOnly? PolicyExpirationDate { get; set; }

    public DateTime? PolicySyncedAtUtc { get; set; }

    public string? PaymentReference { get; set; }

    public string? PaymentStatus { get; set; }

    public decimal? PaymentAmount { get; set; }

    public string? PaymentCurrency { get; set; }

    public DateTimeOffset? PaymentSettledAt { get; set; }

    public DateTime? PaymentSyncedAtUtc { get; set; }

    public DateTime? DocumentSyncedAtUtc { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public IReadOnlyList<string> GetAvailableActions()
    {
        var actions = new List<string>();
        if (_advanceTransitions.ContainsKey(Status))
        {
            actions.Add("advance");
        }
        if (_approvalRoutingStates.Contains(Status))
        {
            actions.Add("route-for-approval");
        }
        return actions;
    }

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
            CreatedAtUtc = createdAtUtc,
            OwnedByUserId = NormalizeRequired(createdByUserId, nameof(createdByUserId)),
            NextExpectedAction = "Initial review"
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

    public string AdvanceWorkflow(string performedByUserId, DateTime advancedAtUtc)
    {
        var normalizedUserId = NormalizeRequired(performedByUserId, nameof(performedByUserId));

        if (!_advanceTransitions.TryGetValue(Status, out var transition))
        {
            throw new InvalidOperationException($"Cannot advance workflow from state '{Status}'.");
        }

        var previousStatus = Status;
        Status = transition.NextState;
        OwnedByUserId = normalizedUserId;
        NextExpectedAction = transition.NextExpectedAction;
        BlockerType = null;
        BlockerReason = null;
        UpdatedByUserId = normalizedUserId;
        UpdatedAtUtc = advancedAtUtc;

        return transition.NextExpectedAction is not null
            ? $"Claim progressed from {previousStatus} to {Status}. Next action: {NextExpectedAction}."
            : $"Claim progressed from {previousStatus} to {Status}.";
    }

    public void RouteForPaymentApproval(string rationale, string performedByUserId, DateTime routedAtUtc)
    {
        var normalizedUserId = NormalizeRequired(performedByUserId, nameof(performedByUserId));
        var normalizedRationale = NormalizeRequired(rationale, nameof(rationale));
        var normalizedUpdatedAtUtc = EnsurePastOrPresentUtc(routedAtUtc, nameof(routedAtUtc));

        if (!_approvalRoutingStates.Contains(Status))
        {
            throw new InvalidOperationException($"Cannot route for payment approval from state '{Status}'.");
        }

        Status = "pending";
        BlockerType = "awaiting-payment-approval";
        BlockerReason = normalizedRationale;
        NextExpectedAction = "Awaiting payment approval decision";
        OwnedByUserId = normalizedUserId;
        UpdatedByUserId = normalizedUserId;
        UpdatedAtUtc = normalizedUpdatedAtUtc;
    }

    public string ApplyPolicyData(
        string policyHolder,
        string coverageType,
        DateOnly effectiveDate,
        DateOnly expirationDate,
        DateTime syncedAtUtc)
    {
        PolicyHolder = NormalizeOptional(policyHolder);
        CoverageType = NormalizeOptional(coverageType);
        PolicyEffectiveDate = effectiveDate;
        PolicyExpirationDate = expirationDate;
        PolicySyncedAtUtc = EnsurePastOrPresentUtc(syncedAtUtc, nameof(syncedAtUtc));

        var clearedPolicyWarning = ResolveDataIntegrityIssue("policy") || ClearLegacyWarning("policy");

        var summary =
            $"Policy data synchronized from external policy system. Policy holder: {PolicyHolder}, coverage: {CoverageType}, effective {PolicyEffectiveDate:yyyy-MM-dd} to {PolicyExpirationDate:yyyy-MM-dd}.";

        return clearedPolicyWarning
            ? $"{summary} Previous data integrity warning cleared and the dependency issue was resolved."
            : summary;
    }

    public void MarkPolicySyncFailed(string reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "reason unknown."
            : reason.Trim();

        UpsertDataIntegrityIssue("policy", $"Policy data synchronization failed — {normalizedReason}");
    }

    public string ApplyPaymentData(
        string? paymentReference,
        string? paymentStatus,
        decimal? amount,
        string? currency,
        DateTimeOffset? settledAt,
        DateTime syncedAtUtc)
    {
        PaymentReference = NormalizeOptional(paymentReference);
        PaymentStatus = NormalizeOptional(paymentStatus);
        PaymentAmount = amount;
        PaymentCurrency = NormalizeOptional(currency);
        PaymentSettledAt = settledAt;
        PaymentSyncedAtUtc = EnsurePastOrPresentUtc(syncedAtUtc, nameof(syncedAtUtc));

        var clearedPaymentWarning = ResolveDataIntegrityIssue("payment") || ClearLegacyWarning("payment");

        var hasPaymentData = PaymentReference is not null ||
            PaymentStatus is not null ||
            PaymentAmount is not null ||
            PaymentCurrency is not null ||
            PaymentSettledAt is not null;

        var summary = hasPaymentData
            ? $"Payment data synchronized. Reference: {PaymentReference}, status: {PaymentStatus ?? "unknown"}, amount: {FormatPaymentAmount(PaymentAmount)} {PaymentCurrency}."
            : "Payment data synchronized — no active payment on file.";

        return clearedPaymentWarning
            ? $"{summary} Previous data integrity warning cleared and the dependency issue was resolved."
            : summary;
    }

    public void MarkPaymentSyncFailed(string reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "reason unknown."
            : reason.Trim();

        UpsertDataIntegrityIssue("payment", $"Payment data synchronization failed — {normalizedReason}");
    }

    public string ApplyDocumentSync(DateTime syncedAtUtc, int importedCount)
    {
        DocumentSyncedAtUtc = EnsurePastOrPresentUtc(syncedAtUtc, nameof(syncedAtUtc));

        var clearedDocumentWarning = ResolveDataIntegrityIssue("documents") || ClearLegacyWarning("document");

        var summary = importedCount > 0
            ? $"Document repository synchronized. {importedCount} document(s) imported from external repository."
            : "Document repository synchronized. No new documents found.";

        return clearedDocumentWarning
            ? $"{summary} Previous data integrity warning cleared and the dependency issue was resolved."
            : summary;
    }

    public void MarkDocumentSyncFailed(string reason)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason)
            ? "reason unknown."
            : reason.Trim();

        UpsertDataIntegrityIssue("documents", $"Document data synchronization failed — {normalizedReason}");
    }

    public IReadOnlyList<ClaimDataIntegrityIssue> GetActiveDataIntegrityIssues()
    {
        return GetDataIntegrityIssues();
    }

    public ClaimReconciliationDetails? GetLastReconciliationDetails()
    {
        if (string.IsNullOrWhiteSpace(LastReconciliationDetailsJson))
        {
            return null;
        }

        return JsonSerializer.Deserialize<ClaimReconciliationDetails>(LastReconciliationDetailsJson);
    }

    public void RecordReconciliationOutcome(
        DateTime attemptedAtUtc,
        IReadOnlyList<string> retriedDependencies,
        IReadOnlyList<string> recoveredDependencies,
        string summary)
    {
        var details = new ClaimReconciliationDetails(
            EnsurePastOrPresentUtc(attemptedAtUtc, nameof(attemptedAtUtc)),
            retriedDependencies.Select(NormalizeDependency).Distinct(StringComparer.Ordinal).OrderBy(static dependency => dependency, StringComparer.Ordinal).ToArray(),
            recoveredDependencies.Select(NormalizeDependency).Distinct(StringComparer.Ordinal).OrderBy(static dependency => dependency, StringComparer.Ordinal).ToArray(),
            GetActiveDataIntegrityIssues().Select(issue => issue.Dependency).Distinct(StringComparer.Ordinal).OrderBy(static dependency => dependency, StringComparer.Ordinal).ToArray(),
            summary.Trim());

        LastReconciliationDetailsJson = JsonSerializer.Serialize(details);
    }

    public ClaimDocument AddDocument(
        string fileName,
        string fileType,
        string storageIdentifier,
        string uploadedByUserId,
        DateTime uploadedAtUtc,
        string? contentType = null,
        long fileSizeBytes = 0,
        string source = "uploaded")
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
            FileSizeBytes = fileSizeBytes,
            Source = NormalizeRequired(source, nameof(source))
        };

        Documents.Add(document);
        return document;
    }

    private static string FormatPaymentAmount(decimal? amount)
    {
        return amount?.ToString("F2") ?? "N/A";
    }

    private void UpsertDataIntegrityIssue(string dependency, string message)
    {
        var normalizedDependency = NormalizeDependency(dependency);
        var issues = GetDataIntegrityIssues().ToList();
        var issue = new ClaimDataIntegrityIssue(normalizedDependency, CapMessage(message));
        var existingIndex = issues.FindIndex(existing => string.Equals(existing.Dependency, normalizedDependency, StringComparison.Ordinal));

        if (existingIndex >= 0)
        {
            issues[existingIndex] = issue;
        }
        else
        {
            issues.Add(issue);
        }

        SetDataIntegrityIssues(issues);
    }

    private bool ResolveDataIntegrityIssue(string dependency)
    {
        var normalizedDependency = NormalizeDependency(dependency);
        var issues = GetDataIntegrityIssues().ToList();
        var removed = issues.RemoveAll(issue => string.Equals(issue.Dependency, normalizedDependency, StringComparison.Ordinal)) > 0;

        if (removed)
        {
            SetDataIntegrityIssues(issues);
        }

        return removed;
    }

    private bool ClearLegacyWarning(string dependencyFragment)
    {
        if (!HasDataIntegrityWarning || string.IsNullOrWhiteSpace(DataIntegrityWarningMessage))
        {
            return false;
        }

        if (!DataIntegrityWarningMessage.Contains(dependencyFragment, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var issues = GetDataIntegrityIssues();
        if (issues.Count == 0)
        {
            HasDataIntegrityWarning = false;
            DataIntegrityWarningMessage = null;
            ActiveDataIntegrityIssuesJson = null;
            return true;
        }

        SetDataIntegrityIssues(issues);
        return true;
    }

    private IReadOnlyList<ClaimDataIntegrityIssue> GetDataIntegrityIssues()
    {
        if (string.IsNullOrWhiteSpace(ActiveDataIntegrityIssuesJson))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<ClaimDataIntegrityIssue>>(ActiveDataIntegrityIssuesJson) ?? [];
    }

    private void SetDataIntegrityIssues(IReadOnlyList<ClaimDataIntegrityIssue> issues)
    {
        if (issues.Count == 0)
        {
            ActiveDataIntegrityIssuesJson = null;
            HasDataIntegrityWarning = false;
            DataIntegrityWarningMessage = null;
            return;
        }

        var normalizedIssues = issues
            .Select(issue => new ClaimDataIntegrityIssue(NormalizeDependency(issue.Dependency), CapMessage(issue.Message)))
            .DistinctBy(issue => issue.Dependency, StringComparer.Ordinal)
            .OrderBy(issue => issue.Dependency, StringComparer.Ordinal)
            .ToArray();

        ActiveDataIntegrityIssuesJson = JsonSerializer.Serialize(normalizedIssues);
        HasDataIntegrityWarning = true;
        DataIntegrityWarningMessage = BuildDataIntegrityWarningMessage(normalizedIssues);
    }

    private static string BuildDataIntegrityWarningMessage(IReadOnlyList<ClaimDataIntegrityIssue> issues)
    {
        if (issues.Count == 1)
        {
            return CapMessage(issues[0].Message);
        }

        var dependencies = string.Join(", ", issues.Select(issue => GetDependencyLabel(issue.Dependency)));
        return CapMessage($"Claim data requires reconciliation for: {dependencies}.");
    }

    private static string NormalizeDependency(string dependency)
    {
        var normalized = dependency.Trim().ToLowerInvariant();
        return normalized switch
        {
            "document" => "documents",
            _ => normalized,
        };
    }

    private static string GetDependencyLabel(string dependency)
    {
        return NormalizeDependency(dependency) switch
        {
            "policy" => "Policy",
            "payment" => "Payment",
            "documents" => "Documents",
            var value => value,
        };
    }

    private static string CapMessage(string message)
    {
        var normalized = string.IsNullOrWhiteSpace(message) ? "reason unknown." : message.Trim();
        return normalized.Length > 500 ? normalized[..500] : normalized;
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

    private static string? NormalizeOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
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

    public int DaysSinceCreated(DateTimeOffset utcNow) => (utcNow - CreatedAtUtc).Days;

    public bool IsStuck(DateTimeOffset utcNow)
    {
        if (Status is "closed" or "paid")
        {
            return false;
        }

        return (utcNow - (UpdatedAtUtc ?? CreatedAtUtc)).TotalDays > 7;
    }

    public bool IsAging(DateTimeOffset utcNow)
    {
        if (Status is "closed" or "paid")
        {
            return false;
        }

        return DaysSinceCreated(utcNow) > 30;
    }

    public bool RequiresAttention()
    {
        return !string.IsNullOrEmpty(BlockerType) || HasDataIntegrityWarning;
    }

    public bool IsPendingApproval()
    {
        return Status is "pending-approval" or "pending";
    }
}