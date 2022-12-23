# Sample

## Namespace settings
For all new projects add to *.cs
```xml
<RootNamespace>$(SolutionName.Replace(" ", "_")).$(MSBuildProjectName.Replace(" ", "_"))</RootNamespace>
```

## Naming convensions
- For requests/responses: **[** Resource **]** **[** ResourceSuffix **]** **[** Action **]** **[** Role? **]** **[** Request | Response **]** (e.g. TodoItemCreateRequest, UserAddressAdminUpdateRequest, CurrentUserResponse)
- For api actions: **[** Role? **]** **[** Action **]** **[** Resource **]** (e.g. UpdateUser, AdminUpdateUser)

## Manual TypeScript client generation
Use openapi-generator-cli:v6.2.1
image `image openapitools/openapi-generator-cli:v6.2.1`
```
cd Web
dotnet restore
dotnet tool restore
dotnet publish -c Release -o publish

cd publish
dotnet swagger tofile --output spec.yml --yaml Web.dll merged_api_versions
openapi-generator-cli generate -g typescript-fetch -i spec.yml -o front -c ts-gen-config.json --global-property skipFormModel=false
```