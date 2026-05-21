using ClaimManager.Domain.Claims.Entities;
using FluentAssertions;
using System;
using Xunit;

namespace ClaimManager.Domain.UnitTests.Claims;

public sealed class ClaimTests
{
    private static readonly DateTime _now = new(2026, 5, 11, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void CanForceReassign_ShouldBeAllowed_WhenBlockedForMoreThan48Hours()
    {
        // Arrange
        var claim = Claim.Create("C-1", "Test", "t@t.com", "123", "P-1", _now.AddDays(-10), "Theft", "desc", "user1", "adj1", 1000, _now.AddDays(-10));
        var blockedClaim = claim.With(claim.AdjusterId, claim.Status);
        // Using reflection to set private setter property for test setup
        typeof(Claim).GetProperty(nameof(Claim.BlockedSince))!.SetValue(blockedClaim, _now.AddHours(-49));

        // Act
        var (allowed, reasons) = blockedClaim.CanForceReassign(_now);

        // Assert
        allowed.Should().BeTrue();
        reasons.Should().Contain("Claim has been blocked for more than 48 hours.");
    }

    [Fact]
    public void CanForceReassign_ShouldBeAllowed_WhenAmountIsGreaterThan10000()
    {
        // Arrange
        var claim = Claim.Create("C-1", "Test", "t@t.com", "123", "P-1", _now.AddDays(-10), "Theft", "desc", "user1", "adj1", 10001, _now.AddDays(-10));

        // Act
        var (allowed, reasons) = claim.CanForceReassign(_now);

        // Assert
        allowed.Should().BeTrue();
        reasons.Should().Contain("Claim amount is over €10,000.");
    }

    [Fact]
    public void CanForceReassign_ShouldBeAllowed_WithBothConditionsMet()
    {
        // Arrange
        var claim = Claim.Create("C-1", "Test", "t@t.com", "123", "P-1", _now.AddDays(-10), "Theft", "desc", "user1", "adj1", 10001, _now.AddDays(-10));
        var blockedClaim = claim.With(claim.AdjusterId, claim.Status);
        typeof(Claim).GetProperty(nameof(Claim.BlockedSince))!.SetValue(blockedClaim, _now.AddHours(-49));

        // Act
        var (allowed, reasons) = blockedClaim.CanForceReassign(_now);

        // Assert
        allowed.Should().BeTrue();
        reasons.Should().HaveCount(2);
        reasons.Should().Contain("Claim has been blocked for more than 48 hours.");
        reasons.Should().Contain("Claim amount is over €10,000.");
    }

    [Fact]
    public void CanForceReassign_ShouldBeDisallowed_WhenNoConditionIsMet()
    {
        // Arrange
        var claim = Claim.Create("C-1", "Test", "t@t.com", "123", "P-1", _now.AddDays(-10), "Theft", "desc", "user1", "adj1", 9999, _now.AddDays(-10));
        var blockedClaim = claim.With(claim.AdjusterId, claim.Status);
        typeof(Claim).GetProperty(nameof(Claim.BlockedSince))!.SetValue(blockedClaim, _now.AddHours(-47));

        // Act
        var (allowed, reasons) = blockedClaim.CanForceReassign(_now);

        // Assert
        allowed.Should().BeFalse();
        reasons.Should().BeEmpty();
    }

    [Fact]
    public void GetNextStatus_ShouldThrowException_WhenStatusIsClosed()
    {
        // Arrange
        var claim = Claim.Create("C-1", "Test", "t@t.com", "123", "P-1", _now.AddDays(-10), "Theft", "desc", "user1", "adj1", 1000, _now.AddDays(-10));
        var closedClaim = claim.With(claim.AdjusterId, Enums.ClaimStatus.Closed);

        // Act
        Action act = () => closedClaim.GetNextStatus();

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("No upper state possible for a closed claim.");
    }
}
