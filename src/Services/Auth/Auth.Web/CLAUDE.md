# Auth.Web — OIDC/OAuth 2.0 Authorization Server

Standalone OpenID Connect provider на ASP.NET Core 10 + Blazor Server. UI аккаунта и эндпоинты OIDC живут в одном хосте. Не зависит от `Services/App` — самостоятельный сервис с собственной БД.

## Стек

| Назначение | Пакет |
|-----------|-------|
| OIDC/OAuth сервер | `OpenIddict.AspNetCore` + `OpenIddict.EntityFrameworkCore` |
| Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0 (SchemaVersion3 — passkeys) |
| Хранилище | PostgreSQL через `Npgsql.EntityFrameworkCore.PostgreSQL` + `EFCore.NamingConventions` (snake_case) |
| UI | Blazor Server (Razor Components, interactive none) |
| Внешние провайдеры | `AspNet.Security.OAuth.GitHub`, `AspNet.Security.OAuth.Yandex` |
| Data Protection | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` |
| Email | MailKit (`EmailSender` / `MockEmailSender` по `AppSettings:UseMockEmailSender`) |
| SMS | только `MockSmsSender` (реальный провайдер не подключён) |
| Captcha | Google reCAPTCHA v2/v3 (`ReCaptchaService`) |
| Distributed lock | Postgres advisory locks через `DistributedLock.Postgres` |
| 2FA | TOTP + recovery codes (`EnableAuthenticator.razor`), QRCoder |
| Логи | Serilog (JSON в Console) + Serilog.AspNetCore request logging |
| Health | `AspNetCore.HealthChecks.UI.Client` на `/health` |

## Структура

```
Auth.Web/
├── Program.cs                  — хост, middleware, DI (top-level)
├── Controllers/
│   ├── ConnectController.cs    — /connect/authorize|token|userinfo|logout (OIDC endpoints)
│   └── AccountController.cs    — /account/logout, /account/external_login, link, GDPR export
├── Components/                 — Blazor UI
│   ├── Account/Pages/          — Login, Register, ForgotPassword, ResetPassword, 2fa, ExternalLoginCallback
│   ├── Account/Pages/Manage/   — смена пароля, email, 2FA, recovery codes, personal data, connected accounts
│   └── Account/Shared/         — ReCaptcha, ExternalProviders, StatusMessage, RedirectToLogin
│       IdentityRedirectManager — безопасные редиректы (anti-open-redirect) + статус-кука (5s, HttpOnly, Strict)
└── Infrastructure/
    ├── Data/
    │   ├── Contexts/AuthDbContext.cs    — IdentityDbContext + IDataProtectionKeyContext + OpenIddict, default schema "auth"
    │   ├── Entities/ApplicationUser.cs  — IdentityUser, Id = GUID v7
    │   ├── Entities/SigningCredential.cs — RSA-ключи OpenIddict (зашифрованные)
    │   └── EntityConfigurations/         — конфигурации OpenIddict + Identity + SigningCredential + DataProtectionKey
    ├── Identity/                — AddIdentityCore с password policy
    ├── OpenIddict/              — сервер, SigningKeyStore (singleton), ConfigureOpenIddictServerOptions (IPostConfigureOptions)
    ├── DataProtection/          — DisableAutomaticKeyGeneration + PersistKeysToDbContext + optional X509 protection
    ├── Locks/                   — ILockProvider/PostgresLockProvider (advisory locks)
    ├── Email/                   — EmailSender (MailKit/SMTP), UserEmailSender, Mock
    ├── Sms/                     — только MockSmsSender
    ├── ReCaptcha/               — ReCaptchaService (IsEnabled = обе ключа заданы)
    ├── Cors/                    — default policy; AllowedOrigins=[] ⇒ AllowAll (опасно в проде)
    ├── Logging/                 — UseSerilogRequestLogging + enrichers (IP, Referer, User)
    ├── Options/                 — AppSettings, SmtpSettings, ReCaptchaSettings, CorsSettings, DataProtectionSettings
    └── StartupTasks/            — все критические бэкграунд-init задачи
