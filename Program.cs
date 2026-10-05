using CultureSandbox.Components;
using CultureSandbox.Data;
using CultureSandbox.Localization;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddSingleton<UserCultureStore>();

// Neutral cultures ("en", "fr") plus specific ones to compare regional formatting (e.g. currency).
string[] supportedCultures = ["en", "en-US", "en-GB", "fr", "fr-FR", "fr-CA"];

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.SetDefaultCulture("en")
        .AddSupportedCultures(supportedCultures)
        .AddSupportedUICultures(supportedCultures);

    // Only the per-user provider: the culture comes from the user's record in the JSON "DB", or the default (en).
    // The built-in query string / cookie / Accept-Language providers are intentionally removed.
    options.RequestCultureProviders = [new UserRequestCultureProvider()];
    options.ApplyCurrentCultureToResponseHeaders = true; // emits Content-Language
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Must run before components (and the Blazor hub) so both SSR requests and circuits get the culture.
app.UseRequestLocalization();

app.UseAntiforgery();

// Saves the culture on the current user's record, then redirects back so a new request (and circuit) picks it up.
// Components can't do this on their own: the culture is resolved by middleware, before any component runs.
app.MapGet("/culture/set", async (string culture, string? redirectUri, HttpContext context, UserCultureStore store) =>
{
    if (!supportedCultures.Contains(culture))
    {
        return Results.BadRequest($"Unsupported culture '{culture}'.");
    }

    var userId = context.Request.Cookies[UserRequestCultureProvider.UserCookieName];
    if (string.IsNullOrWhiteSpace(userId))
    {
        return Results.BadRequest("No user. Open the app with ?user=<name> first.");
    }

    await store.SetCultureAsync(userId, culture);
    return Results.LocalRedirect(string.IsNullOrEmpty(redirectUri) ? "/" : redirectUri);
});

// "Log out": forget the current user.
app.MapGet("/user/clear", (string? redirectUri, HttpContext context) =>
{
    context.Response.Cookies.Delete(UserRequestCultureProvider.UserCookieName);
    return Results.LocalRedirect(string.IsNullOrEmpty(redirectUri) ? "/" : redirectUri);
});

// Raw contents of the JSON "DB".
app.MapGet("/users", async (UserCultureStore store) => Results.Ok(await store.GetAllAsync()));

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
