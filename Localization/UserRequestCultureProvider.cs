using CultureSandbox.Data;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.StaticAssets;

namespace CultureSandbox.Localization;

/// <summary>
/// Resolves the culture from the current user's record in <see cref="UserCultureStore"/>.
/// The user comes from <c>?user=</c> (remembered in a cookie, standing in for an auth cookie) because the
/// circuit's SignalR request (<c>/_blazor</c>) doesn't carry the page's query string, only its cookies.
/// Runs per HTTP request, so each user's culture is loaded independently.
/// </summary>
public class UserRequestCultureProvider : RequestCultureProvider
{
    public const string UserQueryKey = "user";
    public const string UserCookieName = "sandbox-user";
    public const string UserIdItemKey = "SandboxUserId";

    public override async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        // Static files (CSS, JS, images, _framework/*) don't need a culture: skip the store lookup.
        // Works because WebApplication runs routing before this middleware, so the endpoint is already selected.
        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<StaticAssetDescriptor>() is not null)
        {
            return await NullProviderCultureResult;
        }

        var queryUser = httpContext.Request.Query[UserQueryKey].ToString();
        var cookieUser = httpContext.Request.Cookies[UserCookieName];
        var userId = string.IsNullOrWhiteSpace(queryUser) ? cookieUser : queryUser.Trim();

        if (string.IsNullOrWhiteSpace(userId))
        {
            return await NullProviderCultureResult;
        }

        if (userId != cookieUser)
        {
            httpContext.Response.Cookies.Append(UserCookieName, userId,
                new CookieOptions { IsEssential = true, HttpOnly = true, SameSite = SameSiteMode.Lax });
        }

        httpContext.Items[UserIdItemKey] = userId;

        var store = httpContext.RequestServices.GetRequiredService<UserCultureStore>();
        var user = await store.GetOrCreateAsync(userId, httpContext.Request.Path);
        return new ProviderCultureResult(user.Culture);
    }
}
