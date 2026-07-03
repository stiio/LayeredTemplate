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
│   ├── FeatureDiscovery.cs        — рефлексия: материализует IEndpointGroup, диспатчит IEndpoint через [EndpointGroup<T>], [DevOnly] фильтр
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
│   │   ├── IEndpoint.cs                — `static abstract void Map(IEndpointRouteBuilder)`, реализуется НА КАЖДОМ endpoint-классе
│   │   ├── IEndpointGroup.cs           — `static abstract RouteGroupBuilder MapGroup(IEndpointRouteBuilder)`, один на route-группу
│   │   ├── EndpointGroupAttribute.cs   — `[EndpointGroup<TGroup>]` связывает endpoint с группой
│   │   ├── IFeatureServices.cs         — `static abstract void ConfigureServices(IServiceCollection)`
│   │   └── DevOnlyAttribute.cs
│   ├── Errors/
│   │   ├── Exceptions.cs          — AppMessage/NotFound/AccessDenied/Validation/Domain hierarchy
│   │   ├── AppProblemDetails.cs   — RFC 7807 + ErrorType + Errors dict
│   │   └── GlobalExceptionHandler.cs
│   ├── Infrastructure/
│   │   ├── Email/                 — IEmailSender + EmailSender (MailKit) + EmailSenderMock
│   │   └── Locks/                 — ILockProvider + PostgresLockProvider (Medallion)
│   ├── Options/AppSettings.cs     — AppSettings, SmtpSettings, ConnectionStringKeys
│   └── Pagination/                — PaginationRequest/Response, Sorting<T>, QueryableExtensions
├── Features/                      — одна папка = одна вертикальная нарезка
│   ├── Info/
│   │   ├── _Group.cs              — IEndpointGroup: /api/v1/info, tag Info, group v1
│   │   └── Endpoints/
│   │       └── GetInfo.cs         — IEndpoint, [EndpointGroup<InfoGroup>]
│   ├── Users/
│   │   ├── _Group.cs              — IEndpointGroup
│   │   ├── Entities/
│   │   │   └── User.cs            — User entity (POCO)
│   │   ├── DbConfig/
│   │   │   └── UserConfiguration.cs — IEntityTypeConfiguration<User> (auto-discovered)
│   │   ├── Endpoints/
│   │   │   ├── GetCurrentUser.cs
│   │   │   ├── SendUserEmailCode.cs
│   │   │   └── VerifyUserEmailCode.cs
│   │   └── Models/CurrentUserDto.cs
│   ├── TodoLists/
│   │   ├── _Group.cs              — IEndpointGroup + IFeatureServices (registers ITodoListRatingService)
│   │   ├── Endpoints/
│   │   │   ├── CreateTodoList.cs, GetTodoList.cs, UpdateTodoList.cs, DeleteTodoList.cs
│   │   │   ├── SearchTodoLists.cs + SearchTodoLists.Request.cs + SearchTodoLists.Response.cs — partial split
│   │   │   ├── ListTodoListItems.cs, CreateTodoListItems.cs
│   │   │   ├── CreateTodoListFile.cs, DownloadTodoListFile.cs — multipart + JSON demo
│   │   │   └── RateTodoList.cs    — consumes ITodoListRatingService via DI
│   │   ├── Services/
│   │   │   └── TodoListRatingService.cs — feature-internal service
│   │   └── Models/TodoListDto.cs  — DTOs + polymorphic Items + enums
│   └── _Dev/                      — /api/dev/* (DevOnly, есть только в Development)
│       ├── _Group.cs              — [DevOnly] IEndpointGroup
│       └── Endpoints/
│           └── DebugTest.cs       — [DevOnly] IEndpoint
├── Migrations/                    — EF Core migrations (создавать через `dotnet ef`)
├── appsettings.json (+ .Development/.Staging/.Production/.Test)
└── App.Web.csproj                 — единственный проект
```

## Ключевые паттерны

- **Vertical Slice** — фича = папка под `Features/`. Все слои фичи (entity, DbConfig, DTO, endpoint, services) собраны вместе. Внутри фичи — папки по роли: `Entities/`, `DbConfig/`, `Endpoints/`, `Services/`, `Models/`. Кросс-фичевый код только в `Shared/`.
- **Minimal API endpoints** — каждый endpoint = `public sealed class Foo : IEndpoint` с `Map(IEndpointRouteBuilder)` и `Handle(...)`. Класс sealed чтобы static-only поведение нельзя было унаследовать. Никаких контроллеров, Mediator'а, request handler'ов.
- **Группы маршрутов через `IEndpointGroup`** — base path + tags + OpenAPI version + auth декларируются один раз в `<Feature>/_Group.cs`. Endpoint опционально подключается атрибутом `[EndpointGroup<TGroup>]`; без атрибута регистрируется на корневом builder'е (например, `/health`). В одной фиче могут жить несколько групп — типичный кейс: `TodoListsGroup` + `TodoListsAdminGroup` с разной auth-политикой и разным префиксом, endpoint'ы выбирают свою через `[EndpointGroup<...>]`.
- **Endpoint split convention** — если endpoint-файл переваливает ~150 строк или содержит 3+ nested-типа, превращаем класс в `public sealed partial class Foo : IEndpoint` и выносим nested-типы в sibling-файлы `<Endpoint>.<Part>.cs`. Namespace и OpenAPI schema naming (`<Endpoint><Part>` через parent-name-prepend) остаются прежними — partial-классы для компилятора это один тип. **Живой пример**: [Features/TodoLists/Endpoints/SearchTodoLists.cs](Services/App/App.Web/Features/TodoLists/Endpoints/SearchTodoLists.cs) (Map + Handle), [SearchTodoLists.Request.cs](Services/App/App.Web/Features/TodoLists/Endpoints/SearchTodoLists.Request.cs), [SearchTodoLists.Response.cs](Services/App/App.Web/Features/TodoLists/Endpoints/SearchTodoLists.Response.cs). Альтернатива для очень больших endpoint'ов — подпапка `Endpoints/<Endpoint>/` с тем же naming-pattern.
- **Discovery (`IEndpoint` / `IEndpointGroup` / `IFeatureServices`)** — рефлексия в [Setup/FeatureDiscovery.cs](Services/App/App.Web/Setup/FeatureDiscovery.cs):
    1. `services.AddFeatureServices(env)` ДО `builder.Build()` — находит все `IFeatureServices` и вызывает `ConfigureServices(services)`.
    2. `app.MapAllEndpoints()` ПОСЛЕ `builder.Build()` — в два прохода: (a) материализует каждый `IEndpointGroup` один раз (`MapGroup → RouteGroupBuilder`), (b) для каждого `IEndpoint` читает `EndpointGroupAttribute<TGroup>`, передаёт соответствующий group-builder в `Map(...)`. Endpoint без атрибута получает корневой `IEndpointRouteBuilder`. `[DevOnly]` фильтр работает и на группах, и на endpoint'ах; endpoint, нацеленный на dev-only группу, cascade-скипается вне Development.
- **Feature-internal services** — живут в `Features/<Foo>/Services/<Service>.cs` (interface + impl в одном файле). Регистрируются через `IFeatureServices.ConfigureServices` на `_Group.cs` фичи (классу удобно реализовывать оба интерфейса). Пример: `Features/TodoLists/Services/TodoListRatingService.cs` + потребление в `RateTodoList.Handle`. Если сервис используется несколькими фичами — переезжает в `Shared/Infrastructure/`.
- **DbContext** — все `DbSet<T>` собраны в `Shared/Db/AppDbContext.cs`. EF-конфигурации (`IEntityTypeConfiguration<T>`) автодискаверятся из `Features/<X>/DbConfig/*.cs` через `ApplyConfigurationsFromAssembly`. Добавление новой сущности: `Entities/<Foo>.cs` + `DbConfig/<Foo>Configuration.cs` в фиче + одна строка DbSet в общий DbContext.
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
| **Plugins.Http.Extensions** | HttpContext-расширения (`GetRequestIp` и др.) |
| **Plugins.JsonMultipart** + Abstractions | Minimal-API биндер + OpenAPI-трансформеры для multipart/form-data с JSON-полями. DTO декларируется как `IJsonMultipartRequest<TSelf>`, JSON-поля помечаются `[FromJson]`, файловые поля — обычным `IFormFile` |
| **Plugins.Logging.HttpClientLog** | DelegatingHandler для логирования HttpClient с маскировкой |
| **Plugins.PhoneHelpers** | libphonenumber-csharp wrappers + DataAnnotations |
| **Plugins.StartupRunner** | HostedService для запуска `IStartupTask` при старте |
| **Plugins.Workflow.\*** (Abstractions / Engine / Storage.EFCore) | Durable workflow engine: графы-определения, раны со снапшотом графа, claim шагов через `FOR UPDATE SKIP LOCKED`, LISTEN/NOTIFY-пробуждение воркеров (пулинг остаётся fallback'ом), ретраи с backoff, suspend/resume + bookmarks/сигналы, sub-workflows, retention. Хранилище Postgres-only в отдельной схеме `workflow` со своей историей миграций, опциональное шифрование PHI-колонок. Подключение: `services.AddWorkflowCore(cfg).AddEfCoreStorage(connStr)`, кастомные экшены — `.AddActionType<T>()`. Детали: [Engine README](Services/Plugins/Plugins.Workflow.Engine/README.md) |

## `Tests/` — тесты

| Проект | Что покрывает |
|--------|---------------|
| **Tests.Workflow** | Функциональные тесты workflow-движка (xUnit, без БД): worker (порты/ретраи/suspend/FinishRun), timeout-sweep, resumer (lifecycle-хуки + транзакционная атомарность), dispatcher (капы, flush-семантика), canceller, restarter, сигнальный контур (signaler, WaitSignal/SendSignal, e2e), work-signal (латч LISTEN/NOTIFY-пробуждения) + предикат notify-интерцептора (на EF InMemory), protected-конвертеры Storage.EFCore, CorrelationKeyLog. In-memory фейки `IWorkflowStore` и др. живут в `TestDoubles/`; internal-швы движка (`ExecuteOneAsync`, `SweepExpiredWaitingStepsOnceAsync`) открыты через `InternalsVisibleTo` в [Directory.Build.props](Directory.Build.props) |

Тестовые проекты кладём в `Tests/` (naming: `Tests.<Область>[.<Вид>]`). Assembly-имена `Tests.App.Functional`, `Tests.App.Integration` и `Tests.Workflow` уже включены в repo-wide `InternalsVisibleTo`.

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

# Тесты
dotnet test Tests/Tests.Workflow

# Docker
docker-compose -f docker-compose.yml up
```

## Что важно помнить при правках App.Web

1. **Добавление фичи** — создать `Features/<Feature>/_Group.cs` (реализует `IEndpointGroup`) + `Features/<Feature>/Endpoints/<X>.cs` с `[EndpointGroup<XxxGroup>] public sealed class X : IEndpoint`. Discovery подхватит без правок в `Program.cs`.
2. **Добавление endpoint'а в существующую фичу** — один файл в `Features/<Feature>/Endpoints/`, реализующий `IEndpoint` с `[EndpointGroup<...>]`. Не нужно лезть в `_Group.cs` или какой-либо регистрационный файл.
3. **Добавление таблицы** — entity в `Features/<Foo>/Entities/<Foo>.cs`, EF mapping в `Features/<Foo>/DbConfig/<Foo>Configuration.cs` (автодискаверится), одна строка `DbSet<Foo>` в `Shared/Db/AppDbContext.cs`. Затем `dotnet ef migrations add`.
4. **Добавление сервиса фичи** — `Features/<Foo>/Services/<Service>.cs` (interface + impl). Если у фичи ещё нет DI-регистрации — `_Services.cs` реализует `IFeatureServices` и вызывает `services.AddScoped<IFoo, Foo>()` в `ConfigureServices`. Discovery подхватит.
5. **Несколько групп в одной фиче** — добавить ещё один `_AdminGroup.cs` (или произвольное имя) рядом с `_Group.cs`, реализующий `IEndpointGroup`. Endpoint'ы, нацеленные на новую группу, ставят `[EndpointGroup<XxxAdminGroup>]` вместо `[EndpointGroup<XxxGroup>]`. Типичный кейс: разный префикс/auth для user-facing vs admin endpoint'ов одной доменной области.
6. **Версионирование** — v2 endpoint = новый файл `XxxV2.cs` в той же `Endpoints/` папке, нацеленный на отдельную группу `XxxV2Group` (с префиксом `/api/v2/...` и `WithGroupName("v2")`). Старые остаются в v1.
7. **Dev endpoint'ы** — `Features/_Dev/` для cross-cutting, отдельный `_File.cs` в обычной фиче для feature-specific. `[DevOnly]` ставится либо на `IEndpointGroup` (тогда вся группа исчезает в non-Development), либо на конкретный `IEndpoint`. Endpoint, нацеленный на dev-only группу, автоматически cascade-скипается вне Development — отдельный `[DevOnly]` на endpoint'е в этом случае не обязателен, но и не помешает.
8. **Кросс-фичевая модель** — добавлять в `Shared/` только если она реально шарится. Иначе оставлять в фиче, даже если другая фича могла бы её использовать (DRY < локальная связность).
9. **Не вешать `///`-комментарии на свойства дженерик-классов в App.Web.** Source-gen `Microsoft.AspNetCore.OpenApi.SourceGenerators` 10.0.x падает с `duplicate key` при попытке закешировать такие комментарии (только если тип в **текущей** компиляции; для referenced-сборок через `.xml` всё работает). Конкретно: `Sorting<TFields>.Column` — без XML-doc. Если нужно описание поля — добавляй его на point-of-use, например, на свойство `SearchTodoLists.Request.Sorting`. Когда баг в SDK починят, ограничение снимется.
10. **Multipart + JSON-части** — для endpoint'ов вида "JSON-документ в одной multipart-части + файл во второй" используется `Plugins.JsonMultipart`. **JSON-bound тип** маркируется интерфейсом `: IJsonMultipartPart<TSelf>` (CRTP, через DIM подключает `BindAsync` который десериализует form-field или uploaded-file как JSON). **Endpoint handler** объявляет multipart-части как individual параметры — `Body` биндится через свой BindAsync, `IFormFile` — нативно, primitives — через `[FromForm]`. Пример: [Features/TodoLists/Endpoints/CreateTodoListFile.cs](Services/App/App.Web/Features/TodoLists/Endpoints/CreateTodoListFile.cs):
    ```csharp
    public sealed class Body : IJsonMultipartPart<Body> { ... }

    public static TodoListDto Handle(Body body, IFormFile file, [FromForm] bool isDraft) => ...;
    ```
    OpenAPI получает корректную схему: `multipart/form-data` content type, `body` part c inline schema + `encoding.body.contentType: application/json`, `IFormFile` как `type: string, format: binary`, primitives нативно. `[AsParameters]` с Request DTO **не используется** — конфликтует с `BindAsync` на complex-типах. Если нужна валидация всего "запроса" целиком — FluentValidation per-параметр через DataAnnotations и индивидуальные validator'ы.
11. **Build-time OpenAPI generation требует знать про DI-сервисы, которые endpoint'ы потребляют.** Сборка spec'ов запускается через `GetDocument.Insider` host (Microsoft.Extensions.ApiDescription.Server) — отдельная ветка в `Program.cs`. Endpoint вида `Handle(AppDbContext db, ITodoListRatingService rating, ...)` потребует, чтобы `AppDbContext` / `ITodoListRatingService` были видны DI-контейнеру в этой ветке, иначе minimal API упадёт с `Failure to infer one or more parameters`.
    - **Feature services** (`IFeatureServices.ConfigureServices`) — автоматически подхватываются через `services.AddFeatureServices(...)` уже в build-time ветке.
    - **OpenAPI-плагины** (например `AddPluginJsonMultipart`) — подключаются явно там же.
    - **"Тяжёлые" сервисы** (`AppDbContext`, `ILockProvider`, etc.) — НЕ хотим регистрировать через `AddAppDb` в build-time (это потянет реальную БД, миграции, DataProtection с настоящим сертификатом). Вместо этого добавляем **type-only stub** в [Setup/ConfigureDocGenStubs.cs](Services/App/App.Web/Setup/ConfigureDocGenStubs.cs) — одна строка типа `services.AddDbContextPool<AppDbContext>(o => o.UseNpgsql("Host=stub"))` или `services.AddSingleton<IFoo>(_ => null!)`. Сервис никогда не резолвится (doc generator только описывает endpoint'ы, не выполняет), но minimal API видит его как зарегистрированный.

    Convention для добавления **нового** heavy-сервиса в endpoint signature: если `dotnet build` упал на стадии doc-gen — добавь stub в `AddDocGenStubs()`. Production-side регистрации (`AddAppDb`, etc.) при этом остаются чистыми без build-time условий.
