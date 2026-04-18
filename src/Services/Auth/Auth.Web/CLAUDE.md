# Auth.Web — OIDC/OAuth 2.0 Authorization Server

Standalone OpenID Connect provider на ASP.NET Core 10 + Blazor Server. UI аккаунта, админ-дашборд и эндпоинты OIDC живут в одном хосте. Не зависит от `Services/App` — самостоятельный сервис с собственной БД.

## Стек

| Назначение | Пакет |
|-----------|-------|
| OIDC/OAuth сервер | `OpenIddict.AspNetCore` (Server) + `OpenIddict.Validation.AspNetCore` (Bearer для собственного admin API) + `OpenIddict.EntityFrameworkCore` |
| Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0 (SchemaVersion3 — passkeys), `ApplicationUser` с `FirstName`/`LastName`, `ApplicationRole` |
| Хранилище | PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL` + `EFCore.NamingConventions` — snake_case) |
| UI | Blazor Server (Razor Components, interactive none, SSR) |
| Внешние провайдеры | `AspNet.Security.OAuth.GitHub`, `AspNet.Security.OAuth.Yandex` |
| Data Protection | `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore` |
| Email | MailKit (`EmailSender` / `MockEmailSender` по `AppSettings:UseMockEmailSender`) |
| SMS | только `MockSmsSender` (реальный провайдер не подключён) |
| Captcha | Google reCAPTCHA v2/v3 (`ReCaptchaService`), стоит на всех публичных формах, которые шлют email |
| Distributed lock | Postgres advisory locks через `DistributedLock.Postgres` |
| 2FA | TOTP + recovery codes (`EnableAuthenticator.razor`), QRCoder |
| Background scheduling | `IHostedService` + `PeriodicTimer` (см. `Infrastructure/BackgroundTasks/`) |
| Логи | Serilog (JSON в Console) + Serilog.AspNetCore request logging |
| Health | `AspNetCore.HealthChecks.UI.Client` на `/health` |

## Структура

```
Auth.Web/
├── Program.cs                      — хост, middleware, DI (top-level)
├── Controllers/
│   ├── ConnectController.cs        — /connect/authorize | token | userinfo | logout (OIDC endpoints)
│   ├── AccountController.cs        — /account/logout (cookie), /account/external_login, link, GDPR export
│   └── Api/Admin/                  — S2S admin API (Bearer + scope admin.users)
│       ├── AdminUsersController.cs — GET/POST/PATCH/DELETE /api/admin/users[/{id}] + /invite-token
│       └── AdminUsersModels.cs     — DTO (UserResponse, Create/UpdateUserRequest, InviteTokenResponse)
├── Components/                     — Blazor UI (SSR)
│   ├── Account/Pages/              — Login, Register, ForgotPassword, ResetPassword, ResendEmailConfirmation,
│   │                                 ConfirmEmail, ExternalLoginCallback, AcceptInvite, LogoutConfirmation,
│   │                                 Login/Recovery with 2fa
│   ├── Account/Pages/Manage/       — Profile (Index), EditProfile (name), EditEmail, EditPhone,
│   │                                 ChangePassword, 2FA setup/recovery, ConnectedAccounts, PersonalData,
│   │                                 DeletePersonalData
│   ├── Account/Shared/             — ReCaptcha, ExternalProviders, StatusMessage, RedirectToLogin,
│   │                                 IdentityRedirectManager, ManageLayout/Nav
│   └── Admin/                      — админ-дашборд (cookie + [Authorize(Policy=Admin)])
│       ├── Shared/                 — AdminLayout, AdminNavMenu
│       ├── Applications/           — Index, Create, Edit, Delete + ApplicationFormFields, AdminScopeHelper
│       └── Users/                  — Index (search), Details (view), Edit (form), Create, Delete
└── Infrastructure/
    ├── Data/
    │   ├── Contexts/AuthDbContext.cs    — IdentityDbContext<ApplicationUser, ApplicationRole> + IDataProtectionKeyContext + OpenIddict, default schema "auth"
    │   ├── Entities/ApplicationUser.cs  — IdentityUser, Id = GUID v7, FirstName/LastName (PersonalData)
    │   ├── Entities/ApplicationRole.cs  — IdentityRole, Id = GUID v7
    │   ├── Entities/SigningCredential.cs — RSA-ключи OpenIddict (зашифрованные)
    │   └── EntityConfigurations/         — OpenIddict + Identity + SigningCredential + DataProtectionKey
    ├── Identity/                    — Roles, Scopes, Policies, Invite token provider
    │   ├── ServicesExtensions.cs    — AddIdentityCore + AddRoles + AddTokenProvider("Invite")
    │   ├── AppRoles.cs              — константа "Admin"
    │   ├── AppScopes.cs             — AdminUsers="admin.users", Roles="roles", + AppAuthorizationPolicies.ScopeAdminUsers
    │   └── InviteTokenSettings.cs   — ProviderName/Purpose/Lifespan(30d) для invite-токенов
    ├── OpenIddict/                  — OIDC server + validation
    │   ├── ServicesExtensions.cs    — AddServer(...) + AddValidation(UseLocalServer) — Auth.Web валидирует свои же токены
    │   ├── SigningKeyStore.cs       — singleton, заполняется RotateSigningKeysTask
    │   └── ConfigureOpenIddictServerOptions.cs — IPostConfigureOptions читает store, применяет ключи
    ├── DataProtection/              — DisableAutomaticKeyGeneration + PersistKeysToDbContext + optional X509
    ├── BackgroundTasks/             — периодические фоновые задачи
    │   └── OpenIddictCleanupService.cs — PeriodicTimer, чистит expired tokens/authorizations каждые 12ч
    ├── Locks/                       — ILockProvider/PostgresLockProvider (advisory locks)
    ├── Email/                       — EmailSender (MailKit/SMTP), UserEmailSender, Mock
    ├── Sms/                         — только MockSmsSender
    ├── ReCaptcha/                   — ReCaptchaService (IsEnabled = оба ключа заданы)
    ├── Cors/                        — default policy; AllowedOrigins=[] ⇒ AllowAll (опасно в проде). Используется и как whitelist для accept_invite returnUrl.
    ├── Logging/                     — UseSerilogRequestLogging + enrichers (IP, Referer, User)
    ├── Options/                     — AppSettings, SmtpSettings, ReCaptchaSettings, CorsSettings, DataProtectionSettings, InitialAdminUserSettings
    └── StartupTasks/                — все критические init-задачи
