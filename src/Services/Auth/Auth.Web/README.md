# Auth.Web — OIDC/OAuth 2.0 Authorization Server

Standalone OpenID Connect provider on ASP.NET Core 10 + Blazor Server + OpenIddict. Hosts account UI, admin dashboard and OIDC endpoints in a single process. PostgreSQL is the only stateful dependency; DataProtection keys, OpenIddict artifacts and Identity tables all live in the same database.

> **Audience:** DevOps / SRE / platform engineers deploying or operating this service. For deep architecture details see [CLAUDE.md](CLAUDE.md).

---

## 1. Architecture snapshot

```
┌──────────────┐           ┌────────────────────────────────────────┐
│  Browser /   │ HTTPS     │  Auth.Web (ASP.NET Core 10)            │
│  SPA         ├──────────▶│  ┌──────────────┐  ┌────────────────┐  │
└──────────────┘           │  │ Blazor SSR   │  │ OIDC endpoints │  │
                           │  │ UI + Admin   │  │ + Admin API    │  │
┌──────────────┐           │  └──────┬───────┘  └────────┬───────┘  │
│  Backend S2S │ Bearer    │         └──────────┬────────┘          │
│  (client_cre ├──────────▶│              ┌─────▼─────┐             │
│  dentials)   │           │              │ Identity  │             │
└──────────────┘           │              │ + OpenIdd │             │
                           │              └─────┬─────┘             │
                           └────────────────────┼───────────────────┘
                                                │
                     ┌──────────────────────────▼─────────────────────────┐
                     │ PostgreSQL (schema "auth")                         │
                     │  • Identity (users, roles, tokens, passkeys)       │
                     │  • OpenIddict (apps, authorizations, tokens, scope)│
                     │  • data_protection_keys                            │
                     │  • signing_credentials (RSA keys, DP-encrypted)    │
                     └────────────────────────────────────────────────────┘
```

**External dependencies:** PostgreSQL (required), SMTP (email confirmation, password reset, invites), optional Google reCAPTCHA (v2/v3), optional GitHub/Yandex as external OAuth IdPs. SMS provider is **not implemented** — only `MockSmsSender` ships. Phone confirmation is off by default.

**In-process surfaces:**
- `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/logout` — OIDC endpoints
- `/account/*` — Blazor SSR UI (login, register, manage profile, 2FA, passkeys, reset/forgot password, accept-invite, external-login)
- `/admin/*` — admin dashboard (cookie + `Admin` role)
- `/api/admin/users/*` — server-to-server admin API (Bearer JWT + `auth/admin.users` scope)
- `/health` — liveness (no authentication)

---

## 2. Security model

### 2.1 Authentication schemes

| Scheme | Used by | Storage | Notes |
|---|---|---|---|
| `Identity.Application` cookie | Browser UI, admin dashboard | DataProtection-encrypted cookie | Default scheme |
| `Identity.External` cookie | GitHub / Yandex roundtrip | DP-encrypted cookie | Transient (consumed on callback) |
| OpenIddict Bearer JWT | Admin API, downstream microservices | Stateless | `typ=at+jwt` (RFC 9068), JWKS at `/.well-known/jwks` |

Same process exposes both roles — browser-flow (authorize/login/etc.) and token-flow (token/userinfo/admin-api). OpenIddict Server and Validation coexist: `AddValidation().UseLocalServer()` means Auth.Web validates its own JWTs for its own admin API — no round-trip.

### 2.2 Cryptographic materials

| Material | Lifetime | Rotation | Storage | Notes |
|---|---|---|---|---|
| **DataProtection master certificate (X509)** | **Manual** — set by ops | **Manual** | `DataProtectionSettings.CertificateBase64` (env / secrets store) | **Wraps DP keys at rest.** If missing, DP keys are written to DB in plaintext XML. **Required in production.** |
| **DataProtection keys** | 360 days | 320 days (automatic via `RotateDataProtectionKeysTask` at each startup under advisory lock) | `data_protection_keys` table (wrapped by master cert) | Used for cookies + all Identity tokens + signing-key-at-rest encryption. Rotation (320d) and lifetime (360d) overlap by 40d — long enough for a routine restart to mint the successor key before the current one expires, but requires the service to be actually restarted within that window |
| **OpenIddict signing/encryption RSA keys** | 180 days max age | 140 days (`RotateSigningKeysTask`) | `signing_credentials` table, key material DP-protected | RSA-2048. Both old and new key live in JWKS during the 40-day overlap window (140…180d) |
| **Identity password/email/reset tokens** | ≤1h (Identity defaults) | N/A — derived from DP keys | Stateless | Security-stamp rotation invalidates older tokens |
| **Invite tokens** | 30 days | N/A — derived from DP keys, single-use | Stateless | `DataProtectorTokenProvider<ApplicationUser>("Invite")` |
| **Access tokens (JWT)** | 1 hour | N/A — minted on-demand | Stateless (JWT) | `typ=at+jwt`, `aud` = resource URI from scope |
| **Identity tokens** | 1 hour | N/A | Stateless | Claims gated per scope (OIDC Core §5.4) |
| **Refresh tokens** | 30 days | N/A | `openiddict_tokens` (sliding) | Pruned by `OpenIddictCleanupService` |

