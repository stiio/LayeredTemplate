# LayeredTemplate

## Архитектура

```
HTTP Request → [App.Web] Endpoint Handler (minimal API) → AppDbContext (EF Core + Dapper) → PostgreSQL
```

**Vertical Slice Architecture (VSA).** Один проект `App.Web`, никаких Application/Infrastructure/Domain слоёв. Каждая фича — отдельная папка под `Features/`, всё про фичу в этой папке: entity, EF mapping, DTO, endpoint'ы, валидаторы.

Cross-cutting код (DbContext, ошибки, аутентификация, OpenAPI, паджинация) живёт в `Shared/` и `Setup/`. Никаких посредников типа Mediator — минимальные API сами по себе use cases.

## Структура `Services/App/App.Web/`

```
App.Web/
├── Program.cs                     — хост + middleware + endpoint discovery
├── Setup/                         — cross-cutting setup (DI, OpenAPI, Auth, Errors, Serilog)
│   ├── ConfigureAuth.cs
│   ├── ConfigureDb.cs
│   ├── ConfigureProblemDetails.cs
│   ├── ConfigureSerilog.cs
│   ├── ConfigurationExtensions.cs — json_settings_names env-var → JSON config
│   ├── EndpointDiscovery.cs       — рефлексия по IEndpoint, [DevOnly] фильтр
│   ├── Json/                      — DateTimeJsonConverter, DateOnlyJsonConverter
│   └── OpenApi/
│       ├── ConfigureOpenApi.cs    — три документа: v1, dev, merged_api
│       └── Transformers/          — Security, Errors, Auth, CamelCase, Date/Enum/Polymorphism
├── Shared/                        — общие примитивы (стабильные, маленькие)
│   ├── Auth/
│   │   ├── AppClaims.cs           — claims/schemes/roles/AppPermissions
│   │   ├── CurrentUser.cs         — ICurrentUser над ClaimsPrincipal
│   │   ├── HasPermissionAttribute.cs + HasPermissionPolicyProvider.cs
│   │   └── MockAuth/              — Dev-only auth handler (USE_MOCK_AUTH)
│   ├── Db/
│   │   ├── AppDbContext.cs        — public partial, DbSets живут в фичах
│   │   ├── AppDesignTimeDbContextFactory.cs
│   │   ├── BaseEntity.cs          — IBaseEntity, ITimeStamp, IBaseAuditableEntity
│   │   ├── Interceptors/BaseEntitySaveChangesInterceptor.cs — CreatedAt/UpdatedAt
│   │   └── RunMigrationsTask.cs   — startup task под advisory lock
│   ├── Endpoints/
│   │   ├── IEndpoint.cs           — `static abstract void Map(IEndpointRouteBuilder)`
│   │   └── DevOnlyAttribute.cs
│   ├── Errors/
│   │   ├── Exceptions.cs          — AppMessage/NotFound/AccessDenied/Validation/Domain hierarchy
│   │   ├── AppProblemDetails.cs   — RFC 7807 + ErrorType + Errors dict
│   │   └── GlobalExceptionHandler.cs
│   ├── Infrastructure/
│   │   ├── Email/                 — IEmailSender + EmailSender (MailKit) + EmailSenderMock
│   │   └── Locks/                 — ILockProvider + PostgresLockProvider (Medallion)
│   ├── Options/AppSettings.cs     — AppSettings, SmtpSettings, ConnectionStringKeys
│   ├── Pagination/                — PaginationRequest/Response, Sorting<T>, QueryableExtensions
│   └── Validation/ValidationFilter.cs — endpoint filter, WithValidation<T>()
├── Features/                      — одна папка = одна вертикальная нарезка
│   ├── Info/
│   │   ├── _Routes.cs             — IEndpoint, регистрация группы /api/v1/info
│   │   └── GetInfo.cs             — Request? Response, Handle()
│   ├── Users/
│   │   ├── _Routes.cs
│   │   ├── _Entities.cs           — User entity
│   │   ├── _DbContext.cs          — partial AppDbContext { DbSet<User> }
│   │   ├── _DbConfig.cs           — IEntityTypeConfiguration<User>
│   │   ├── GetCurrentUser.cs
│   │   ├── SendUserEmailCode.cs
│   │   ├── VerifyUserEmailCode.cs
│   │   └── Models/CurrentUserDto.cs
│   ├── TodoLists/
│   │   ├── _Routes.cs
│   │   ├── CreateTodoList.cs, GetTodoList.cs, UpdateTodoList.cs, DeleteTodoList.cs
│   │   ├── SearchTodoLists.cs, ListTodoListItems.cs, CreateTodoListItems.cs
│   │   └── Models/TodoListDto.cs  — DTOs + polymorphic Items + enums
│   └── _Dev/                      — /api/dev/* (DevOnly, есть только в Development)
│       ├── _Routes.cs
│       └── DebugTest.cs
├── Migrations/                    — EF Core migrations (создавать через `dotnet ef`)
├── appsettings.json (+ .Development/.Staging/.Production/.Test)
└── App.Web.csproj                 — единственный проект
```

## Ключевые паттерны

