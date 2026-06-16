# AuthScape auth-flow examples

AuthScape ships **one API host** (`API`) that supports both authentication flows. The token issuer
is selected at runtime by `Authentication:Provider` in `API/appsettings.json` — set it to
`OpenIddict` or `Keycloak`. No second host, no rebuild.

| Flow | Issuer | What to run |
|------|--------|-------------|
| **OpenIddict** | The bundled `IDP` host issues tokens | `IDP` + `API` (`Provider = OpenIddict`) |
| **Keycloak** | A Dockerized Keycloak realm issues tokens | `keycloak/` (Docker) + `API` (`Provider = Keycloak`) |

The API listens on **https://localhost:5001**. The shared libraries (`AuthScape.Services`,
`AuthScape.Controllers`, `Models`, `Services`, `Services.Database`, the `AuthScape.AuthManager.*`
adapters) are reused for both flows — only the configured provider differs.

## Pick a flow from the startup dropdown

The Visual Studio "Startup Projects" dropdown is driven by `AuthScape.slnLaunch`:

- **OpenIddict** → starts `IDP` (https://localhost:44303) and `API` (https://localhost:5001).
- **Keycloak** → starts `API` (https://localhost:5001). Keycloak itself runs in Docker (see below) —
  start it once before launching this profile.

Set `Authentication:Provider` in `API/appsettings.json` to match the profile you run.

---

## OpenIddict flow

The `IDP` host is the OpenIddict authorization server; `API` validates the tokens it issues
(`Authentication:Provider = OpenIddict`).

1. Select the **OpenIddict** startup profile and run (or `dotnet run --project IDP` and
   `dotnet run --project API`).
2. IDP comes up at `https://localhost:44303`, API at `https://localhost:5001/scalar`.
3. Get a token (client-credentials) and call a protected endpoint:

   ```bash
   curl -k -X POST https://localhost:44303/connect/token \
     -d grant_type=client_credentials \
     -d client_id=resource_server_1 \
     -d client_secret=846B62D0-DEF9-4215-A99D-86E6B8DAB342

   curl -k https://localhost:5001/me -H "Authorization: Bearer <access_token>"
   ```

   `GET /me` (in `Controllers/ExampleController.cs`) returns `200` with the token's claims.

---

## Keycloak flow

`API` validates tokens issued by a local Keycloak realm (`Authentication:Provider = Keycloak`).
Keycloak is **not** a .NET project, so start it in Docker first:

```bash
cd AuthScape.Core
docker compose up -d        # admin console http://localhost:8080 (admin / admin)
```

The `authscape` realm, the `authscape-api` client, and a test user are imported automatically from
`keycloak/realm-authscape.json`.

1. With Keycloak running, set `Provider = Keycloak`, select the **Keycloak** startup profile and run
   (or `dotnet run --project API`). API comes up at `https://localhost:5001/scalar`.
2. Get a token from Keycloak and call a protected endpoint:

   ```bash
   curl -X POST http://localhost:8080/realms/authscape/protocol/openid-connect/token \
     -d grant_type=password \
     -d client_id=authscape-api \
     -d client_secret=authscape-api-dev-secret \
     -d username=testuser -d password=Password123!

   curl -k https://localhost:5001/me -H "Authorization: Bearer <access_token>"
   ```

   `GET /me` returns `200` with the Keycloak claims mapped into AuthScape roles.

Stop & wipe Keycloak with `docker compose down -v`.

---

## Adding a third flow later

The provider adapters live under the `AuthManagers` solution folder
(`AuthScape.AuthManager.<Provider>`). To add a new flow: implement an adapter, reference it from
`API`, branch on it in `AuthScapeIdentityComposition.cs`, and add its config section to
`API/appsettings.json`.
