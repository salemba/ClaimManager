namespace ClaimManager.Application.UnitTests.Claims;

using ClaimManager.Application.Services;
using ClaimManager.Application.Interfaces;
using ClaimManager.Domain.Claims.Entities;
using Moq;
using System.Threading.Tasks;
using Xunit;
using System;
using FluentAssertions;
using ClaimManager.Application.Exceptions;

public class ClaimServiceTests
{
    private readonly Mock<IClaimRepository> _claimRepositoryMock;
    private readonly Mock<IAuditRepository> _auditRepositoryMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly ClaimService _claimService;

    public ClaimServiceTests()
    {
        _claimRepositoryMock = new Mock<IClaimRepository>();
        _auditRepositoryMock = new Mock<IAuditRepository>();
        _notificationServiceMock = new Mock<INotificationService>();
        _claimService = new ClaimService(_claimRepositoryMock.Object, _auditRepositoryMock.Object, _notificationServiceMock.Object);
    }

    [Fact]
    public async Task ForceReassignClaimAsync_ShouldThrowClaimNotFoundException_WhenClaimDoesNotExist()
    {
        // Arrange
        _claimRepositoryMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Claim)null);

        // Act
        Func<Task> act = () => _claimService.ForceReassignClaimAsync(Guid.NewGuid(), "newAdj", "supervisor");

        // Assert
        await act.Should().ThrowAsync<ClaimNotFoundException>();
    }

    [Fact]
    public async Task ForceReassignClaimAsync_ShouldThrowForceReassignmentNotAllowedException_WhenConditionsAreNotMet()
    {
        // Arrange
        var claim = Claim.Create("C-1", "Test", "t@t.com", "123", "P-1", DateTime.UtcNow.AddDays(-10), "Theft", "desc", "user1", "adj1", 100, DateTime.UtcNow.AddDays(-10));
        _claimRepositoryMock.Setup(r => r.GetByIdAsync(claim.Id)).ReturnsAsync(claim);

        // Act
        Func<Task> act = () => _claimService.ForceReassignClaimAsync(claim.Id, "newAdj", "supervisor");

        // Assert
        await act.Should().ThrowAsync<ForceReassignmentNotAllowedException>();
    }

    [Fact]
    public async Task ForceReassignClaimAsync_ShouldReassignClaim_WhenConditionsAreMet()
    {
        // Arrange
        var claim = Claim.Create("C-1", "Test", "t@t.com", "123", "P-1", DateTime.UtcNow.AddDays(-10), "Theft", "desc", "user1", "adj1", 10001, DateTime.UtcNow.AddDays(-10));
        _claimRepositoryMock.Setup(r => r.GetByIdAsync(claim.Id)).ReturnsAsync(claim);

        // Act
        var updatedClaim = await _claimService.ForceReassignClaimAsync(claim.Id, "newAdj", "supervisor");

        // Assert
        updatedClaim.AdjusterId.Should().Be("newAdj");
        updatedClaim.Status.Should().Be(Domain.Claims.Enums.ClaimStatus.UnderReview);
        _auditRepositoryMock.Verify(r => r.AddForceReassignAuditAsync(It.IsAny<Domain.Audit.Entities.ForceReassignAudit>()), Times.Once);
        _notificationServiceMock.Verify(s => s.NotifyAdjusterReassignmentAsync("adj1", "newAdj", claim.Id), Times.Once);
    }
}
