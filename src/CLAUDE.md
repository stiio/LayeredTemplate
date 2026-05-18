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
│   │   ├── IFeatureServices.cs    — `static abstract void ConfigureServices(IServiceCollection)`
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
│   │   ├── _DbConfig.cs           — IEntityTypeConfiguration<User> (auto-discovered)
│   │   ├── GetCurrentUser.cs
│   │   ├── SendUserEmailCode.cs
│   │   ├── VerifyUserEmailCode.cs
│   │   └── Models/CurrentUserDto.cs
│   ├── TodoLists/
│   │   ├── _Routes.cs             — IEndpoint + IFeatureServices (registers ITodoListRatingService)
│   │   ├── CreateTodoList.cs, GetTodoList.cs, UpdateTodoList.cs, DeleteTodoList.cs
│   │   ├── SearchTodoLists.cs, ListTodoListItems.cs, CreateTodoListItems.cs
│   │   ├── CreateTodoListFile.cs, DownloadTodoListFile.cs — multipart + JSON demo
│   │   ├── RateTodoList.cs        — consumes ITodoListRatingService via DI
│   │   ├── Services/
│   │   │   └── TodoListRatingService.cs — feature-internal service
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
- **Endpoint split convention** — если endpoint-файл переваливает ~150 строк или содержит 3+ nested-типа, превращаем outer-класс в `public static partial class` и выносим nested-типы в sibling-файлы `<Endpoint>.<Part>.cs`. Namespace и OpenAPI schema naming (`<Endpoint><Part>` через parent-name-prepend) остаются прежними — partial-классы для компилятора это один тип. **Живой пример**: [Features/TodoLists/SearchTodoLists.cs](Services/App/App.Web/Features/TodoLists/SearchTodoLists.cs) (Configure + Handle), [SearchTodoLists.Request.cs](Services/App/App.Web/Features/TodoLists/SearchTodoLists.Request.cs), [SearchTodoLists.Response.cs](Services/App/App.Web/Features/TodoLists/SearchTodoLists.Response.cs). Альтернатива для очень больших endpoint'ов — подпапка `<Endpoint>/` с тем же naming-pattern.
- **`IEndpoint` / `IFeatureServices` discovery** — каждая фича имеет `_Routes.cs` с `IEndpoint`-маркером (роуты) и/или `IFeatureServices` (DI-регистрация). `Program.cs` вызывает `services.AddFeatureServices(env)` ДО `builder.Build()` и `app.MapAllEndpoints()` ПОСЛЕ — рефлексия находит реализации в сборке. Фича может иметь оба, один из них, или ни одного.
- **Feature-internal services** — живут в `Features/<Foo>/Services/<Service>.cs` (interface + impl в одном файле). Регистрируются через `IFeatureServices.ConfigureServices` на `_Routes.cs` фичи. Пример: `Features/TodoLists/Services/TodoListRatingService.cs` + потребление в `RateTodoList.Handle`. Если сервис используется несколькими фичами — переезжает в `Shared/Infrastructure/`.
- **DbContext** — все `DbSet<T>` собраны в `Shared/Db/AppDbContext.cs`. EF-конфигурации (`IEntityTypeConfiguration<T>`) автодискаверятся из `Features/<X>/_DbConfig.cs` через `ApplyConfigurationsFromAssembly`. Добавление новой сущности: `_Entities.cs` + `_DbConfig.cs` в фиче + одна строка DbSet в общий DbContext.
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
| **Plugins.JsonMultipart** + Abstractions | Minimal-API биндер + OpenAPI-трансформеры для multipart/form-data с JSON-полями. DTO декларируется как `IJsonMultipartRequest<TSelf>`, JSON-поля помечаются `[FromJson]`, файловые поля — обычным `IFormFile` |
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
2. **Добавление таблицы** — entity в `Features/<Foo>/_Entities.cs`, EF mapping в `Features/<Foo>/_DbConfig.cs` (автодискаверится), одна строка `DbSet<Foo>` в `Shared/Db/AppDbContext.cs`. Затем `dotnet ef migrations add`.
3. **Добавление сервиса фичи** — `Features/<Foo>/Services/<Service>.cs` (interface + impl). Если у фичи ещё нет регистрации — `_Routes.cs` дополнительно реализует `IFeatureServices` и вызывает `services.AddScoped<IFoo, Foo>()` в `ConfigureServices`. Discovery подхватит.
4. **Версионирование** — v2 endpoint = новый файл `XxxV2.cs`. Регистрируется только в новой группе `/api/v2/...`. Старые остаются в v1.
5. **Dev endpoint'ы** — `Features/_Dev/` для cross-cutting, `_File.cs` в обычной фиче для feature-specific. `[DevOnly]` на классе `IEndpoint` — discovery пропустит вне Development.
6. **Кросс-фичевая модель** — добавлять в `Shared/` только если она реально шарится. Иначе оставлять в фиче, даже если другая фича могла бы её использовать (DRY < локальная связность).
7. **Не вешать `///`-комментарии на свойства дженерик-классов в App.Web.** Source-gen `Microsoft.AspNetCore.OpenApi.SourceGenerators` 10.0.x падает с `duplicate key` при попытке закешировать такие комментарии (только если тип в **текущей** компиляции; для referenced-сборок через `.xml` всё работает). Конкретно: `Sorting<TFields>.Column` — без XML-doc. Если нужно описание поля — добавляй его на point-of-use, например, на свойство `SearchTodoLists.Request.Sorting`. Когда баг в SDK починят, ограничение снимется.
8. **Multipart + JSON-поля** — для endpoint'ов вида `[FromJson] Body + IFormFile File` (документ JSON в одной multipart-части, файл во второй) используется `Plugins.JsonMultipart`. DTO декларируется как `: IJsonMultipartRequest<Request>` — это CRTP-интерфейс, который через DIM подключает `BindAsync` (minimal API custom binding) и `PopulateMetadata` (OpenAPI multipart hint). Пример в [Features/TodoLists/CreateTodoListFile.cs](Services/App/App.Web/Features/TodoLists/CreateTodoListFile.cs). OpenAPI получает корректную схему: `multipart/form-data` content type, `encoding.<field>.contentType: application/json` для JSON-частей, `IFormFile` сериализуется как `type: string, format: binary`.
9. **Build-time OpenAPI generation требует знать про DI-сервисы, которые endpoint'ы потребляют.** Сборка spec'ов запускается через `GetDocument.Insider` host (Microsoft.Extensions.ApiDescription.Server) — отдельная ветка в `Program.cs`. Endpoint вида `Handle(AppDbContext db, ITodoListRatingService rating, ...)` потребует, чтобы `AppDbContext` / `ITodoListRatingService` были видны DI-контейнеру в этой ветке, иначе minimal API упадёт с `Failure to infer one or more parameters`.
    - **Feature services** (`IFeatureServices.ConfigureServices`) — автоматически подхватываются через `services.AddFeatureServices(...)` уже в build-time ветке.
    - **OpenAPI-плагины** (например `AddPluginJsonMultipart`) — подключаются явно там же.
    - **"Тяжёлые" сервисы** (`AppDbContext`, `ILockProvider`, etc.) — НЕ хотим регистрировать через `AddAppDb` в build-time (это потянет реальную БД, миграции, DataProtection с настоящим сертификатом). Вместо этого добавляем **type-only stub** в [Setup/ConfigureDocGenStubs.cs](Services/App/App.Web/Setup/ConfigureDocGenStubs.cs) — одна строка типа `services.AddDbContextPool<AppDbContext>(o => o.UseNpgsql("Host=stub"))` или `services.AddSingleton<IFoo>(_ => null!)`. Сервис никогда не резолвится (doc generator только описывает endpoint'ы, не выполняет), но minimal API видит его как зарегистрированный.

    Convention для добавления **нового** heavy-сервиса в endpoint signature: если `dotnet build` упал на стадии doc-gen — добавь stub в `AddDocGenStubs()`. Production-side регистрации (`AddAppDb`, etc.) при этом остаются чистыми без build-time условий.