Migrations/                          — EF миграции (таблица __ef_auth_migrations в схеме auth)
```

## Порядок запуска (IStartupTask)

Плагин `Plugins.StartupRunner` выполняет задачи последовательно по `Order`. Каждая берёт Postgres advisory lock — безопасно для multi-instance.

| Order | Task | Что делает |
|-------|------|-----------|
| 10 | `RunMigrationsTask` | `Database.MigrateAsync()` |
| 15 | `SeedAdminRoleTask` | Создаёт роль `Admin`; если `InitialAdminUser:Email` задан и юзера нет — создаёт его (EmailConfirmed=true); назначает роль `Admin` |
| 20 | `RotateDataProtectionKeysTask` | DP-ключ rotation 180d, lifetime 360d |
| 30 | `RotateSigningKeysTask` | RSA signing/encryption ключи rotation 90d, maxAge 180d; заполняет `SigningKeyStore` |
| 35 | `SeedOidcScopesTask` | Сид скоупов: openid/profile/email/phone/offline_access/roles/admin.users |
| 40 | `SeedOidcClientsTask` | Только в `env.IsDevelopment()` — сид `default_client` (public, PKCE) |

`ConfigureOpenIddictServerOptions` (IPostConfigureOptions) читает `SigningKeyStore` и применяет ключи к OpenIddict перед первым запросом. Пустой стор → fallback на dev-сертификаты с warning.

## OIDC сервер — ключевые настройки

Файл: `Infrastructure/OpenIddict/ServicesExtensions.cs`

- Endpoints: `/connect/authorize`, `/connect/token`, `/connect/userinfo`, `/connect/logout` (все с passthrough в `ConnectController`)
- Flows: **AuthorizationCode + RefreshToken + ClientCredentials**, **PKCE required** для AuthCode
- Access token encryption отключён (`DisableAccessTokenEncryption()`) — токены читаемы как JWT, для дебага
- Lifetimes: AccessToken = 1h, IdentityToken = 1h, RefreshToken = 30d
- Registered scopes: `openid`, `profile`, `email`, `phone`, `roles`, `offline_access`, `admin.users` (resource `api://auth-admin`)
- Server + Validation сосуществуют: `AddValidation(UseLocalServer)` регистрирует Bearer-схему для собственного admin API
- **Error passthrough** (`EnableErrorPassthrough()`) — OpenIddict-рождённые ошибки валидации (unknown client, invalid scope, redirect_uri mismatch, …) проходят через MVC-пайплайн вместо встроенного plaintext-body. Обработка в `ConnectController`:
  - `Authorize` / `LogoutGet` (user-facing) → `TryBuildAuthorizeErrorRedirect` редиректит на `/account/authorize_error?error=...&error_description=...&client_id=...` — брендированная Razor-страница [AuthorizeError.razor](Components/Pages/AuthorizeError.razor) (живёт в общем `Components/Pages/` рядом с `Error.razor`/`NotFound.razor`, а не в `Account/Pages/`, т.к. это не часть identity-UI flow)
  - `Exchange` / `Userinfo` (machine-to-machine) → `TryBuildOAuthErrorJson` отдаёт стандартный RFC 6749 §5.2 JSON (`{error, error_description, error_uri}`), статус 401 для `invalid_client`, иначе 400
  - Стандартный OAuth flow — когда OpenIddict может вернуть ошибку клиенту через валидный `redirect_uri` — не трогается: error улетает в SPA/клиент стандартным редиректом с error в query, наши fallback'и срабатывают только когда redirect к клиенту невозможен.

