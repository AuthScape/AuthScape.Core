# AuthScape auth-flow examples

AuthScape ships two self-contained backend examples, one per authentication flow. Each lives in
its own solution folder and has its own startup profile in the Visual Studio run dropdown.

| Flow | Solution folder | Projects | Identity provider |
|------|-----------------|----------|-------------------|
| **OpenIddict** | `OpenIddict Example` | `OpenIddict.API` + `OpenIddict.IDP` | The bundled `OpenIddict.IDP` host issues tokens |
| **Keycloak** | `Keycloak Example` | `Keycloak.API` + `keycloak/` (Docker) | A Dockerized Keycloak realm issues tokens |

Both API hosts listen on **https://localhost:5001** (only one flow runs at a time, so they share
the port). The shared libraries (`AuthScape.Services`, `AuthScape.Controllers`, `Models`,
`Services`, `Services.Database`, the `AuthScape.AuthManager.*` adapters) are reused by both — only
the host wiring differs.

## Pick a flow from the startup dropdown

The Visual Studio "Startup Projects" dropdown is driven by `AuthScape.slnLaunch`:

- **OpenIddict** → starts `OpenIddict.IDP` (https://localhost:44303) and `OpenIddict.API`
  (https://localhost:5001).
- **Keycloak** → starts `Keycloak.API` (https://localhost:5001). Keycloak itself runs in Docker
  (see below) — start it once before launching this profile.

---

## OpenIddict flow

The `OpenIddict.IDP` host is the OpenIddict authorization server; `OpenIddict.API` validates the
tokens it issues (`Authentication:Provider = OpenIddict`).

1. Select the **OpenIddict** startup profile and run (or
   `dotnet run --project OpenIddict.IDP` and `dotnet run --project OpenIddict.API`).
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

`Keycloak.API` validates tokens issued by a local Keycloak realm
(`Authentication:Provider = Keycloak`). Keycloak is **not** a .NET project, so start it in Docker
first:

```bash
cd AuthScape.Core/keycloak
docker compose up -d        # admin console http://localhost:8080 (admin / admin)
```

The `authscape` realm, the `authscape-api` client, and a test user are imported automatically from
`realm-authscape.json`.

1. With Keycloak running, select the **Keycloak** startup profile and run (or
   `dotnet run --project Keycloak.API`). API comes up at `https://localhost:5001/scalar`.
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

> **One-click Keycloak (optional):** if you have the Visual Studio *Container development tools*
> workload, you can add a Docker Compose project (`.dcproj`) wrapping `keycloak/docker-compose.yml`
> and include it in the **Keycloak** startup profile so Keycloak boots on F5. Without it, just run
> `docker compose up -d` once per session.

---

## Adding a third flow later

The provider adapters live under the `AuthManagers` solution folder
(`AuthScape.AuthManager.<Provider>`). To add a new flow: implement an adapter, create a host
project (copy one of the existing example hosts), wire it to the adapter in
`AuthScapeIdentityComposition.cs`, add a solution folder + the project to `AuthScape.sln`, and add a
startup profile to `AuthScape.slnLaunch`.