```

## Порядок запуска (IStartupTask)

Плагин `Plugins.StartupRunner` выполняет задачи последовательно по `Order`. Каждая берёт Postgres advisory lock до выполнения — безопасно для multi-instance.

| Order | Task | Что делает |
|-------|------|-----------|
| 10 | `RunMigrationsTask` | `Database.MigrateAsync()` под локом `run-migrations:auth-db-context` |
| 20 | `RotateDataProtectionKeysTask` | Создаёт/ротирует DP-ключ (lifetime 360d, rotation 180d) |
| 30 | `RotateSigningKeysTask` | Ротирует RSA signing/encryption ключи OpenIddict (rotation 90d, maxAge 180d) и заполняет `SigningKeyStore` |
| 40 | `SeedOidcClientsTask` | Seed'ит `default_client` (public, PKCE, redirects на `localhost:3061/3062`) |

`ConfigureOpenIddictServerOptions` (IPostConfigureOptions) читает `SigningKeyStore` и добавляет ключи в `OpenIddictServerOptions.SigningCredentials/EncryptionCredentials` перед первым запросом. Если стор пуст — fallback на dev-сертификаты с warning.

## OIDC сервер — ключевые настройки

Файл: `Infrastructure/OpenIddict/ServicesExtensions.cs`

- Endpoints: `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/logout` (все с passthrough в MVC-контроллер)
- Flows: **AuthorizationCode + RefreshToken**, **PKCE required** (`RequireProofKeyForCodeExchange`)
- Lifetimes: AccessToken = 1h, IdentityToken = 1h, RefreshToken = 30d
- Scopes: `openid`, `profile`, `email`
- Токены подписываются RSA-ключами из БД (RS256), шифруются (RSA-OAEP + A256CBC-HS512)
- Claims в токенах выставляет `ConnectController`: `sub`, `name`, `email` (destinations: AccessToken+IdentityToken)

## Identity / безопасность

- Password: digit+lower+upper+nonAlphanum, min 6, uniqueChars 1
- `RequireConfirmedAccount = true`
- Schema v3 (поддержка passkeys, `UserPasskey`)
- 2FA: TOTP через `EnableAuthenticator`, recovery codes, `LoginWith2fa` / `LoginWithRecoveryCode`
- External: GitHub (`user:email`), Yandex (callback `/signin-yandex`) — клиенты читаются из `GitHub:ClientId/Secret` и `Yandex:ClientId/Secret`
- reCAPTCHA на Login (другие формы — при необходимости через `<ReCaptcha>`). Сервис disabled если ключи пустые → `ValidateAsync` возвращает `true`
- `lockoutOnFailure: false` в `Login.razor.cs:48` — account lockout по неудачным попыткам **выключен**
- `IdentityRedirectManager.RedirectTo` защищает от open-redirect: не-относительные URI проходят через `ToBaseRelativePath`
- Cookie `Identity.StatusMessage`: HttpOnly, SameSite=Strict, MaxAge=5s
- ForwardedHeaders включены для работы за прокси (`ForwardLimit = 2`, `KnownProxies` очищены — проверяй периметр!)
- HSTS + HTTPS redirection + Antiforgery включены
- `[ValidateAntiForgeryToken]` на POST-экшенах AccountController

## Конфигурация

Загрузка (`Program.cs:66`): `appsettings.json` → `appsettings.{Env}.json` → `appsettings.local.json` → env vars → **env vars из JSON** (через `json_settings_names` — массив имён env-переменных, каждая содержит JSON-документ, который мёржится в конфигурацию). Последний механизм используется для деплоя секретов одним блоком.

Секции:
- `ConnectionStrings:AuthDbConnection` — Postgres
- `DataProtectionSettings` — `CertificateBase64` / `CertificatePassword` + массив `UnprotectCertificates` для ротации X509
- `ReCaptchaSettings` — `SiteKey` / `SecretKey` (пустые → reCAPTCHA disabled)
- `AppSettings` — `UseMockEmailSender`, `UseMockSmsSender`, `EnablePhoneConfirmation`, `EnableDeletePersonalData`
- `CorsSettings:AllowedOrigins` — массив; **пустой массив = AllowAll + credentials** (использовать только для dev)
- `SmtpSettings` — `From/Host/Port/User/Password`
- `GitHub:ClientId/ClientSecret`, `Yandex:ClientId/ClientSecret` — external providers

**Секреты не коммитить.** `appsettings.local.json` в `.gitignore` (строка 367). В проде — env vars или `json_settings_names`.

## Данные

- PostgreSQL, default schema `auth`, snake_case, все string-колонки по умолчанию 256 символов, enum → string
- История миграций: `auth.__ef_auth_migrations`
- `DbContextPool` + split queries
- Таблицы Identity (`aspnet_user*`, `aspnet_role*`), OpenIddict (`openiddict_*`), `data_protection_keys`, `signing_credentials`

### Миграции

```bash
dotnet ef migrations add <Name> -c AuthDbContext -o Infrastructure/Data/Migrations -s Auth.Web.csproj -p Auth.Web.csproj
```

Миграции применяются автоматически через `RunMigrationsTask` на старте.

## Криптография

- **Data Protection keys** — создаются через `IKeyManager.CreateNewKey` в `RotateDataProtectionKeysTask`, хранятся в `data_protection_keys`. Опционально обёрнуты X509-сертификатом (`ProtectKeysWithCertificate`) — `UnprotectCertificates` позволяет ротировать сертификат без потери старых ключей.
- **OpenIddict signing/encryption keys** — RSA 2048, генерируются в `RotateSigningKeysTask`, параметры сериализуются в JSON, шифруются через `IDataProtector("OpenIddict.SigningCredentials")` и кладутся в `signing_credentials.KeyData` → зависят от DP-ключей, т.е. потеря DP-ключей = потеря signing-ключей.
- Генерация self-signed DP-cert — см. `README.md` (openssl + PKCS12).

## Команды

```bash
# Запуск
dotnet run --project Services/Auth/Auth.Web

