namespace ClaimManager.Api.Configuration;

using Microsoft.AspNetCore.Antiforgery;

internal static class AntiforgeryTokenCookie
{
    public const string ClientCookieName = "claimmanager.csrf";
    public const string HeaderName = "X-CSRF-TOKEN";

    public static void Refresh(HttpContext httpContext, IAntiforgery antiforgery)
    {
        var tokens = antiforgery.GetAndStoreTokens(httpContext);
        if (string.IsNullOrWhiteSpace(tokens.RequestToken))
        {
            return;
        }

        httpContext.Response.Cookies.Append(ClientCookieName, tokens.RequestToken, BuildCookieOptions(httpContext));
    }

    public static void Clear(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(ClientCookieName, BuildCookieOptions(httpContext));
    }

    private static CookieOptions BuildCookieOptions(HttpContext httpContext)
    {
        return new CookieOptions
        {
            HttpOnly = false,
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = httpContext.Request.IsHttps
        };
    }
}