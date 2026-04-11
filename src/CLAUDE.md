# LayeredTemplate

## Архитектура

```
HTTP Request
  -> [App.Web] Controllers -> Mediator
  -> [App.Application] Handlers + ValidationBehaviour
  -> [App.Infrastructure] Services (Auth, Email, Locks)
  -> [App.Infrastructure.Data] DbContext (EF Core + Dapper)
  -> [App.Domain] Entities, Domain Logic
  -> PostgreSQL
```

Зависимости: Web -> Infrastructure -> Infrastructure.Data -> Application -> Domain.

## Структура проекта

### `Services/App/` — основное приложение

**App.Web** — точка входа, API-слой.
- `Program.cs` — конфигурация хоста, middleware, endpoints.
- `Controllers/` — REST-контроллеры. Наследуют `AppControllerBase`, делегируют в Mediator через `Sender.Send()`.
- `ConfigureOptions/ConfigureOpenApiOptions.cs` — центральная конфигурация OpenAPI: трансформеры, версионирование документов, schema IDs.
- `OpenApiTransformers/` — кастомные трансформеры OpenAPI-документа (auth, camelCase params, datetime, polymorphism, security и др.).
- `Json/Converters/` — кастомные JSON-конвертеры.
- `Models/` — DTO для ответов (ErrorResult, SuccessfulResult).

**App.Application** — бизнес-логика, CQRS.
- `Features/{Feature}/Requests/` — запросы (IRequest<TResponse>).
- `Features/{Feature}/Handlers/` — обработчики (IRequestHandler).
- `Features/{Feature}/Models/` — DTO для фичи.
- `Features/{Feature}/Validators/` — FluentValidation-валидаторы.
- `Common/Models/` — общие модели: PaginationRequest/Response, Sorting<T>, DirectionType.

**App.Domain** — доменная модель.
- `Entities/` — сущности (User). Реализуют IBaseAuditableEntity (Id, CreatedAt, UpdatedAt).
- `Exceptions/` — DomainException, ForeignKeyViolationException, AlreadyExistsException.
- `Interfaces/` — IBaseEntity, IBaseAuditableEntity, ITimeStamp.

**App.Infrastructure** — внешние сервисы и настройки.

**App.Infrastructure.Data** — доступ к данным.
- `Configurations/` — IEntityTypeConfiguration для сущностей.

### `Services/Plugins/` — переиспользуемые модули

| Плагин | Назначение |
|--------|-----------|
| **Plugins.AssemblyExtensions** | GetBuildDate(), GetVersion() из метаданных сборки |
| **Plugins.Authorization.Abstractions** | Константы: AuthenticationSchemes, Claims, Permissions, TokenKeys |
| **Plugins.Http.Extensions** | HttpContext-расширения (GetRequestIp и др.) |
| **Plugins.JsonMultipart** | Model binder и OpenAPI-трансформер для multipart/form-data с JSON-полями |
| **Plugins.JsonMultipart.Abstractions** | Атрибут [FromJson] |
| **Plugins.Logging.HttpClientLog** | DelegatingHandler для логирования HttpClient с маскировкой данных |
| **Plugins.Options** | Модели конфигурации (SmtpSettings, AppSettings, ConnectionStrings) |
| **Plugins.SharedExtensions** | TypeExtensions, EnumerableExtensions, CollectionExtensions |
| **Plugins.StartupRunner** | HostedService для запуска IStartupTask при старте приложения |

### `Pipelines/` — CI/CD

- `backend-deploy.yml` — Docker build, push в AWS ECR, деплой в ECS.
- `npm-api-package-deploy.yml` — генерация TypeScript-клиента из OpenAPI, публикация npm-пакета.

### Корень `/`

- `Directory.Build.props` — общие настройки: net10.0, nullable, StyleCop, RootNamespace.
- `.gitlab-ci.yml` — пайплайн GitLab CI.

## Ключевые паттерны

- **CQRS + Mediator** — запросы через `IRequest<T>`, обработка в `IRequestHandler<TRequest, TResponse>`. Контроллеры только вызывают `Sender.Send()`.
- **FluentValidation** — валидаторы запросов, выполняются автоматически через `ValidationBehaviour`.
- **API Versioning** — URL-сегмент (`/api/app/v1/...`). Документы OpenAPI: v1, v1-dev, merged_api.
- **OpenAPI** — Microsoft.AspNetCore.OpenApi + Scalar UI. Трансформеры для auth, camelCase, datetime, polymorphism.
- **EF Core + Dapper** — EF для ORM, Dapper для raw SQL. Общий контекст транзакций.
- **Distributed Locking** — PostgreSQL advisory locks через `ILockProvider`.
- **Startup Tasks** — миграции и seed через `IStartupTask` (плагин StartupRunner).
- **Serilog** — JSON-логирование, request logging.

## Команды

```bash
# Сборка
dotnet build LayeredTemplate.App.slnx

# Запуск
dotnet run --project Services/App/App.Web

# Docker
docker-compose -f docker-compose.yml up
```
