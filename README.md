# Culture Sandbox

Blazor Web App (.NET 10) for experimenting with culture in **Interactive Server** mode. The render mode is global (`@rendermode="InteractiveServer"` on `Routes` and `HeadOutlet` in `App.razor`), so every page runs over a SignalR circuit.

```sh
dotnet run   # or: dotnet watch
```

## How culture works here

1. Open the app with `?user=alice`. `UserRequestCultureProvider` (the only culture provider) saves the user id in a `sandbox-user` cookie. This stands in for an auth cookie. It then loads alice's record from `App_Data/users.json`. Unknown users are created with `en`.
2. That request prerenders the page. `App.razor` is still static at this point, so it reads `HttpContext` and stores the user id and provider in an `InitialRequestInfo`. It passes that into `Routes`, which cascades it to the pages.
3. The circuit starts with its own HTTP request (`/_blazor/negotiate` + connect). That request doesn't have the page's query string, but it does have the cookie, so the provider loads alice's culture again. The circuit keeps that culture for its whole lifetime.
4. To change culture, the switcher sends you to `/culture/set` with `forceLoad: true`. That updates alice's record in the JSON file and redirects, so a new request loads the new culture and a new circuit starts.

Every load is logged (`Loaded culture fr-FR for user alice (/_blazor/negotiate)`), so watch the console to see when and for whom the "DB" is read. The store has no cache: each load re-reads the file.

Endpoints: `/culture/set?culture=xx&redirectUri=/` (needs a user), `/user/clear` (log out), `/users` (raw store contents).

## Where things are

| What | Where |
| --- | --- |
| Supported cultures, provider setup, `/culture/set`, `/user/clear`, `/users` endpoints | `Program.cs` |
| Per-user culture provider (query → cookie → store) | `Localization/UserRequestCultureProvider.cs` |
| JSON "DB" of users and their cultures | `Data/UserCultureStore.cs` → `App_Data/users.json` |
| Global render mode + capturing the initial request's culture info | `Components/App.razor`, `InitialRequestInfo.cs`, `Components/Routes.razor` |
| Translations (EN default + FR) | `Resources/SharedResource.resx`, `Resources/SharedResource.fr.resx` |
| `IStringLocalizer<SharedResource> L` injected everywhere | `Components/_Imports.razor` |
| User display, culture switcher, log out | `Components/Layout/CultureSelector.razor` |
| Translated text + formatting + culture diagnostics | `Components/Shared/LocalizedSamples.razor` |
| Circuit diagnostics, all users in store, forcing the culture inside the circuit | `Components/Pages/Home.razor` |
| Input parsing (`@bind`, `@bind:culture`) | `Components/Pages/Inputs.razor` |

## Things to try

- **Two users side by side.** Open `/?user=alice` in one browser and `/?user=bob` in a private window or another browser. Save `fr` for alice. Bob's circuit and his next reload stay `en`. (Two tabs in the same browser share the cookie, so they are always the same user, just as with real auth.)
- **New users.** `/?user=carol` creates carol with `en`. Check `/users` or the table on Home.
- **Editing the DB directly.** Change a culture in `App_Data/users.json` by hand. Open tabs keep their culture until you reload them, and a reload picks up the change.
- **What triggers a load.** Watch the console during one browser page load. There's a load for the page, for `/_blazor/negotiate`, for the circuit's WebSocket connection, and for every static file (CSS, JS), because `UseRequestLocalization()` runs for every request. With a real DB, consider skipping static files or caching per request or user.
- **Without `forceLoad`.** In `CultureSelector.razor`, change `forceLoad: true` to `false`. Blazor then routes inside the circuit, so `/culture/set` never runs.
- **Forcing culture inside the circuit.** On Home, click a "Force culture" button. Then click the counter, or go to Inputs and back. Does the change survive?
- **`DefaultThreadCurrentCulture`.** The red button sets the culture for the whole process: an example of what "loading the culture for all users" looks like. Open a new tab with no user to see it.
- **Prerendering.** The first paint is prerendered over plain HTTP (`Interactive: False`), then the circuit re-renders. Try `new InteractiveServerRenderMode(prerender: false)` in `App.razor`.
- **Neutral vs. specific cultures.** `fr` and `en` show the generic currency sign `¤`. `fr-FR` shows €, `fr-CA` shows $ and `en-GB` shows £. `fr-CA` still gets its text from the `.fr` resx file because .NET falls back from `fr-CA` to `fr`.
- **Input parsing.** Under `fr`, `1234,5` parses in a text box. `type=number` and `@bind:culture="CultureInfo.InvariantCulture"` both expect `1234.5`.
- **Missing translations.** Delete a key from the `.fr` resx. The default text is shown instead. If the key is missing everywhere, the key name itself is shown.
