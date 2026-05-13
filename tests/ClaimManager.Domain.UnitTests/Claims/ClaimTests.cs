using ClaimManager.Domain.Claims;

namespace ClaimManager.Domain.UnitTests.Claims;

public sealed class ClaimTests
{
    [Fact]
    public void Claim_preserves_core_foundation_fields()
    {
        var createdAtUtc = new DateTime(2026, 5, 11, 8, 30, 0, DateTimeKind.Utc);

        var claim = Claim.Create(
            "CLM-0099",
            "Jordan Avery",
            "jordan.avery@example.com",
            "555-0100",
            "POL-0009",
            createdAtUtc.AddDays(-1),
            "Water damage",
            "Pipe burst in lower level.",
            "adjuster-1",
            createdAtUtc);

        Assert.Equal("CLM-0099", claim.ClaimNumber);
        Assert.Equal("new", claim.Status);
        Assert.Equal(createdAtUtc, claim.CreatedAtUtc);
        Assert.Equal("Jordan Avery", claim.ClaimantName);
        Assert.Equal("POL-0009", claim.PolicyNumber);
    }

    [Fact]
    public void Updating_claim_core_details_sets_updated_fields_when_material_changes_exist()
    {
        var claim = Claim.Create(
            "CLM-0100",
            "Jordan Avery",
            "jordan.avery@example.com",
            "555-0100",
            "POL-0100",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Water damage",
            "Pipe burst in lower level.",
            "adjuster-1",
            new DateTime(2026, 5, 11, 8, 30, 0, DateTimeKind.Utc));

        var updatedAtUtc = new DateTime(2026, 5, 12, 9, 15, 0, DateTimeKind.Utc);

        var changed = claim.UpdateCoreDetails(
            "Jordan Avery",
            "jordan.updated@example.com",
            "555-0111",
            "POL-0100",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Water damage",
            "Pipe burst in lower level with cabinet damage.",
            "adjuster-2",
            updatedAtUtc);

        Assert.True(changed);
        Assert.Equal("jordan.updated@example.com", claim.ClaimantEmail);
        Assert.Equal("555-0111", claim.ClaimantPhone);
        Assert.Equal(updatedAtUtc, claim.UpdatedAtUtc);
        Assert.Equal("adjuster-2", claim.UpdatedByUserId);
    }

    [Fact]
    public void Claim_can_add_a_note_with_trimmed_content_and_actor_metadata()
    {
        var claim = Claim.Create(
            "CLM-0101",
            "Jordan Avery",
            "jordan.avery@example.com",
            "555-0100",
            "POL-0101",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Water damage",
            "Pipe burst in lower level.",
            "adjuster-1",
            new DateTime(2026, 5, 11, 8, 30, 0, DateTimeKind.Utc));

        var createdAtUtc = new DateTime(2026, 5, 12, 10, 0, 0, DateTimeKind.Utc);

        var note = claim.AddNote("  Customer confirmed remediation vendor appointment.  ", "adjuster-2", createdAtUtc);

        Assert.Equal(claim.Id, note.ClaimId);
        Assert.Equal("Customer confirmed remediation vendor appointment.", note.Content);
        Assert.Equal("adjuster-2", note.CreatedByUserId);
        Assert.Equal(createdAtUtc, note.CreatedAtUtc);
    }

    [Fact]
    public void Create_initializes_workflow_status_defaults()
    {
        var claim = Claim.Create(
            "CLM-0103",
            "Jordan Avery",
            "jordan.avery@example.com",
            "555-0100",
            "POL-0103",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Water damage",
            "Pipe burst in lower level.",
            "adjuster-1",
            new DateTime(2026, 5, 11, 8, 30, 0, DateTimeKind.Utc));

        Assert.Equal("adjuster-1", claim.OwnedByUserId);
        Assert.Equal("Initial review", claim.NextExpectedAction);
        Assert.False(claim.HasDataIntegrityWarning);
        Assert.Null(claim.BlockerType);
        Assert.Null(claim.BlockerReason);
        Assert.Null(claim.DataIntegrityWarningMessage);
    }

    [Fact]
    public void Claim_can_add_document_metadata_with_safe_storage_reference()
    {
        var claim = Claim.Create(
            "CLM-0102",
            "Jordan Avery",
            "jordan.avery@example.com",
            "555-0100",
            "POL-0102",
            new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            "Water damage",
            "Pipe burst in lower level.",
            "adjuster-1",
            new DateTime(2026, 5, 11, 8, 30, 0, DateTimeKind.Utc));

        var uploadedAtUtc = new DateTime(2026, 5, 12, 10, 15, 0, DateTimeKind.Utc);

        var document = claim.AddDocument(" estimate.pdf ", ".pdf", "stored/claim-0102/abc123.bin", "adjuster-2", uploadedAtUtc);

        Assert.Equal(claim.Id, document.ClaimId);
        Assert.Equal("estimate.pdf", document.FileName);
        Assert.Equal(".pdf", document.FileType);
        Assert.Equal("stored/claim-0102/abc123.bin", document.StorageIdentifier);
        Assert.Equal("adjuster-2", document.UploadedByUserId);
        Assert.Equal(uploadedAtUtc, document.UploadedAtUtc);
    }
}