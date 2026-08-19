# PortYard

Container yard management API for a port terminal, built with ASP.NET Core and EF Core.

[![CI](https://github.com/Bergstefann/container-yard-management/actions/workflows/ci.yml/badge.svg)](https://github.com/Bergstefann/container-yard-management/actions/workflows/ci.yml)

## What this is, and why

A container terminal has to answer three questions correctly, all the time: where is every box physically sitting, which ones is the terminal legally allowed to release through the gate, and how full is the yard right now. Get any of those wrong and the consequences are concrete — a container nobody can locate, cargo released under an active customs hold, or a yard block that silently overflows its capacity. PortYard models that domain directly: containers move through a strict lifecycle (expected, gated in, stored, staged, gated out), every physical move is written to an append-only ledger, and the rules that govern slot capacity, reefer placement, and customs holds are enforced by the domain model itself rather than trusted to whichever caller happens to be writing to the database that day.

This is a portfolio project, not a production system, but it's built the way the underlying problem deserves: a domain layer with no framework dependencies and real invariants, a persistence layer that maps cleanly onto it, a REST API that never lets a controller mutate state directly, and a test suite that exists to prove the business rules hold rather than to pad a coverage number.

## Quick start

PortYard runs against SQL Server. Start one locally with Docker (this is the exact image CI also uses, so if it works here it'll work there):

```bash
docker run -e "ACCEPT_EULA=Y" -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest
```

(the password above is Microsoft's own documented placeholder for this image, not a real credential — `appsettings.json`'s default connection string already matches it, no configuration needed for local use). Then:

```bash
git clone https://github.com/Bergstefann/container-yard-management.git
cd container-yard-management
dotnet run --project src/PortYard.Api
```

The database schema is applied and seeded automatically on first run in Development. Open `https://localhost:<port>/swagger` (the port is printed on startup) to browse and try every endpoint, or `https://localhost:<port>/health` to check the app can reach the database.

No Docker handy? Point `ConnectionStrings:YardDb` (in `appsettings.Development.json`, or the `ConnectionStrings__YardDb` environment variable) at an [Azure SQL free-tier database](https://azure.microsoft.com/en-us/products/azure-sql/database/) instead — same schema, same migration, no code changes.

## Architecture

```
src/
├── PortYard.Domain/   entities, enums, ISO 6346 validation, domain exceptions
└── PortYard.Api/      contracts (DTOs), controllers, EF Core, services, middleware
tests/
└── PortYard.Tests/    unit tests (domain) and integration tests (API)
```

`PortYard.Domain` has no reference to EF Core, ASP.NET Core, or anything else outside the base class library. Every business rule — legal status transitions, slot capacity, reefer placement, customs holds, movement chronology — lives on the entities themselves as methods (`Container.GateIn()`, `AssignToSlot()`, `Stage()`, `GateOut()`), and every one of those methods can be unit tested in memory, in milliseconds, with no database and no HTTP pipeline. `PortYard.Api` depends on `PortYard.Domain`, never the other way round: controllers call into thin services, services orchestrate EF Core and call the domain methods, and the domain methods are the only code path that can change a container's state.

## Domain rules

These are the invariants the domain layer enforces and the test suite exists to prove:

- **Legal status transitions only.** `Expected → GatedIn → Stored → Staged → GatedOut`, with two additional legal edges: `GatedIn → Staged` (direct transhipment, a container that's never actually yarded) and `Staged → Stored` (re-yarded). Every other transition throws. `GatedOut` is terminal.
- **A container occupies at most one slot.** Assigning a container to a new slot clears its previous slot and records a single Yard movement carrying both the from and to slot.
- **Slot capacity, in TEU, is never exceeded.** A 20ft container is 1 TEU; 40ft and 45ft are 2 TEU. An assignment that would push a slot over its `MaxTeu` is rejected.
- **Reefers only go in reefer-capable slots.** Assigning a reefer container to a slot without power throws.
- **No gate-out under an active customs hold.** A container with any unreleased hold cannot gate out; releasing the hold permits it.
- **Movements are append-only and chronologically consistent.** A new movement can never be dated earlier than the container's most recent one, and nothing ever mutates a movement once it's recorded.
- **Container numbers are unique and ISO 6346-valid**, enforced both at the API boundary (a malformed number never reaches the domain layer) and by a unique database index.

## API endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/containers` | Paged, filterable list (status, size, type, shippingLine, block) |
| GET | `/api/containers/{containerNumber}` | Full detail: current slot, active holds, movement history |
| POST | `/api/containers` | Register a new expected container |
| POST | `/api/containers/{containerNumber}/gate-in` | Expected → GatedIn |
| POST | `/api/containers/{containerNumber}/assign-slot` | Assign to a yard slot (→ Stored) |
| POST | `/api/containers/{containerNumber}/stage` | Stage for departure |
| POST | `/api/containers/{containerNumber}/gate-out` | Staged → GatedOut |
| GET | `/api/slots` | All yard slots with occupancy and remaining TEU |
| GET | `/api/slots/{code}` | Slot detail, including occupying containers |
| POST | `/api/containers/{containerNumber}/holds` | Place a customs hold |
| POST | `/api/holds/{id}/release` | Release a customs hold |
| GET | `/api/reports/yard-utilisation` | Occupancy by block: slots and TEU used vs capacity |
| GET | `/api/reports/dwell-time` | Average/median dwell time by shipping line and container type |
| GET | `/api/reports/throughput` | Gate-in/gate-out counts by day, over a date range |

## Data model

```mermaid
erDiagram
    VESSEL ||--o{ CONTAINER : "inbound (optional)"
    YARD_SLOT ||--o{ CONTAINER : "currently stores (optional)"
    CONTAINER ||--o{ MOVEMENT : "has"
    CONTAINER ||--o{ CUSTOMS_HOLD : "has"
    YARD_SLOT ||--o{ MOVEMENT : "from / to (optional)"

    VESSEL {
        int Id PK
        string Name
        string Imo
        datetimeoffset Eta
    }
    YARD_SLOT {
        int Id PK
        string Block
        int Row
        int Tier
        int MaxTeu
        bool IsReeferCapable
    }
    CONTAINER {
        int Id PK
        string ContainerNumber
        string Size
        string Type
        string Status
        int GrossWeightKg
        string ShippingLine
        int CurrentSlotId FK
        int InboundVesselId FK
        datetimeoffset ArrivedAt
        datetimeoffset DepartedAt
    }
    MOVEMENT {
        int Id PK
        int ContainerId FK
        string Type
        int FromSlotId FK
        int ToSlotId FK
        datetimeoffset OccurredAt
        string Operator
    }
    CUSTOMS_HOLD {
        int Id PK
        int ContainerId FK
        string Reason
        datetimeoffset PlacedAt
        datetimeoffset ReleasedAt
    }
```

## Deployment

**Live**: not yet — see the checklist below. The pipeline that will keep it deployed is already in place.

```mermaid
flowchart LR
    Dev[git push to main] --> CI[GitHub Actions: build-and-test]
    CI -- pass --> Publish[dotnet publish]
    Publish --> Deploy[azure/webapps-deploy]
    Deploy --> AppService[Azure App Service]
    AppService -- ConnectionStrings__YardDb --> SQL[(Azure SQL)]
```

`.github/workflows/ci.yml` has two jobs: `build-and-test` runs on every push and PR (build, then the full test suite against a real SQL Server service container); `deploy` runs only after that passes, only on a push to `main` — never on a PR, so a fork can't trigger a deploy — and publishes straight to App Service via its publish profile.

**One-time setup this repo can't do for itself** (needs an Azure subscription, which this environment doesn't have):

1. Create the Azure SQL database (free tier is enough for this) and an App Service (Linux, .NET 10 runtime) — via the portal or `az cli`.
2. Point the App Service at the database: Application Setting `ConnectionStrings__YardDb` = the Azure SQL connection string. `Program.cs` already reads it via `GetConnectionString("YardDb")` — no code change needed, the double-underscore is ASP.NET Core's standard nested-configuration-key convention for environment variables/App Settings.
3. Apply the schema once, before the app ever takes traffic: `dotnet ef database update --project src/PortYard.Api --connection "<the Azure SQL connection string>"` from a machine that can reach it (or via the Azure Cloud Shell). This is deliberately not automatic — see "Migrations run at app startup only in Development" below.
4. Set `ASPNETCORE_ENVIRONMENT=Production` as an App Service Application Setting (App Service defaults it to `Production` already, but pin it explicitly rather than relying on the default).
5. In this GitHub repo: add secret `AZURE_WEBAPP_PUBLISH_PROFILE` (Azure Portal → the App Service → **Get publish profile**, paste the whole XML) and variable `AZURE_WEBAPP_NAME` (the App Service's name). The `deploy` job fails at exactly one step until both exist; everything before that (build, test, publish) already runs and passes without them.
6. Push to `main`. Then put the real URL at the top of this section.

Managed identity (App Service → Azure SQL, no password in the connection string at all) is a better long-term answer than a SQL login-and-password connection string, and worth doing before this holds anything real — not done here since it needs the same Azure access as everything else in this checklist.

## Testing

```bash
dotnet test
```

65 tests: 53 unit tests against the domain layer (ISO 6346 validation, status transitions — legal and illegal — slot capacity, and movement chronology) with no database involved, plus 12 integration tests that run the full ASP.NET Core pipeline against a real SQL Server database via `WebApplicationFactory`. Each test class gets its own uniquely named database (created via `Database.Migrate()` — the same call `Program.cs` makes in Development, so a green run also proves the committed migration matches the current model, not just that `EnsureCreated` can build *some* database that does — and dropped on disposal), rather than EF Core's `UseInMemoryDatabase` provider, which doesn't enforce relational constraints — foreign keys and unique indexes — and wouldn't exercise real SQL Server T-SQL at all.

The integration suite needs a reachable SQL Server — the same local Docker container from Quick Start works, or set `PORTYARD_TEST_CONNECTION_STRING` to point at another one. CI runs it against a fresh `mcr.microsoft.com/mssql/server:2022-latest` service container every run (see `.github/workflows/ci.yml`).

## Design decisions and trade-offs

**SQL Server, not SQLite.** This ran on SQLite through most of development — a reviewer could clone the repo and run it with literally zero setup, which was worth a lot early on. It moved to SQL Server once the project's actual point became being deployed (App Service + Azure SQL) rather than just being clonable: nothing in the domain layer needed to change for the swap (that was the bet the SQLite choice made, and it paid off — the only real casualty was `Database.Migrate()`'s target changing, plus regenerating one migration file), but a demo that only proves it runs on SQLite doesn't actually demonstrate it runs on the database this is deployed against. The trade-off is real, though: "zero setup" became "one `docker run`."

**Migrations run at app startup only in Development (and demo data only in Development and Staging).** This used to be gated on `!IsDevelopment()`, which meant *any* non-Development environment — Staging, Production, anything — started with no schema and no data, and would 500 on the first real request. Applying a migration automatically whenever an instance happens to boot is also just the wrong place for it once there's more than one instance: two instances racing to migrate the same database on startup is a real failure mode, not a hypothetical one. Migrations belong to the deploy pipeline (`dotnet ef database update`, run once, before traffic is ever routed to the new version) — Development keeps the auto-migrate-and-seed convenience for a zero-setup `dotnet run`, Staging seeds demo data onto an already-migrated schema so a reviewer gets a populated yard without ever touching a production database, and Production does neither at runtime.

**Enums stored as strings, not ints.** A `Status` column that reads `'Stored'` in the database is a schema you can debug by eye; a column that reads `2` is not. The storage cost is trivial at this scale.

**The movement ledger is append-only by construction**, not by convention — there is no method on `Movement` that mutates an existing record, and `Container` only ever adds to its movement collection. That's what makes it trustworthy as an audit trail: a movement, once recorded, cannot quietly change under you.

**No authentication.** Every write endpoint records a fixed `"gate-system"` operator rather than an authenticated user. Given a next iteration, this is the first thing I'd add — real per-operator attribution matters as soon as this stops being a demo.

**Optimistic concurrency on slot assignment**, via `YardSlot.LastModifiedAt` mapped as an `IsConcurrencyToken()` (not `IsRowVersion()` — SQLite has no server-generated rowversion type the way SQL Server does, so the domain sets it explicitly on every occupancy change instead, which also means the mechanism doesn't change when the database provider does). Two concurrent `assign-slot` calls for the same slot can each read it before either one writes, each individually pass the capacity check against that now-stale snapshot, and — without this — both commit, over-filling the slot past `MaxTeu`. The second writer's `SaveChanges` now fails with `DbUpdateConcurrencyException` instead, which `ContainerService.AssignSlotAsync` catches and turns into the same 409 every other domain conflict returns. Covered by `SlotConcurrencyTests`, which reproduces the race deterministically (two `DbContext`s, each loading its own snapshot before either saves) rather than depending on real thread timing.

**No real vessel scheduling.** Vessels exist only as a nullable reference containers can point to; there's no berth planning, no ETA-driven workflow. That's a different, larger project.

**`/health` checks the database, not just the process.** `AddDbContextCheck<YardDbContext>()` means an instance whose SQL connection has died reports unhealthy and can be taken out of rotation by App Service's health-check feature, instead of serving requests that are all going to fail anyway.

**CORS is configuration-driven and off by default.** No frontend exists yet, so there's no real origin to hardcode. `Cors:AllowedOrigins` (empty by default) becomes an App Service Application Setting — `Cors__AllowedOrigins__0=https://...` — the day a real caller needs it, with no code change.
