# AuthScape

AuthScape is a single-install authentication, error-tracking, and analytics stack for .NET + NextJS. Drop one NuGet and one NPM package into a new project, paste ~10 lines of wiring, and you have a working IDP, an API that issues and validates tokens, and first-party analytics + error tracking — without buying SaaS for each.

## What's included (always-on)

- **Authentication** — OpenIddict-backed token issuer (default), provider-agnostic identity pipeline so you can swap in Keycloak or others without touching call sites. Includes federation hooks (SAML 2.0, LDAP, SCIM, OAuth login) as opt-in plugins.
- **Error tracking** — server-side error capture in the API, pushed to the IDP via HTTP, viewable in the IDP admin dashboard. Client-side error capture in the NextJS package.
- **Analytics** — session, page-view, event, and conversion tracking on the server; GA4 + Microsoft Clarity wiring in the NextJS package.

## What's optional (install only if you need it)

Each of these is a separate NuGet that you `AddAuthScapeXxx()` to opt in:

- `AuthScape.AuthManager.Keycloak` — Keycloak as the token issuer in place of OpenIddict.
- `AuthScape.Ldap`, `AuthScape.Saml2`, `AuthScape.Scim`, `AuthScape.AccountLinking` — federation protocols.
- `AuthScape.UserManageSystem` — CRM-style user/company/location administration with custom fields.
- `AuthScape.Marketplace`, `AuthScape.TicketSystem`, `AuthScape.ContentManagement`, `AuthScape.Document`, `AuthScape.Reporting` — domain extensions.

The previous AI, Email, JIRA, Invoicing, Node-services, Spreadsheet, Kanban, and Billing modules have been removed from the core install to keep the surface focused. You can wire them back in as user-space plugins if you need them.

## .NET install

```bash
dotnet add package AuthScape
```

`Program.cs` — minimal API wiring:

```csharp
using AuthScape;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthScape(builder.Configuration);

var app = builder.Build();
app.MapControllers();
app.Run();
```

That one `AddAuthScape()` call registers: configuration binding, the DbContext (provider auto-detected from the connection string — SqlServer, PostgreSQL, MySQL, or SQLite), the OpenIddict-backed identity pipeline, the analytics write-path, and the error/logging write-path.

`appsettings.json` — minimum viable config:

```json
{
  "AppSettings": {
    "Name": "My App",
    "Stage": 1,
    "IDPUrl": "https://localhost:44303",
    "DatabaseContext": "Server=localhost;Database=AuthScape;Trusted_Connection=true;TrustServerCertificate=true;",
    "WebsiteRedirectUrl": "https://localhost:3000",
    "LoginRedirectUrl": "https://localhost:3000"
  }
}
```

### Swapping OpenIddict for Keycloak

Provider config lives entirely inside one builder call. Swapping is a one-block change — everything else in your `Startup.cs` stays the same.

**OpenIddict (default):**

```csharp
services
    .AddAuthScapeIdentity(o => { o.Mode = AuthProviderMode.Issuing; o.AutoProvisionUsers = true; })
    .UseOpenIddict(o =>
    {
        o.AccessTokenLifetime      = TimeSpan.FromHours(1);
        o.AllowRefreshTokens       = true;
        o.AllowClientCredentials   = true;

        // RFC 7662 introspection against the IDP
        o.UseIntrospection           = true;
        o.Issuer                     = "https://localhost:44303/";
        o.Audience                   = "resource_server_1";
        o.IntrospectionClientId      = "resource_server_1";
        o.IntrospectionClientSecret  = "...";
    });

authenticationManager.RegisterConfigureServices(Configuration, env, services, scope, dbContextSetup);
```

**Keycloak — swap the `.UseOpenIddict(...)` block for `.UseKeycloak(...)`:**

```csharp
services
    .AddAuthScapeIdentity(o => { o.Mode = AuthProviderMode.Validating; o.AutoProvisionUsers = true; })
    .UseKeycloak(o =>
    {
        o.Authority = "https://kc.example.com/realms/myrealm";
        o.ClientId  = "authscape-api";

        // Same introspection switch shape as OpenIddict. Set false for local JWKS validation.
        o.UseIntrospection           = true;
        o.IntrospectionClientId      = "authscape-api";
        o.IntrospectionClientSecret  = "...";
    });

authenticationManager.RegisterConfigureServices(Configuration, env, services, scope, dbContextSetup);
```

