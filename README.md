# PortYard

Container yard management API for a port terminal, built with ASP.NET Core and EF Core.

[![CI](https://github.com/Bergstefann/container-yard-management/actions/workflows/ci.yml/badge.svg)](https://github.com/Bergstefann/container-yard-management/actions/workflows/ci.yml)

## What this is, and why

A container terminal has to answer three questions correctly, all the time: where is every box physically sitting, which ones is the terminal legally allowed to release through the gate, and how full is the yard right now. Get any of those wrong and the consequences are concrete — a container nobody can locate, cargo released under an active customs hold, or a yard block that silently overflows its capacity. PortYard models that domain directly: containers move through a strict lifecycle (expected, gated in, stored, staged, gated out), every physical move is written to an append-only ledger, and the rules that govern slot capacity, reefer placement, and customs holds are enforced by the domain model itself rather than trusted to whichever caller happens to be writing to the database that day.

This is a portfolio project, not a production system, but it's built the way the underlying problem deserves: a domain layer with no framework dependencies and real invariants, a persistence layer that maps cleanly onto it, a REST API that never lets a controller mutate state directly, and a test suite that exists to prove the business rules hold rather than to pad a coverage number.

## Quick start

```bash
git clone https://github.com/Bergstefann/container-yard-management.git
cd container-yard-management
dotnet run --project src/PortYard.Api
```

The SQLite database is created and seeded automatically on first run — no external database, no setup step. Open `https://localhost:<port>/swagger` (the port is printed on startup) to browse and try every endpoint.

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

## Testing

```bash
dotnet test
```

63 tests: 53 unit tests against the domain layer (ISO 6346 validation, status transitions — legal and illegal — slot capacity, and movement chronology) with no database involved, plus 10 integration tests that run the full ASP.NET Core pipeline against a real SQLite database via `WebApplicationFactory`. The integration tests deliberately use a live SQLite connection rather than EF Core's `UseInMemoryDatabase` provider, because that provider doesn't enforce relational constraints — foreign keys and unique indexes — which would silently defeat the point of testing them.

## Design decisions and trade-offs

**SQLite, not SQL Server or PostgreSQL.** A reviewer should be able to clone this repository and run it with zero setup. In a real deployment this would be PostgreSQL or SQL Server; nothing in the domain layer or the EF Core configuration is SQLite-specific enough to make that migration painful.

**Enums stored as strings, not ints.** A `Status` column that reads `'Stored'` in the database is a schema you can debug by eye; a column that reads `2` is not. The storage cost is trivial at this scale.

**The movement ledger is append-only by construction**, not by convention — there is no method on `Movement` that mutates an existing record, and `Container` only ever adds to its movement collection. That's what makes it trustworthy as an audit trail: a movement, once recorded, cannot quietly change under you.

**No authentication.** Every write endpoint records a fixed `"gate-system"` operator rather than an authenticated user. Given a next iteration, this is the first thing I'd add — real per-operator attribution matters as soon as this stops being a demo.

**No optimistic concurrency on slot assignment.** Two concurrent `assign-slot` calls for the same slot are each individually consistent (the capacity check re-reads current occupancy inside the request), but there's a narrow race between the check and the save under real concurrent load. A `RowVersion` / `[ConcurrencyCheck]` token on `YardSlot` would close that gap.

**No real vessel scheduling.** Vessels exist only as a nullable reference containers can point to; there's no berth planning, no ETA-driven workflow. That's a different, larger project.
