# PawTrack

ASP.NET Core 10 (Razor Pages) app that lists vet visits for users and their pets.

## Data model

```
User 1--* Pet 1--* VetVisit *--1 Veterinarian
```

Data lives in PostgreSQL. The `postgres` compose service restores `db/init/pawtrack.sql.gz` (a `pg_dump` with about 5,000 users, 12,500 pets, 40 vets and 56,000 visits) the first time it starts with an empty volume.

| Page / endpoint | Query |
|---|---|
| `/` | Visits joined with pets, owners and vets, filterable by species and city, 500 rows per page |
| `/Owners` | Owners joined with pets and visits, grouped by owner (pet count, visit count, total spend, last visit) |
| `/api/visits`, `/api/owners` | The same queries returned as JSON |
| `/health` | Liveness check (does not touch Redis) |

The LINQ queries are in `src/PawTrack.Web/Queries/VetVisitQueries.cs`. Results are cached in Redis for one minute through `ICacheStore`.

## Running locally

Dev (Postgres on `localhost:5432` and Redis on `localhost:6379`):

```sh
docker compose up -d postgres redis
dotnet run --project src/PawTrack.Web
```

Production-like (app, Postgres and Redis in Docker):

```sh
docker compose up -d --build
# http://localhost:8080
```

### Database backup

The backup is only restored into an empty volume. To reset the database to the backup:

```sh
docker compose down -v
docker compose up -d
```

To replace the backup with the current database contents:

```sh
docker compose exec postgres pg_dump -U pawtrack -d pawtrack --no-owner --no-privileges | gzip -9 > db/init/pawtrack.sql.gz
```

## Load testing

k6 runs as a compose service under the `loadtest` profile, so it doesn't need a local install:

```sh
docker compose --profile loadtest run --rm k6
```

The script is `loadtest/browse-visits.js`. It ramps up to 150 virtual users that browse the visits page, the owners page and the API.