- **Vertical Slice** — фича = папка под `Features/`. Все слои фичи (entity, DbConfig, DTO, endpoint) собраны вместе. Кросс-фичевый код только в `Shared/`.
- **Minimal API endpoints** — каждый endpoint = статический класс с `Configure(RouteGroupBuilder)` и `Handle(...)`. Никаких контроллеров, Mediator'а, request handler'ов.
- **`IEndpoint` discovery** — каждая фича имеет `_Routes.cs` с `IEndpoint`-маркером. `Program.cs` вызывает `app.MapAllEndpoints()` — рефлексия находит всё в сборке.
- **Per-feature partial DbContext** — DbSets фичи объявлены в `Features/<Feature>/_DbContext.cs` через `public partial class AppDbContext`. EF-конфигурация в `_DbConfig.cs` рядом.
- **Endpoint filters для cross-cutting** — `WithValidation<T>()` валидирует FluentValidation'ом, бросает `AppValidationException`. `GlobalExceptionHandler` мапит в RFC 7807.
- **Версионирование sparse** — endpoint живёт в одной версии. `/api/v1/...` для текущей; когда появляется breaking change, делается `XxxV2.cs` и регистрируется в новой `MapGroup("/api/v2/...")`. Endpoints без изменений остаются только в v1, не дублируются.
- **OpenAPI naming** — endpoint class = operationId (e.g. `CreateTodoList`). Nested types (`CreateTodoList.Request`) → schema `CreateTodoListRequest` через `CreateSchemaReferenceId` callback в `ConfigureOpenApi.cs`.
- **Dev endpoints отдельной группой** — `/api/dev/*`, помечены `[DevOnly]`. В Production/Staging discovery их пропускает.
- **EF Core + Dapper** — `AppDbContext` exposes Dapper-методы (`QueryAsync` etc.) на ту же connection с амбиентной транзакцией.
- **Distributed Locking** — PostgreSQL advisory locks через `ILockProvider` (Medallion.Threading.Postgres).
- **Startup Tasks** — миграции через `IStartupTask` (плагин `Plugins.StartupRunner`).
- **Serilog** — JSON-логирование, request logging с user-claim enrichment.

## `Services/Auth/` — отдельный OIDC-сервер

- `Auth.Web` — OpenIddict + ASP.NET Identity + Blazor Server SSR. Standalone, ничего не знает про App.
- `Auth.ApiClient` — NuGet-style клиент для admin API.

См. [Services/Auth/Auth.Web/CLAUDE.md](Services/Auth/Auth.Web/CLAUDE.md).

## `Services/OAuthSample/` — sample SPA для Auth.Web

Минимальная страничка с oidc-client-ts, демонстрирует authorization-code+PKCE flow и интеграцию с Auth.Web как OIDC-провайдером.

## `Services/Plugins/` — переиспользуемые модули

| Плагин | Назначение |
|--------|-----------|
| **Plugins.AssemblyExtensions** | `GetBuildDate()`, `GetVersion()` из метаданных сборки |
| **Plugins.Authorization.Abstractions** | Константы: AuthenticationSchemes, Claims, Permissions, TokenKeys |
| **Plugins.Http.Extensions** | HttpContext-расширения (`GetRequestIp` и др.) |
| **Plugins.JsonMultipart** + Abstractions | Model binder и OpenAPI-трансформер для multipart/form-data с JSON-полями (MVC only — НЕ работает с minimal API из-за разных биндеров) |
| **Plugins.Logging.HttpClientLog** | DelegatingHandler для логирования HttpClient с маскировкой |
| **Plugins.PhoneHelpers** | libphonenumber-csharp wrappers + DataAnnotations |
| **Plugins.SharedExtensions** | TypeExtensions, EnumerableExtensions |
| **Plugins.StartupRunner** | HostedService для запуска `IStartupTask` при старте |

## `Pipelines/` — CI/CD

- `backend-deploy.yml` — Docker build, push в AWS ECR, деплой в ECS.
- `npm-api-package-deploy.yml` — генерация TypeScript-клиента из OpenAPI, публикация npm-пакета.
- `deps-update.yml` — scheduled bump NuGet-пакета + push в dev-ветку.

## Команды

```bash
# Сборка
dotnet build LayeredTemplate.App.slnx

# Запуск App.Web
dotnet run --project Services/App/App.Web

# Запуск Auth.Web
dotnet run --project Services/Auth/Auth.Web

# Миграции App.Web
cd Services/App/App.Web
dotnet ef migrations add <Name> -o Migrations

# Docker
docker-compose -f docker-compose.yml up
```

## Что важно помнить при правках App.Web

1. **Добавление фичи** — создать `Features/<Feature>/_Routes.cs` (с `IEndpoint`) + один-два endpoint-файла. Discovery подхватит без правок в `Program.cs`.
2. **Добавление таблицы** — entity в `_Entities.cs`, partial DbSet в `_DbContext.cs`, EF mapping в `_DbConfig.cs`. Затем `dotnet ef migrations add`.
3. **Версионирование** — v2 endpoint = новый файл `XxxV2.cs`. Регистрируется только в новой группе `/api/v2/...`. Старые остаются в v1.
4. **Dev endpoint'ы** — `Features/_Dev/` для cross-cutting, `_File.cs` в обычной фиче для feature-specific. `[DevOnly]` на классе `IEndpoint` — discovery пропустит вне Development.
5. **Кросс-фичевая модель** — добавлять в `Shared/` только если она реально шарится. Иначе оставлять в фиче, даже если другая фича могла бы её использовать (DRY < локальная связность).
6. **Не вешать `///`-комментарии на свойства дженерик-классов в App.Web.** Source-gen `Microsoft.AspNetCore.OpenApi.SourceGenerators` 10.0.x падает с `duplicate key` при попытке закешировать такие комментарии (только если тип в **текущей** компиляции; для referenced-сборок через `.xml` всё работает). Конкретно: `Sorting<TFields>.Column` — без XML-doc. Если нужно описание поля — добавляй его на point-of-use, например, на свойство `SearchTodoLists.Request.Sorting`. Когда баг в SDK починят, ограничение снимется.
7. **File upload endpoints удалены** — `Plugins.JsonMultipart` через `[FromJson]` нужен `IModelBinder` (MVC), а minimal API такой не использует. Когда понадобится — переписать на `IFormCollection` или явный JSON-параметр + `IFormFile`.
