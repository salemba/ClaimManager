using ClaimManager.Application.Audit.Commands;

namespace ClaimManager.Application.UnitTests.Audit;

public sealed class RecordClaimAuditCommandTests
{
    [Fact]
    public void ToEntity_maps_command_values_to_audit_entity()
    {
        var performedAtUtc = new DateTime(2026, 5, 12, 9, 15, 0, DateTimeKind.Utc);
        var command = new RecordClaimAuditCommand(
            Guid.NewGuid(),
            "updated",
            "Claimant email updated from 'before@example.com' to 'after@example.com'.",
            "adjuster-2",
            performedAtUtc);

        var entity = command.ToEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
        Assert.Equal(command.ClaimId, entity.ClaimId);
        Assert.Equal(command.Action, entity.Action);
        Assert.Equal(command.Summary, entity.Summary);
        Assert.Equal(command.PerformedByUserId, entity.PerformedByUserId);
        Assert.Equal(command.PerformedAtUtc, entity.PerformedAtUtc);
    }
}