### 2.3 Rotation responsibility

```
                   AUTO (at every pod startup, under advisory lock)
                   ┌──────────────────────────────────────────────┐
                   ▼                                              │
     ┌──────────────────────────┐    ┌────────────────────────┐   │
     │ DataProtection keys      │◄───│ RotateDataProtection-  │   │
     │ (DB, wrapped by X509)    │    │ KeysTask (320d / 360d) │   │
     └──────────────┬───────────┘    └────────────────────────┘   │
                    │ protect/unprotect                           │
                    ▼                                             │
     ┌──────────────────────────┐    ┌────────────────────────┐   │
     │ OpenIddict signing keys  │◄───│ RotateSigningKeysTask  │   │
     │ (DB, DP-encrypted)       │    │ (140d / 180d)          │───┘
     └──────────────┬───────────┘    └────────────────────────┘
                    │ sign
                    ▼
     ┌──────────────────────────┐
     │ JWT access / id tokens   │
     │ (stateless, 1h)          │
     └──────────────────────────┘

                   MANUAL (ops-operated)
     ┌──────────────────────────┐
     │ X509 DP master cert      │◄───  rotate before notAfter; see §6.2
     │ (secret store / env var) │
     └──────────────────────────┘
```

**The X509 master certificate is the only thing ops must rotate by hand.** Everything below it rotates itself at pod startup. Rotation procedure in [§6.2](#62-rotating-the-dataprotection-master-certificate).

### 2.4 User-level protections

- **Passwords:** digit + lower + upper + non-alphanumeric, min 6 chars, 1 unique char (Identity). **Note: 6 is below NIST SP 800-63B; consider raising to ≥12 for production.**
- **Account lockout on failed passwords:** **disabled** (`lockoutOnFailure: false` in Login / ResetPassword / AcceptInvite). Template default.
- **2FA:** TOTP + recovery codes, opt-in via `/account/manage/enable_authenticator`. Passkeys supported via Identity SchemaV3.
- **reCAPTCHA:** gates Register / ForgotPassword / ResendEmailConfirmation. **Silently disabled when keys are empty** — this is a known footgun; `ReCaptchaSettings.SiteKey`/`SecretKey` must be set in production.
- **Email confirmation:** required for sign-in (`RequireConfirmedAccount = true`). Login error `IsNotAllowed` offers a Resend link without user enumeration.
- **GDPR:** personal-data download (JSON) + account delete. Delete is gated by `AppSettings:IsDeletePersonalDataEnabled`.
- **Antiforgery:** enabled globally (`app.UseAntiforgery()`); `[ValidateAntiForgeryToken]` on POST actions in `AccountController`.
- **Open-redirect protection:** `IdentityRedirectManager` strips non-relative URIs via `ToBaseRelativePath`; `AcceptInvite` whitelists `returnUrl` against `CorsSettings.AllowedOrigins`.

---

## 3. Configuration reference

Configuration precedence (each layer overrides the previous):
1. `appsettings.json` (committed defaults)
2. `appsettings.{Environment}.json` (committed environment-specific)
3. `appsettings.local.json` (**gitignored** — dev only, keep **secrets here in dev**)
4. Environment variables (`SECTION__KEY` or `SECTION:KEY` on Windows)
5. `json_settings_names` — see [§3.3](#33-json_settings_names-bulk-secret-block)

### 3.1 Required settings

| Key | Env-var form | Required in | Example / default | Purpose |
|---|---|---|---|---|
| `ConnectionStrings:AuthDbConnection` | `ConnectionStrings__AuthDbConnection` | **all envs** | `Host=db;Port=5432;Database=auth;Username=auth;Password=…` | PostgreSQL connection |
| `DataProtectionSettings:CertificateBase64` | `DataProtectionSettings__CertificateBase64` | **production** | Base64 of a PKCS#12 PFX (see §6.1) | Wraps DP keys at rest |
| `DataProtectionSettings:CertificatePassword` | `DataProtectionSettings__CertificatePassword` | if cert encrypted | — | PFX password |
| `DataProtectionSettings:UnprotectCertificates[n]:Base64` / `Password` | — | during cert rotation | — | Old certs kept for decrypting legacy keys (see §6.2) |
| `CorsSettings:AllowedOrigins` (array) | `CorsSettings__AllowedOrigins__0`, `…__1`, … | **production** | `["https://app.example.com"]` | Allowed browser origins. **Empty array → allow-all + credentials** (dev only). Same list is the whitelist for invite `returnUrl`. |
| `SmtpSettings:Host` / `Port` / `User` / `Password` / `From` | `SmtpSettings__Host` etc. | when `UseMockEmailSender=false` | — | Outgoing email |
| `ReCaptchaSettings:SiteKey` / `SecretKey` | `ReCaptchaSettings__SecretKey` etc. | **production** | — | Google reCAPTCHA v2/v3. **Service silently no-ops when empty.** |
| `InitialAdminUser:Email` | `InitialAdminUser__Email` | bootstrap | `admin@example.com` | Created on startup if missing (EmailConfirmed=true) and assigned `Admin` role |

### 3.2 Feature flags (`AppSettings`)

| Flag | Default | Description |
|---|---|---|
| `UseMockEmailSender` | `false` | Writes emails to log instead of SMTP. **Never set to `true` in prod** — password-reset tokens never reach the user. |
| `UseMockSmsSender` | `false` | Writes SMS to log. A real SMS provider is **not implemented** — keep `UseMockSmsSender=true` OR keep `IsPhoneConfirmationEnabled=false`. |
| `IsPhoneConfirmationEnabled` | `false` | Gate phone-confirmation flow. Requires a real SMS sender when `true`. |
| `IsDeletePersonalDataEnabled` | `false` | Whether the self-service "Delete account" UI is reachable. |

### 3.3 `json_settings_names` bulk secret block

For environments that prefer a single blob of secrets over dozens of env vars (e.g. AWS Secrets Manager or a single Kubernetes Secret):

```bash
# 1. Point at a list of env vars that each contain a full JSON document.
export json_settings_names='["SECRETS_BLOCK_A","SECRETS_BLOCK_B"]'

# 2. Put JSON documents into those env vars:
export SECRETS_BLOCK_A='{"ConnectionStrings":{"AuthDbConnection":"Host=..."},"SmtpSettings":{"Host":"..."}}'
export SECRETS_BLOCK_B='{"DataProtectionSettings":{"CertificateBase64":"..."}}'
```

Each referenced variable is parsed as a JSON object and merged into `IConfiguration` in order (last one wins on conflicts). This runs **after** plain env vars, so it wins on conflicts. Use whichever layering your platform prefers.

### 3.4 Secrets hygiene

- `appsettings.local.json` is `.gitignored` — use it in dev only.
- **Never commit real secrets.** Build artifacts (`bin/`) mirror config files — do not ship a container image built on top of a dev checkout without pruning `bin/`.
- Rotate any secret that has ever been seen outside the secrets store (includes dev boxes, screenshare, tickets).

---

## 4. Deployment

### 4.1 Docker

The project ships a multi-stage [Dockerfile](Dockerfile) producing an Alpine-based runtime image:

```bash
# Build (from src/ root — Dockerfile copies across projects)
docker build -f Services/Auth/Auth.Web/Dockerfile -t auth-web:latest .

# Run
docker run --rm -p 8080:8080 -p 8081:8081 \
  -e ConnectionStrings__AuthDbConnection="Host=db;Database=auth;Username=auth;Password=…" \
  -e DataProtectionSettings__CertificateBase64="$(cat dp-cert-base64.txt)" \
  -e DataProtectionSettings__CertificatePassword="your-password" \
  -e InitialAdminUser__Email=admin@example.com \
  -e ASPNETCORE_ENVIRONMENT=Production \
  auth-web:latest
```

Image runs as non-root (`USER app`). Ports 8080 (HTTP) / 8081 (HTTPS) exposed; HTTPS should be terminated at the reverse proxy in most deployments.

### 4.2 Startup sequence

`Plugins.StartupRunner` runs all `IStartupTask` sequentially, ordered by `Order`. Each task takes a PostgreSQL advisory lock so **multi-instance deployments are safe** — only one pod executes each task, the rest wait then skip.

| Order | Task | Action | Failure behaviour |
|---|---|---|---|
| 10 | `RunMigrationsTask` | `Database.MigrateAsync()` | Hard fail — pod won't start |
| 15 | `SeedAdminRoleTask` | Create `Admin` role; create user from `InitialAdminUser:Email` if missing; assign role | Log error; continue |
| 20 | `RotateDataProtectionKeysTask` | Rotate if newest key > 320d; create one if none | Hard fail if DB unreachable |
| 30 | `RotateSigningKeysTask` | Rotate RSA 2048 signing + encryption keys (140d); load all valid keys into in-memory `SigningKeyStore` | Hard fail |
| 35 | `SeedOidcScopesTask` | Seed `openid` / `profile` / `email` / `phone` / `roles` / `offline_access` / `auth/admin.users` | Log error; continue |
| 40 | `SeedOidcClientsTask` | **Development only** — seeds `default_client` (public, PKCE) | Dev only |

After all startup tasks complete, `ConfigureOpenIddictServerOptions` (PostConfigureOptions) reads `SigningKeyStore` and hands keys to OpenIddict — this happens lazily on the first request. **Empty store ⇒ OpenIddict falls back to development certificates with a warning.** A fresh production DB with no X509 cert and auto-migration running will hit this state during the first request before `RotateSigningKeysTask` has written keys — verify logs for this warning after first deploy.

### 4.3 Zero-downtime migrations

Migrations auto-apply on every startup. Rolling deploys mean a v1 pod may briefly see v2 schema. Rules:
1. Keep migrations backward-compatible (add-only, nullable-add, rename via copy-then-drop across 2 deploys).
2. For destructive or non-null-add migrations — pause rollout, run migrations from a dedicated job, then proceed.
3. Consider splitting: build container with `dotnet ef migrations bundle` and invoke it as an init container instead of auto-running at app start.

### 4.4 Reverse proxy / TLS

- `UseForwardedHeaders` is configured with `ForwardLimit = 2` and `KnownProxies.Clear()` / `KnownIPNetworks.Clear()` — **this currently trusts any caller to set `X-Forwarded-For/Proto/Host`**. In production either:
  - Run behind an ingress that strips/overrides client-supplied `X-Forwarded-*` headers (typical behaviour of nginx, Envoy, AWS ALB), **and** make the pod un-reachable except through ingress.
  - Or hard-code proxy CIDRs in `Program.cs` before deploy: `options.KnownIPNetworks.Add(IPNetwork.Parse("10.0.0.0/8"))`.
- HSTS (`UseHsts`) is on; `UseHttpsRedirection` is on.
- Cookie `Identity.StatusMessage`: HttpOnly, SameSite=Strict, MaxAge=5s.

### 4.5 Health

- `GET /health` — public, no auth, returns `Healthy` (no checks registered yet). Safe for k8s `livenessProbe`. Before adding real DB / SMTP checks, split into `/health/live` (public) + `/health/ready` (private).

### 4.6 Logs

- Serilog JSON → stdout, single sink. Property `Application = "LayeredTemplate.Auth"`.
- Request logging via `UseSerilogRequestLogging` — enriches with IP, Referer, User claims.
- Admin-facing actions are logged at `Information` / `Warning` (e.g. `Admin {AdminId} updated profile for user {UserId}.`) — use these as audit trail.
- Levels are `ReadFrom.Configuration`, so the `Serilog` section of `appsettings.json` is the knob.

---

## 5. Operations

### 5.1 Bootstrapping the first admin

Set `InitialAdminUser:Email=admin@example.com` **before first deploy**. On startup `SeedAdminRoleTask` will:
1. Create the `Admin` role if missing.
2. Create the user (EmailConfirmed=true) if missing — **without a password**, to force first-sign-in via passkey, external provider, or password-reset flow.
3. Assign `Admin`.

Subsequent admins are added through `/admin/users/<id>` → Roles.

### 5.2 Background jobs

- `OpenIddictCleanupService` (BackgroundService) — runs every 12h (initial delay 15 min), prunes expired tokens + ad-hoc authorizations older than 14 days. Advisory lock → safe on multi-instance.

### 5.3 Common operations

| Scenario | Action |
|---|---|
| Rotate DP master cert | See [§6.2](#62-rotating-the-dataprotection-master-certificate) |
| Add a new admin API client (S2S) | Admin UI → Applications → Create (confidential + `client_credentials` + scope `auth/admin.users`) |
| Revoke an OpenIddict client | Admin UI → Applications → Delete (or set to Disabled — current UI removes; issue new secret on compromise) |
| Revoke a single user's tokens | Admin UI → Users → (invalidate) — rotate user's security stamp by updating email/password |
| Add a new OIDC scope for a downstream microservice | Seed in `SeedOidcScopesTask` **or** admin UI → Scopes (when implemented); give it a Resource URI (`api://<service>`) so its `aud` is populated |
| Add an invite flow for a partner | App.Web calls `POST /api/admin/users/{id}/invite-token` via `Auth.ApiClient`, composes own email with `/account/accept_invite?userId=…&code=…&returnUrl=…` |

### 5.4 Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| `No signing/encryption keys in store. OpenIddict will use development certificates.` (warning) | First-request race after fresh DB; `RotateSigningKeysTask` failed or was skipped | Check logs for the startup task; ensure DB connectivity and advisory lock library loaded |
| Every user sees `Error: Invalid verification code` on phone change | `IsPhoneConfirmationEnabled=true` with `MockSmsSender` — codes go to log, not phones | Flip `IsPhoneConfirmationEnabled=false` or wire a real SMS sender |
| Pods crash-loop with `CryptographicException` decrypting DP keys | Master cert changed without adding old cert to `UnprotectCertificates` | Add previous PFX to `UnprotectCertificates[]` for at least `KeyLifetimeDays` (360d). Never delete a cert that may have active keys. |
| Admin API returns `ID2051 insufficient_access` | Client's scope doesn't match the policy | Check the client has `auth/admin.users` permission AND the token request includes `scope=auth/admin.users` |
| `/connect/authorize` shows branded "Authorization failed" | Client-side config issue (unknown client_id, mismatched redirect_uri, unauthorised scope) — NOT a server fault | See [§4.4](#44-reverse-proxy--tls) logs; diagnose via `error_description` query |

---

## 6. Commands

### 6.1 Generate a self-signed DataProtection master certificate

For dev / lab / bootstrapping the first production cert:

```bash
openssl req -x509 -newkey rsa:2048 \
  -keyout dp-key.pem -out dp-cert.pem \
  -days 1825 -nodes -subj "/CN=DataProtection"

openssl pkcs12 -export \
  -out dp-cert.pfx -inkey dp-key.pem -in dp-cert.pem \
  -passout pass:your-password
```

```powershell
# PowerShell — produce a Base64 blob to put into DataProtectionSettings:CertificateBase64
[Convert]::ToBase64String([IO.File]::ReadAllBytes("dp-cert.pfx")) | Out-File -Encoding ASCII "dp-cert-base64.txt"
```

In production use a cert from your PKI / HSM instead of self-signed. The cert is only used locally for wrapping — it does not need to be trusted by clients.

### 6.2 Rotating the DataProtection master certificate

**The master certificate is the only material that does not auto-rotate.** When its `notAfter` approaches, or after a suspected compromise:

1. Generate a new PFX (see §6.1 or from your PKI).
2. Move the existing cert's Base64 into `DataProtectionSettings.UnprotectCertificates[]`:
   ```jsonc
   {
     "DataProtectionSettings": {
       "CertificateBase64": "<NEW_CERT_BASE64>",
       "CertificatePassword": "<NEW_PASSWORD>",
       "UnprotectCertificates": [
         { "Base64": "<OLD_CERT_BASE64>", "Password": "<OLD_PASSWORD>" }
       ]
     }
   }
   ```
3. Deploy. New DP keys minted after this restart will be wrapped by the new cert; old keys (still in `data_protection_keys`) continue to decrypt via the old cert from `UnprotectCertificates`.
4. **Keep the old cert in `UnprotectCertificates` for at least `KeyLifetimeDays` (360d)** — any key wrapped by it may still be needed to decrypt persisted tokens / cookies. Removing it sooner will cause decryption failures and force users to re-authenticate.
5. Once all DP keys wrapped by the old cert have expired, it can be removed from `UnprotectCertificates`.

### 6.3 Migrations

Add a migration:

```bash
dotnet ef migrations add <Name> -c AuthDbContext -o Infrastructure/Data/Migrations
```

The initial migration can be created with:

```bash
dotnet ef migrations add Init -c AuthDbContext -o Infrastructure/Data/Migrations
```

Migrations are applied automatically on startup — no manual `database update` needed. See [§4.3](#43-zero-downtime-migrations) for rolling-deploy caveats.

### 6.4 Local run

```bash
# From repo src/ root
dotnet run --project Services/Auth/Auth.Web
```

The project auto-loads `appsettings.local.json` (gitignored) — put dev secrets there.

---

## 7. Production hardening checklist

Go through this list before shipping. Items marked **blocker** will either break security or functionality in prod.

- [ ] **blocker** Configure `DataProtectionSettings.CertificateBase64` with a real (non-self-signed) certificate from your PKI / HSM.
- [ ] **blocker** Populate `CorsSettings.AllowedOrigins` with explicit origins. Empty array = allow-all + credentials.
- [ ] **blocker** Populate `ReCaptchaSettings.SiteKey` / `SecretKey` (or remove the bot-protected pages from the public perimeter). Empty keys silently disable captcha.
- [ ] **blocker** Set `UseMockEmailSender=false` and configure `SmtpSettings`; otherwise password reset + email confirmation break silently.
- [ ] **blocker** Restrict `UseForwardedHeaders` to the known ingress CIDR (see §4.4). Current defaults trust any caller's `X-Forwarded-*`.
- [ ] **blocker** Set `InitialAdminUser:Email` before first deploy, or bootstrap an admin via SQL.
- [ ] Raise password policy: `options.Password.RequiredLength` ≥ 12 in `Infrastructure/Identity/ServicesExtensions.cs`.
- [ ] Enable account lockout: switch `lockoutOnFailure: true` in `Login.razor.cs`, `ResetPassword.razor.cs`, `AcceptInvite.razor.cs` and configure `options.Lockout` in `AddIdentityCore`.
- [ ] Add ASP.NET Core rate limiter for `/account/login`, `/connect/token`, `/account/forgot_password`, `/account/resend_email_confirmation`.
- [ ] Consider shortening invite-token lifetime (`InviteTokenSettings.Lifespan`) below 30d.
- [ ] Reject password reset for unconfirmed emails (`ResetPassword.razor.cs`).
- [ ] Split `/health` into `/health/live` (public) + `/health/ready` (internal) before wiring real DB / SMTP checks.
- [ ] `DisableAccessTokenEncryption()` is a deliberate template default — re-enable encryption if stateless JWT validation is not required downstream.
- [ ] Plan out cert rotation schedule **now**, before the cert's `notAfter` (§6.2). Put a calendar reminder 60 days ahead.
- [ ] Review `SigningKeyStore` warning on first request after deploy — should never see "development certificates" in prod logs.

---

## 8. Risks & caveats (accepted template defaults)

These are design choices; change only if your threat model requires it:

| Decision | Rationale | Change when |
|---|---|---|
| Access tokens NOT encrypted (`DisableAccessTokenEncryption`) | Stateless JWT validation via JWKS in downstream microservices without Auth.Web roundtrip | You need to hide claims from transport-layer inspectors |
| Signing RSA-2048 | Widely interoperable; 140-day rotation caps exposure | Move to RSA-3072 / EdDSA for long-horizon deployments |
| Auto-migrate on startup | Simplifies single-instance ops | You need strict zero-downtime — split into dedicated job |
| Account lockout disabled | Avoids lockout-based DoS | You prefer lockout over rate limiting |
| No real SMS provider | Template ships only `MockSmsSender`; phone confirmation off by default | You need phone MFA — wire a provider + set `IsPhoneConfirmationEnabled=true` |
| DP keys 320d/360d (vs ASP.NET default 90d/90d) | Reduces key-rotation churn; assumes service is restarted at least every ~40 days (CI/CD cadence) so the successor key lands within the overlap | Tighten if key compromise is a top risk or deploys are rarer than monthly |

---

## 9. Reference

- `src/Services/Auth/Auth.Web/CLAUDE.md` — detailed architecture / code-navigation guide.
- [OpenIddict docs](https://documentation.openiddict.com/) — behind every OIDC decision here.
- [RFC 9068](https://www.rfc-editor.org/rfc/rfc9068) — JWT profile for OAuth 2.0 access tokens (`typ=at+jwt`).
- [OIDC Core §5.4](https://openid.net/specs/openid-connect-core-1_0.html#ScopeClaims) — scope-to-claim mapping rule.
- [ASP.NET Core DataProtection — Persistence models](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview).