That's the whole swap — same `RegisterConfigureServices` call, same `scope` and `dbContextSetup` callbacks.

### Turning on federation plugins

```csharp
builder.Services.AddAuthScapeAccountLinking();
builder.Services.AddAuthScapeLdap();
builder.Services.AddAuthScapeSaml2();
builder.Services.AddAuthScapeScim();
```

Each plugin is inert at runtime until you create a configuration row in the database, so it's safe to register them by default.

## NextJS install

```bash
npm install authscape
```

`pages/_app.js`:

```jsx
import { AuthScapeProvider } from "authscape";
import "react-toastify/dist/ReactToastify.css";

export default function MyApp({ Component, pageProps }) {
  return (
    <AuthScapeProvider
      Component={Component}
      pageProps={pageProps}
      enforceLoggedIn={true}
      enableErrorTracking={true}
      enableNotifications={true}
    />
  );
}
```

`AuthScapeProvider` wires the auth context (sign-in redirect, token refresh, current user hook), the error-tracking client (captures unhandled errors and posts them to your IDP), and the analytics client (GA4 + Microsoft Clarity, session tracking back to your API). It is also exported as `AuthScapeApp` for backwards compatibility.

## End-to-end smoke test

After `AddAuthScape()` and `<AuthScapeProvider>` are in place:

- `dotnet run` the API → it should serve `/.well-known/openid-configuration` (issuer) and protected endpoints with `[Authorize]` should reject anonymous calls.
- `npm run dev` the NextJS site → visit `/login`, sign in, hit a protected route, throw a server-side error from an API endpoint, watch it appear in the IDP error tracking dashboard, and confirm an `AnalyticsEvent` row lands in the database.

## Repo layout

- `AuthScape/` — the meta-package (`AuthScape` NuGet) and the `AddAuthScape()` entry point.
- `AuthScape.AuthManager/`, `AuthScape.AuthManager.OpenIddict/`, `AuthScape.AuthManager.Keycloak/` — identity pipeline + provider packages.
- `AuthScape.Configuration/` — `AppSettings` binding and validation.
- `AuthScape.Services.Database/` — multi-provider database extensions.
- `Plugins/AuthScape.Analytics/`, `Plugins/AuthScape.Logging/` — always-on analytics + error tracking.
- `Plugins/AuthScape.Ldap/`, `Plugins/AuthScape.Saml2/`, `Plugins/AuthScape.Scim/`, `Plugins/AuthScape.AccountLinking/` — federation.
- `Plugins/AuthScape.Marketplace/`, `Plugins/TicketSystem/`, `Plugins/ContentManagement/`, `Plugins/AuthScape.DocumentProcessing/`, `Plugins/Reporting/` — optional extensions.
- `API/`, `IDP/` — the sample API and IDP web hosts.
- `Models/`, `Services/`, `Services.Database/` — shared domain types (slated for further split in a later pass).

## Migration notes for existing installs

If you're upgrading from a pre-slim version of AuthScape:

- The `Stripe`, `SendGrid`, `Invoice*`, `Subscription*`, `PromoCode*`, `Wallet*`, `StoreCredit`, `Coupon`, `FlowProject` / `FlowNode` / `FlowEdge` / `FlowViewport`, `KanbanCard` / `KanbanColumn`, `SomeSheet`, `AnalyticsMail*`, and `Plan` tables are no longer mapped by the AuthScape DbContext. They are not auto-dropped — handle the cleanup in your own migration if you want the tables gone.
- `IMailService` / `ISendGridService` are gone. Invite + forgot-password flows now return the reset/invite URL through their normal response; wire your own email delivery on top of that or install a third-party email plugin.
- `AppSettings.Stripe`, `AppSettings.SendGrid`, `AppSettings.Subscriptions` have been removed from the config schema. Drop the corresponding sections from `appsettings.json`.
