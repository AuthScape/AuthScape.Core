# Keycloak (local dev)

A local [Keycloak](https://www.keycloak.org/) instance for testing the AuthScape **Keycloak** auth-provider path. It runs in Docker and auto-imports a pre-built realm (`authscape`) with two OIDC clients and a test user, so there's no manual setup in the admin console.

| | |
|---|---|
| Image | `quay.io/keycloak/keycloak:26.0` |
| Admin console | <http://localhost:8080> |
| Admin login | `admin` / `admin` |
| Realm | `authscape` |
| Container | `authscape-keycloak` |

---

## Prerequisites

- **Docker Desktop** (with the Compose v2 CLI — i.e. `docker compose`, not the old `docker-compose`).
  - Verify: `docker --version` and `docker compose version`.
- Port **8080** must be free on your machine. If something else uses it, see [Changing the port](#changing-the-port).

---

## Install / run

From this folder (`AuthScape.Core/keycloak`):

```powershell
cd C:\Development\AuthScape\AuthScape.Core\keycloak
docker compose up -d
```

First start pulls the image and imports `realm-authscape.json` (because of the `--import-realm` flag). Give it ~20–40 seconds, then open <http://localhost:8080> and sign in with `admin` / `admin`.

To confirm it's healthy:

```powershell
docker compose ps
docker compose logs -f keycloak   # Ctrl+C to stop following
```

You're ready when the log shows a line like `Keycloak 26.0.x ... started in ...`.

---

## What gets imported

The realm `authscape` is created with:

### Clients

| Client ID | Type | Use | Secret |
|---|---|---|---|
| `authscape-api` | Confidential | Server-to-server / API (standard flow, direct grants, service account) | `authscape-api-dev-secret` |
| `authscape-spa` | Public (PKCE `S256`) | Browser SPA, e.g. the React app on `localhost:3000` | — |

Both clients add an audience mapper so issued access tokens carry the `authscape-api` audience.

**Redirect URIs / origins** (already configured):
- `authscape-api`: `http://localhost:3000/*`, `https://localhost:44303/*`
- `authscape-spa`: `http://localhost:3000/*`

> If your frontend/API runs on different URLs, update the `redirectUris` / `webOrigins` (see [Editing the realm](#editing-the-realm)).

### Test user

| Username | Password | Email |
|---|---|---|
| `testuser` | `Password123!` | `testuser@example.com` |

---

## OIDC endpoints

Base (discovery): <http://localhost:8080/realms/authscape/.well-known/openid-configuration>

| Purpose | URL |
|---|---|
| Issuer / Authority | `http://localhost:8080/realms/authscape` |
| Authorization | `http://localhost:8080/realms/authscape/protocol/openid-connect/auth` |
| Token | `http://localhost:8080/realms/authscape/protocol/openid-connect/token` |
| UserInfo | `http://localhost:8080/realms/authscape/protocol/openid-connect/userinfo` |
| JWKS | `http://localhost:8080/realms/authscape/protocol/openid-connect/certs` |
| End session (logout) | `http://localhost:8080/realms/authscape/protocol/openid-connect/logout` |

### Wiring it into AuthScape

Point the API's Keycloak/OIDC settings at the confidential client:

```
Authority      = http://localhost:8080/realms/authscape
ClientId       = authscape-api
ClientSecret   = authscape-api-dev-secret
Audience       = authscape-api
```

The browser SPA uses `authscape-spa` (public, PKCE — no secret).

### Quick smoke test (get a token)

```powershell
curl -X POST "http://localhost:8080/realms/authscape/protocol/openid-connect/token" `
  -H "Content-Type: application/x-www-form-urlencoded" `
  -d "grant_type=password" `
  -d "client_id=authscape-api" `
  -d "client_secret=authscape-api-dev-secret" `
  -d "username=testuser" `
  -d "password=Password123!"
```

A JSON response containing `access_token` means everything is working.

---

## Stop / reset

```powershell
docker compose stop          # stop, keep data
docker compose down          # stop + remove the container
docker compose down -v       # stop + remove container AND volumes (full wipe)
```

> **Note:** the realm is only imported on **first** start. If you change `realm-authscape.json` after the realm already exists, run `docker compose down -v` first so the import runs again on the next `up`. (See [Editing the realm](#editing-the-realm) for the alternative.)

---

## Configuration notes

### Editing the realm

Two options after editing `realm-authscape.json`:

1. **Re-import (simplest):** `docker compose down -v` then `docker compose up -d`. ⚠️ This wipes all Keycloak data (any users/changes you made in the console).
2. **Edit live in the console:** make changes at <http://localhost:8080> under the `authscape` realm. These persist as long as you don't run `down -v`, but won't be reflected back into `realm-authscape.json` unless you export.

To export the current realm back to JSON (to capture console changes):

```powershell
docker exec authscape-keycloak /opt/keycloak/bin/kc.sh export `
  --dir /tmp/export --realm authscape
docker cp authscape-keycloak:/tmp/export/authscape-realm.json .\realm-authscape-export.json
```

### Changing the port

Edit `docker-compose.yml` and change the host side of the port mapping (and `KC_HTTP_PORT` if you want the container to listen elsewhere):

```yaml
ports:
  - "8081:8080"   # host 8081 -> container 8080
```

Then update the endpoint URLs above (and your AuthScape config) accordingly.

---

## Troubleshooting

| Symptom | Fix |
|---|---|
| `port is already allocated` on `up` | Something else uses 8080 — change the host port (above) or free 8080. |
| Realm changes not showing | The import only runs once. `docker compose down -v` then `up -d`. |
| Can't log into admin console | Creds are `admin` / `admin`. If you changed `KC_BOOTSTRAP_ADMIN_*`, use those. Admin env vars only apply on first start — wipe with `down -v` to reset. |
| Redirect URI error during login | Add your app's URL to the client's `redirectUris` / `webOrigins` in the console or the realm JSON. |
| Container won't start | `docker compose logs keycloak` to see the error. |

---

## Files

| File | Purpose |
|---|---|
| `docker-compose.yml` | Runs Keycloak in dev mode with realm import enabled. |
| `realm-authscape.json` | The `authscape` realm definition (clients, mappers, test user). |
