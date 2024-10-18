# Infrastructure.Data

## For add migration run command in cmd:
- From cmd: `dotnet EF migrations add MigrationName`  
- From PM: `Add-Migration MigrationName`

## For revert applying migrations
1. From PM console: `Update-Database TargetMigrationName`
2. From PM: `Remove-Migration`