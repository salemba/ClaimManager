namespace ClaimManager.Api.Endpoints.Auth;

using ClaimManager.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", LoginAsync).AllowAnonymous();
        group.MapPost("/logout", LogoutAsync).RequireAuthorization();
        group.MapGet("/me", GetCurrentUserAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<Results<Ok<AuthSessionResponse>, ValidationProblem, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        SignInManager<ClaimManagerUser> signInManager,
        UserManager<ClaimManagerUser> userManager)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>
            {
                [nameof(LoginRequest.Email)] = ["Email is required."],
                [nameof(LoginRequest.Password)] = ["Password is required."]
            });
        }

        var signInResult = await signInManager.PasswordSignInAsync(request.Email, request.Password, false, false);
        if (!signInResult.Succeeded)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Invalid credentials",
                detail: "The supplied email or password is incorrect.");
        }

        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authentication failed",
                detail: "The authenticated user record could not be loaded.");
        }

        var roles = await userManager.GetRolesAsync(user);

        return TypedResults.Ok(new AuthSessionResponse(user.Id.ToString(), user.Email ?? request.Email, roles.ToArray()));
    }

    private static async Task<NoContent> LogoutAsync(SignInManager<ClaimManagerUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<AuthSessionResponse>, ProblemHttpResult>> GetCurrentUserAsync(
        ClaimsPrincipal principal,
        UserManager<ClaimManagerUser> userManager)
    {
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Authentication required",
                detail: "The current request does not have a valid authenticated user.");
        }

        var roles = await userManager.GetRolesAsync(user);
        return TypedResults.Ok(new AuthSessionResponse(user.Id.ToString(), user.Email ?? user.UserName ?? string.Empty, roles.ToArray()));
    }

    public sealed record LoginRequest(string Email, string Password);

    public sealed record AuthSessionResponse(string UserId, string Email, string[] Roles);
}