# Docker (из src/)
docker-compose up auth-web   # если описан в docker-compose.yml

# Миграции (из src/Services/Auth/Auth.Web/)
dotnet ef migrations add <Name> -c AuthDbContext -o Infrastructure/Data/Migrations
dotnet ef database update -c AuthDbContext
```

## Health & наблюдаемость

- `GET /health` — `UIResponseWriter.WriteHealthCheckUIResponse` (JSON), регистраций в `AddHealthChecks` пока нет, добавлять отдельно
- Serilog: JSON в Console, `Application=LayeredTemplate.Auth`; request logging с IP/Referer/User-claims
- Exception page: `/error`, 404 re-execute на `/not_found`

## Что важно помнить при правках

1. **OIDC-ключи инициализируются только через startup task** — если менять schedule, помни, что пустой `SigningKeyStore` → OpenIddict переходит на dev-сертификаты (warning, прод-небезопасно).
2. **Password lockout выключен** в `Login.razor.cs:48` (`lockoutOnFailure: false`) — если включать, проверь UI lockout и reCAPTCHA-интеграцию.
3. **CORS AllowAll при пустом массиве** (`Cors/ServicesExtensions.cs:17`) — в staging/prod обязательно задавать `CorsSettings:AllowedOrigins`.
4. **ForwardedHeaders** принимает доверие ко всем IP (`KnownIPNetworks/KnownProxies` очищены) — ок только если приложение за доверенным reverse-proxy.
5. **Seed `default_client`** (PKCE public) — localhost-only, не подходит для прода; новые клиенты добавлять через `IOpenIddictApplicationManager` в отдельной задаче/админке.
6. **SMS** — реальный провайдер не реализован, `ISmsSender` = `MockSmsSender`. `EnablePhoneConfirmation` рассчитан на добавление реализации.
7. **Миграции автоприменяются** на каждом старте — в проде для нулевого downtime разворачивать non-breaking миграции отдельным шагом.
8. **Bump пакетов OpenIddict** — проверять совместимость с Identity schema v3 и claim destinations.
