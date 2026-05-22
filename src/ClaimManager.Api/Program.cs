using ClaimManager.Api.Configuration;
using ClaimManager.Api.Endpoints.Auth;
using ClaimManager.Api.Endpoints.Claims;
using ClaimManager.Api.Endpoints.Dashboard;
using ClaimManager.Api.Endpoints.Workspace;
using ClaimManager.Application.Security;
using ClaimManager.Infrastructure.Integrations.DocumentRepository;
using ClaimManager.Infrastructure.Integrations.Messaging;
using ClaimManager.Infrastructure.Integrations.PaymentSystem;
using ClaimManager.Infrastructure.Integrations.PolicySystem;
using ClaimManager.Infrastructure.Identity;
using ClaimManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.Configure<PolicySystemOptions>(builder.Configuration.GetSection("PolicySystem"));
builder.Services.Configure<PaymentSystemOptions>(builder.Configuration.GetSection("PaymentSystem"));
builder.Services.Configure<DocumentRepositoryOptions>(builder.Configuration.GetSection("DocumentRepository"));
builder.Services.Configure<MessagingOptions>(builder.Configuration.GetSection("Messaging"));

builder.Services.AddHealthChecks()
    .AddCheck<PolicySystemHealthCheck>("policy-system", tags: ["integration"])
    .AddCheck<PaymentSystemHealthCheck>("payment-system", tags: ["integration"])
    .AddCheck<DocumentRepositoryHealthCheck>("document-repository", tags: ["integration"])
    .AddCheck<MessagingHealthCheck>("messaging", tags: ["integration"]);

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AntiforgeryProblemDetailsHandler>();
builder.Services.AddOpenApi();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = AntiforgeryTokenCookie.HeaderName;
});
builder.Services.AddSingleton<IDocumentRepository>(_ => new LocalDocumentRepository());
builder.Services.AddSingleton<IPolicySystemClient>(sp =>
    new LocalPolicySystemClient(sp.GetRequiredService<ILoggerFactory>().CreateLogger<LocalPolicySystemClient>()));
builder.Services.AddSingleton<IPaymentSystemClient>(sp =>
    new LocalPaymentSystemClient(sp.GetRequiredService<ILoggerFactory>().CreateLogger<LocalPaymentSystemClient>()));
builder.Services.AddSingleton<IMessagingClient>(sp =>
    new LocalMessagingClient(sp.GetRequiredService<ILoggerFactory>().CreateLogger<LocalMessagingClient>()));

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
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (AntiforgeryValidationException) when (context.Request.Path.StartsWithSegments("/api") && !context.Response.HasStarted)
    {
        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status400BadRequest;

        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid antiforgery token",
                Detail = "The request could not be validated. Refresh the page and try again."
            }
        });
    }
});
app.UseAntiforgery();

app.MapDefaultEndpoints();
app.MapAuthEndpoints();
app.MapClaimEndpoints();
app.MapWorkspaceEndpoints();
app.MapDashboardEndpoints();

app.UseFileServer();

app.Run();

public partial class Program;
