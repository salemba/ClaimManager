namespace ClaimManager.Application.Claims.Dtos;

public sealed record SupervisorInterventionDto(
    string? NewAdjusterId,
    string? NewState,
    string Reason);