### ConnectController — клеймы и destinations

- `BuildUserIdentityAsync(user)` — единственное место, где собираются клеймы (sub, name, given_name, family_name, email, email_verified, phone_number, phone_number_verified, role×N). Используется и в `Authorize`, и в auth_code/refresh_token ветке `Exchange`.
- `ResolveDestinations(claim)` — single source of truth для маршрутизации клеймов. **Per OIDC Core §5.4: клеймы эмитятся только при наличии соответствующего scope**, и когда эмитятся — идут в ОБА токена (backend'ы читают из access_token, SPA — из id_token):
  - `sub` → всегда в AT+IT
  - `name`/`given_name`/`family_name` → AT+IT только при scope `profile`
  - `email`/`email_verified` → AT+IT только при scope `email`
  - `phone_number`/`phone_number_verified` → AT+IT только при scope `phone`
  - `role` → AT+IT только при scope `roles`
  - остальные → никуда (не эмитим)
- Токен клиента, запросившего только `openid`, содержит лишь `sub` — минимальный footprint.
- `Userinfo` честно смотрит на `principal.HasScope(...)`, возвращает только разрешённое
- `Exchange` обрабатывает три grant: `client_credentials` (sub=client_id), `authorization_code`, `refresh_token`
- **Logout**: GET с непустым `post_logout_redirect_uri` (OpenIddict валидирует его перед контроллером) → silent logout + редирект. GET без параметров → редирект на `/account/logout_confirmation` (защита от nuisance-CSRF). POST → всегда silent logout.

### Access token формат и валидация для resource-серверов

- **JWT, не encrypted** (`DisableAccessTokenEncryption()` в server options). Любой backend с доступом к JWKS может валидировать самостоятельно — без introspection roundtrip.
- **JWT header `typ=at+jwt`** (RFC 9068). Resource-серверы должны выставлять `ValidTypes = ["at+jwt"]` в JwtBearer — это отсекает id_token, случайно посланный в Authorization header.
- **Identity claims в AT гейтятся по scope** (см. `ResolveDestinations` выше). Backend читает нужное напрямую из токена — `HttpContext.User.FindFirstValue(...)`.

### Resource-based audience для микросервисов

`OpenIddictScopeDescriptor.Resources` на каждом API-скоупе → OpenIddict включает `api://...` URI в `aud` access_token'а. Каждый микросервис в JwtBearer настраивает `ValidAudience = "api://my-service"` → tokens для других сервисов отклоняются.

Правила:
- OIDC-identity скоупы (`openid`, `profile`, `email`, `phone`, `roles`, `offline_access`) — **без resources**. Описывают юзера, не API.
- Кастомные API-скоупы — **с resources**. Пример в [SeedOidcScopesTask](Infrastructure/StartupTasks/SeedOidcScopesTask.cs): `auth/admin.users` имеет resource `api://auth-web`. Закомментированы примеры `app/all.read` / `reports/all`.

Чтобы добавить новый микросервис:
1. В админке создать scope с именем `<service>/<action>` (или через seed-task).
2. Назначить scope resource `api://<service>` (через admin UI или сидом).
3. Клиент (SPA) запрашивает scope в authorize.
4. Resource-сервер в JwtBearer: `ValidAudience = "api://<service>"`, `ValidTypes = ["at+jwt"]`, `Authority = <Auth.Web URL>`.

### Introspection — опциональный путь

OpenIddict server экспортит `/connect/introspect`, но в шаблоне не используется. Нужен только если требуется **real-time revocation** (отозвать AT до его natural expiry): backend POSTит токен + свои client_id/secret → Auth возвращает `{ active, sub, scope, ... }`. Цена — +round-trip на каждый запрос (митигируется локальным кешем на TTL).

Для включения на resource-server стороне:
```csharp
.AddValidation(options => {
    options.SetIssuer("https://auth/");
    options.UseIntrospection().SetClientId("...").SetClientSecret("...");
    options.UseSystemNetHttp();
    options.UseAspNetCore();
});
```

Для обычных бизнес-сервисов (нет требований к мгновенному отзыву, 1h TTL access_token ок) — **JWT-валидация через JWKS** правильнее: stateless, без дополнительной зависимости на Auth при каждом запросе.

## Identity / безопасность

- Password policy: digit+lower+upper+nonAlphanum, min 6, uniqueChars 1
- `RequireConfirmedAccount = true`
- Schema v3 (passkeys через `UserPasskey`)
- Roles: `ApplicationRole`, сид `Admin` через `SeedAdminRoleTask`
- 2FA: TOTP через `EnableAuthenticator`, recovery codes, `LoginWith2fa` / `LoginWithRecoveryCode`
- External: GitHub (`user:email`), Yandex (callback `/signin-yandex`) — клиенты читаются из `GitHub:ClientId/Secret`, `Yandex:ClientId/Secret`. При создании юзера через external login извлекаются FirstName/LastName из claims.
- reCAPTCHA на **всех публичных email-отправляющих формах**: Register, ForgotPassword, ResendEmailConfirmation. Сервис disabled если ключи пустые → `ValidateAsync` возвращает `true`
- `lockoutOnFailure: false` в [Login.razor.cs](Components/Account/Pages/Login.razor.cs) — account lockout по неудачным попыткам **выключен**
- Unconfirmed-email detection на Login: при `IsNotAllowed` + валидный пароль показывается ссылка на Resend (без user-enumeration — только для владельцев правильного пароля)
- Auto-login после password reset; при 2FA → редирект на `/account/login_with_2fa`
- Self-Admin-removal protection: админ не может снять с себя роль Admin (UI + server guard)
- Self-delete protection в админке: нельзя удалить собственный аккаунт
- `IdentityRedirectManager.RedirectTo` защищает от open-redirect: не-относительные URI проходят через `ToBaseRelativePath`
- Cookie `Identity.StatusMessage`: HttpOnly, SameSite=Strict, MaxAge=5s
- ForwardedHeaders включены для работы за прокси (`ForwardLimit = 2`, `KnownProxies` очищены — проверяй периметр!)
- HSTS + HTTPS redirection + Antiforgery включены
- `[ValidateAntiForgeryToken]` на POST-экшенах AccountController

## Admin

### Dashboard (cookie auth + role)

URL: `/admin/...`. Защищён `[Authorize(Policy = "Admin")]` через `_Imports.razor` каждой страничной подпапки (**не** в корне `Components/Admin/`, иначе `AdminLayout` оборачивается сам собой → бесконечная рекурсия на рендере).

- **Applications** (`/admin/applications`) — CRUD над OpenIddict-клиентами + кнопка Regenerate secret. На Edit `ClientId` и `ClientType` в readonly — менять нельзя (пересоздать при необходимости). Scopes загружаются динамически из `IOpenIddictScopeManager`, чекбоксы.
- **Users** (`/admin/users`) — поиск по email, Create, Details (view), Edit (form), Delete. Email + phone с visual confirmed/unconfirmed icons. Manage password (send reset / set manually). Manage roles (чекбоксы + self-Admin guard).

### Admin API (Bearer + scope admin.users)

Endpoint'ы под `/api/admin/users`:

- `GET /api/admin/users/{id}` — получить по id
- `GET /api/admin/users?email=...` — получить по email
- `POST /api/admin/users` — создать (email + optional first/last/phone/password + EmailConfirmed)
- `PATCH /api/admin/users/{id}` — обновить (EmailConfirmed=true только; PhoneNumber, NewPassword)
- `DELETE /api/admin/users/{id}` — удалить
- `POST /api/admin/users/{id}/invite-token` — выдать invite-токен (30d TTL), для формирования ссылки `{auth}/account/accept_invite?userId=X&code=Y&returnUrl=Z`

Валидируется через OpenIddict Validation handler (bearer scheme), требует scope `admin.users`. Клиент регистрируется через admin dashboard как confidential + client_credentials grant + scope `admin.users`.

## Invite flow

1. Бэкенд (App.Web) создаёт юзера через `POST /api/admin/users` (EmailConfirmed на его усмотрение, без пароля — для external-only).
2. Запрашивает токен через `POST /api/admin/users/{id}/invite-token` — отдельный `DataProtectorTokenProvider` с именем `"Invite"` и TTL 30d (константы в `InviteTokenSettings`).
3. Формирует свой invite-email со ссылкой на `/account/accept_invite?userId=X&code=Y&returnUrl=<SPA>`.
4. [AcceptInvite.razor](Components/Account/Pages/AcceptInvite.razor) имеет три состояния:
   - **Invalid** (токен истёк/использован/не тот юзер) → ссылки на Sign in / Reset password
   - **AlreadyActive** (у юзера уже есть пароль или external login) → кнопка Sign in, пропускаем set-password чтобы не ломать 2FA и не перезатирать creds
   - **NeedsPassword** → форма Set password → `AddPasswordAsync` (ротирует stamp → инвалидирует invite-токен single-use) → EmailConfirmed=true → `PasswordSignInAsync` → редирект на returnUrl
5. `returnUrl` валидируется: relative path — ок; absolute — только если origin в `CorsSettings.AllowedOrigins`; иначе fallback на `/account/manage` с warning в лог.

## Конфигурация

Загрузка ([Program.cs](Program.cs)): `appsettings.json` → `appsettings.{Env}.json` → `appsettings.local.json` → env vars → **env vars из JSON** (через `json_settings_names` — массив имён env-переменных, каждая содержит JSON-документ, мёржится в конфигурацию). Последний механизм — для деплоя секретов одним блоком.

Секции:
- `ConnectionStrings:AuthDbConnection` — Postgres
- `DataProtectionSettings` — `CertificateBase64` / `CertificatePassword` + массив `UnprotectCertificates` для ротации X509
- `ReCaptchaSettings` — `SiteKey` / `SecretKey` (пустые → reCAPTCHA disabled)
- `AppSettings` — `UseMockEmailSender`, `UseMockSmsSender`, `IsPhoneConfirmationEnabled`, `IsDeletePersonalDataEnabled`
- `CorsSettings:AllowedOrigins` — массив; **пустой массив = AllowAll + credentials** (использовать только для dev); используется и как whitelist для accept_invite returnUrl
- `SmtpSettings` — `From/Host/Port/User/Password`
- `GitHub:ClientId/ClientSecret`, `Yandex:ClientId/ClientSecret` — external providers
- `InitialAdminUser:Email` — юзер с этим email создаётся (если нет) + назначается ролью Admin на старте

**Секреты не коммитить.** `appsettings.local.json` в `.gitignore`. В проде — env vars или `json_settings_names`.

## Feature toggles в AppSettings

- `IsPhoneConfirmationEnabled` — если `false`, смена телефона происходит без SMS-кода (`SetPhoneNumberAsync` напрямую), `PhoneNumberConfirmed` сбрасывается в false; endpoint'ы отправки кода + «Confirm Phone» кнопка скрыты. Нужен `true` только когда появится реальный SMS-провайдер и 2FA по SMS.
- `IsDeletePersonalDataEnabled` — если `false`, страница `/account/manage/delete_personal_data` редиректит на `not_found`, кнопка Delete скрыта.

## Данные

- PostgreSQL, default schema `auth`, snake_case, все string-колонки по умолчанию 256 символов, enum → string
- История миграций: `auth.__ef_auth_migrations`
- `DbContextPool` + split queries
- Таблицы: Identity (`aspnet_user*`, `aspnet_role*`), OpenIddict (`openiddict_applications/authorizations/scopes/tokens`), `data_protection_keys`, `signing_credentials`
- `ApplicationUser`: `first_name`/`last_name` nullable(100), PersonalData

### Миграции

```bash
dotnet ef migrations add <Name> -c AuthDbContext -o Infrastructure/Data/Migrations
```

Применяются автоматически через `RunMigrationsTask` на старте.

## Криптография

- **Data Protection keys** — создаются через `IKeyManager.CreateNewKey` в `RotateDataProtectionKeysTask`, хранятся в `data_protection_keys`. Опционально обёрнуты X509 (`ProtectKeysWithCertificate`), `UnprotectCertificates` — ротация сертификата без потери старых ключей.
- **OpenIddict signing/encryption keys** — RSA 2048, генерируются в `RotateSigningKeysTask`, параметры сериализуются в JSON, шифруются через `IDataProtector("OpenIddict.SigningCredentials")` и кладутся в `signing_credentials.KeyData`. Зависят от DP-ключей — потеря DP-ключей = потеря signing-ключей.
- **Invite-токены**: отдельный `DataProtectorTokenProvider<ApplicationUser>` с namedOptions `"Invite"`, lifespan 30d. Single-use через security-stamp rotation при `AddPasswordAsync`.

## Background services

- [OpenIddictCleanupService](Infrastructure/BackgroundTasks/OpenIddictCleanupService.cs): `IHostedService`-based, `PeriodicTimer(12h)` с initial delay 15m. Под advisory lock `openiddict-cleanup`. Чистит expired/revoked tokens + ad-hoc authorizations старше 14 дней через `IOpenIddictTokenManager/AuthorizationManager.PruneAsync`. Race safe — если другой инстанс держит лок, skip.

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

- `GET /health` — `UIResponseWriter.WriteHealthCheckUIResponse` (JSON); check'ов пока нет, добавлять отдельно через `AddHealthChecks()`
- Serilog: JSON в Console, `Application=LayeredTemplate.Auth`; request logging с IP/Referer/User-claims
- Аудит: admin-действия логгируются на `Warning`/`Information` с userId и (где применимо) adminId / clientId — ищи по `"Admin "` в логах

## Что важно помнить при правках

1. **OIDC-ключи инициализируются только через startup task**. Если менять schedule, помни, что пустой `SigningKeyStore` → OpenIddict fallback на dev-сертификаты (warning, prod-небезопасно).
2. **Password lockout выключен** в `Login.razor.cs`. Если включать — проверь UI lockout и reCAPTCHA-интеграцию.
3. **CORS AllowAll при пустом массиве** (`Cors/ServicesExtensions.cs`) — в staging/prod обязательно задавать `CorsSettings:AllowedOrigins`. Тот же массив используется как whitelist для invitation returnUrl.
4. **ForwardedHeaders** доверяет всему периметру — корректно только за доверенным reverse-proxy.
5. **SMS** — реальный провайдер не реализован, `ISmsSender` = `MockSmsSender`.
6. **Миграции автоприменяются** на каждом старте — в проде для zero downtime разворачивать non-breaking миграции отдельным шагом.
7. **Admin _Imports.razor layout trap** — `@layout AdminLayout` прописан в `Admin/Applications/_Imports.razor` и `Admin/Users/_Imports.razor`, **не** в `Admin/_Imports.razor`. Иначе `AdminLayout` (`Admin/Shared/`) попадает под директиву и оборачивается сам собой → бесконечная рекурсия рендера.
8. **Blazor SSR формы — Model нужен до первого `await`**: `EditForm Model="Input"` падает если `Input` null на первом рендере. `Input ??= new()` должен стоять **до** любого `await` в `OnInitializedAsync`.
9. **Empty string vs null в POST**. `FormDataMapper` в Blazor SSR не конвертит `""` в `null` для `string?`. Это ломает `[Phone]`, `[StringLength(MinimumLength=...)]` на пустых optional-полях. Фикс — нормализовать в сеттере свойства: `set => field = string.IsNullOrWhiteSpace(value) ? null : value;` (см. InputModel в `Admin/Users/Create.razor.cs` и `Edit.razor.cs`).
10. **ClientSecret round-trip через `OpenIddictApplicationDescriptor`**: `PopulateAsync(descriptor, app)` кладёт **хешированный** секрет в descriptor; `PopulateAsync(app, descriptor)` пишет значение обратно **без** реhash'а. То есть idempotent если не трогать `descriptor.ClientSecret`. Обнулять → затирает секрет в БД → валидация фейлится для confidential клиентов.
11. **`scope` в строке запроса к /connect/token**: OpenIddict требует точного совпадения с зарегистрированными для клиента permissions (`scp:xxx`). Опечатка в scope name (точка vs двоеточие) → `ID2051 not allowed to use the specified scope`.
