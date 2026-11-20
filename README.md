# PawTrack

ASP.NET Core 10 (Razor Pages) app that lists vet visits for users and their pets.

## Data model

```
User 1--* Pet 1--* VetVisit *--1 Veterinarian
```

Data lives in PostgreSQL. The first time the `postgres` compose service starts with an empty volume, it:

1. restores `db/init/01-pawtrack.sql.gz`, a `pg_dump` with about 5,000 users, 12,500 pets, 40 vets and 56,000 visits;
2. runs `db/init/02-scale.sh`, which copies that data `PAWTRACK_SCALE` times (default 50: about 250,000 users, 630,000 pets and 2.8 million visits, 570 MB). Startup takes about a minute.

| Page / endpoint | Query |
|---|---|
| `/` | Visits joined with pets, owners and vets, filterable by species and city, 500 rows per page |
| `/Owners` | Owners joined with pets and visits, grouped by owner (pet count, visit count, total spend, last visit) |
| `/api/visits`, `/api/owners` | The same queries returned as JSON |
| `/health` | Liveness check (does not touch Redis) |

The LINQ queries are in `src/PawTrack.Web/Queries/VetVisitQueries.cs`. Results are cached in Redis for one minute through `ICacheStore`. If Redis can't be reached, requests read from the database instead.

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

The backup is only restored into an empty volume. To reset the database, or to apply a different scale:

```sh
docker compose down -v
PAWTRACK_SCALE=10 docker compose up -d
```

To replace the backup with the current database contents, start from an unscaled database so the backup stays small:

```sh
docker compose down -v
PAWTRACK_SCALE=1 docker compose up -d postgres
# make your changes, then:
docker compose exec postgres pg_dump -U pawtrack -d pawtrack --no-owner --no-privileges | gzip -9 > db/init/01-pawtrack.sql.gz
```

## Load testing

k6 runs as a compose service under the `loadtest` profile, so it doesn't need a local install:

```sh
docker compose --profile loadtest run --rm k6
```

The script is `loadtest/browse-visits.js`. Each iteration requests the visits page, the owners page and the visits API. Traffic arrives at a fixed rate rather than from a fixed number of users, so it keeps coming when responses slow down:

| Time | Iterations per second | Purpose |
|---|---|---|
| 0–20s | 10 | Warm-up: fills the cache |
| 20–60s | 10 → 200 | Ramp |
| 60–90s | 200 | Peak |
| 90–100s | 200 → 0 | Ramp down |

The test aborts five seconds after more than 0.1% of requests fail.

With the default data scale it reproduces a cascading failure at about 65 seconds, when the cached entries from the warm-up expire under peak load:

1. Cache misses go to the database and take about a second each.
2. The app opens a new Redis connection per request (`RedisServiceCollectionExtensions.cs`) and holds it for the whole request, so slow requests use up Redis's 50-client limit (`--maxclients 50`).
3. Redis refuses new connections, so requests that would have been cache hits also go to the database.
4. The database's connection pool (15 connections) runs out, and requests fail with "connection pool has been exhausted" or time out.
