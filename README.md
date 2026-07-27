# Aidly ERP — .NET Backend

.NET 10 / EF Core / PostgreSQL migration of the Spring Boot `sme-software-backend`.

## Layout

| Project | Contains |
|---|---|
| `AidlyErp.Domain` | Entities, enums, `AuditEntity` / `BaseEntity` |
| `AidlyErp.Application` | Services, DTOs, repository interfaces, RBAC, approvals |
| `AidlyErp.Infrastructure` | EF Core `DbContext`, repositories, JWT |
| `AidlyErp.Api` | Controllers, middleware, composition root |

## Configuration — secrets are NOT in this repo

`appsettings.json` ships with placeholders. Supply real values one of two ways:

**Environment variables** (preferred for deployment):

```
ConnectionStrings__DefaultConnection="Host=…;Password=…;SslMode=Require"
Aidly__Jwt__Secret="<at least 32 characters>"
```

**Or `appsettings.Local.json`** (gitignored) for local development:

```json
{
  "ConnectionStrings": { "DefaultConnection": "Host=…;Password=…" },
  "Aidly": { "Jwt": { "Secret": "<at least 32 characters>" } }
}
```

The JWT secret is validated at startup and must be **≥ 32 characters** — the app
fails fast rather than silently padding a weak key.

## Running

```bash
dotnet run --project src/AidlyErp.Api
```

OpenAPI (Development only), one document per module:
`/v3/api-docs/{core|sys|hrm|fin|inv|pur|sal}.json`

## Migration status

See [MIGRATION-AUDIT.md](MIGRATION-AUDIT.md) for per-module coverage, known gaps
and the defects found during the port.

## Branches

- `main` — this migration
- `pre-aidlyerp-backup` — the previous backend that occupied this repo, preserved
