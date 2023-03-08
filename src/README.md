# Sample

## Environment ([example](./env.example.json))
[-ASPNETCORE_ENVIRONMENT=Staging | Production-]  

## Project settings
For all new projects add to *.csproj and remove duplicate props.
```xml
 <Import Project="../common.targets" />
```

## Naming convensions
- For requests/responses: [Resource][ResourceSuffix?][Role?][Action][Request? | Response?] (e.g. TodoItemCreateRequest, UserAddressAdminUpdateRequest, CurrentUserResponse)
- For api actions: [Role?][Action][Resource][ResourceSuffix] (e.g. UpdateUser, AdminUpdateUser)

## Manual TypeScript client generation
Use openapi-generator-cli:v6.2.1
image `image openapitools/openapi-generator-cli:v6.2.1`
```
openapi-generator-cli generate -g typescript-fetch -i Web/specs/api_merged.yaml -o front -c ts-gen-config.json --global-property skipFormModel=false
```