using ClaimManager.Api.Configuration;
using ClaimManager.Api.Endpoints.Auth;
using ClaimManager.Api.Endpoints.Claims;
using ClaimManager.Api.Endpoints.Workspace;
using ClaimManager.Application.Security;
using ClaimManager.Infrastructure.Integrations.DocumentRepository;
using ClaimManager.Infrastructure.Identity;
using ClaimManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = AntiforgeryTokenCookie.HeaderName;
});
builder.Services.AddSingleton<IDocumentRepository>(_ => new LocalDocumentRepository());

builder.AddNpgsqlDbContext<ClaimManagerDbContext>(
    connectionName: "postgresdb",
    configureDbContextOptions: options => options.UseSnakeCaseNamingConvention());

builder.Services
    .AddIdentityCore<ClaimManagerUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredLength = 12;
    })
    .AddRoles<ClaimManagerRole>()
    .AddEntityFrameworkStores<ClaimManagerDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "claimmanager.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ClaimManagerPolicies.Adjuster, policy =>
        policy.RequireRole(ClaimManagerRoles.Adjuster, ClaimManagerRoles.Admin));
    options.AddPolicy(ClaimManagerPolicies.Supervisor, policy =>
        policy.RequireRole(ClaimManagerRoles.Supervisor, ClaimManagerRoles.Admin));
    options.AddPolicy(ClaimManagerPolicies.Governance, policy =>
        policy.RequireRole(ClaimManagerRoles.Governance, ClaimManagerRoles.Admin));
    options.AddPolicy(ClaimManagerPolicies.Admin, policy =>
        policy.RequireRole(ClaimManagerRoles.Admin));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(ApiProblemDetailsExtensions.WriteProblemDetailsAsync);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapAuthEndpoints();
app.MapClaimEndpoints();
app.MapWorkspaceEndpoints();

app.UseFileServer();

app.Run();

public partial class Program;
