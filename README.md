# PawTrack

App that lists vet visits for users and their pets: a React frontend (`src/PawTrack.Web/ClientApp`) on an ASP.NET Core 10 API.

## Data model

```
User 1--* Pet 1--* VetVisit *--1 Veterinarian
```

Data lives in PostgreSQL. The first time the `postgres` compose service starts with an empty volume, it:

1. restores `db/init/01-pawtrack.sql.gz`, a `pg_dump` with about 5,000 users, 12,500 pets, 40 vets and 56,000 visits;
2. runs `db/init/02-scale.sql`, which copies that data `PAWTRACK_SCALE` times (default 50: about 250,000 users, 630,000 pets and 2.8 million visits, 570 MB). Startup takes about a minute;
3. runs `db/init/03-appointments.sql`, which creates the `Appointments` table and gives about one in five pets an appointment in the next 60 days (about 70% confirmed, 30% pending). Appointments are generated relative to the current date rather than stored in the backup, so they don't go stale.

| Endpoint | Query | Used by |
|---|---|---|
| `/api/visits` | Past visits joined with pets, owners and vets, filterable by `species` and `city`, paged with `page` and `pageSize` (max 1000) | Past visits & invoices page (`/`), 500 rows per page |
| `/api/appointments` | Future appointments joined with pets, owners and vets, soonest first. `status=upcoming` (default) for confirmed, `status=pending` for awaiting confirmation | Appointments page (`/appointments`), one tab per status, 500 rows per page |
| `/api/owners` | Owners joined with pets and visits, grouped by owner (pet count, visit count, total spend, last visit), filterable by `city` | Owners page (`/owners`), 500 rows per page |
| `/health` | Liveness check (does not touch Redis) | |

Any other path that isn't a file returns the React app's `index.html`, so client-side routes work when loaded directly.

The LINQ queries are in `src/PawTrack.Web/Queries/VetVisitQueries.cs`. Results are cached in Redis for one minute through `ICacheStore`. If Redis can't be reached, requests read from the database instead.

## Running locally

Dev (needs Node 22 and the .NET 10 SDK):

```sh
docker compose up -d postgres redis
dotnet run --project src/PawTrack.Web     # API on http://localhost:5064

cd src/PawTrack.Web/ClientApp
npm install
npm run dev                               # http://localhost:5173, proxies /api to the API
```

`npm run build` type-checks the app and builds it into `src/PawTrack.Web/wwwroot`, which `dotnet run` then serves on port 5064. The Docker image does this build itself.

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
docker compose exec postgres pg_dump -U pawtrack -d pawtrack --no-owner --no-privileges --exclude-table='"Appointments"' | gzip -9 > db/init/01-pawtrack.sql.gz
```

## Load testing

k6 runs as a compose service under the `loadtest` profile, so it doesn't need a local install:

```sh
docker compose --profile loadtest run --rm k6
```

The script is `loadtest/browse-visits.js`. Each iteration makes the API calls the visits and owners pages make (500 rows each), plus a smaller visits API call (100 rows). Traffic arrives at a fixed rate rather than from a fixed number of users, so it keeps coming when responses slow down:

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

### Crawling uncached pages

`loadtest/crawl-pending.js` runs a crawler that requests visits pages nobody has opened (every request misses the cache) alongside a staff member opening the pending appointments queue once a second. Pending is never cached, so it needs a free database connection on every request.

```sh
docker compose up -d --build web
docker compose run --rm --no-deps k6 run /scripts/crawl-pending.js
```

The failure needs the default data scale, so the crawler's queries are slow enough to hold every connection. Check before running:

```sh
docker compose exec postgres psql -U pawtrack -d pawtrack -c 'select (select count(*) from "VetVisits") visits, (select count(*) from "Pets") pets, (select count(*) from "Appointments") appointments;'
```

Expect about 2.8 million visits, 628,000 pets and 125,000 appointments. Much smaller numbers mean the database volume was created at a smaller scale; it only applies when the volume is first created, so run `docker compose down -v` (this deletes the database volume) and start again.

`--no-deps` stops the run from recreating the web container. On Windows with Git Bash, prefix the command with `MSYS_NO_PATHCONV=1` so `/scripts/...` isn't rewritten as a Windows path.

The test passes only if every pending request succeeds with a p95 under 500 ms, and the app's peak usage stays within its limits. The peaks come from `/api/diagnostics/load`, which is only served when `LoadDiagnostics:Enabled` is set (the compose web service sets it). At the end the test prints a report:

| Row | Allowed | What it measures |
|---|---|---|
| DB connections in use | below the pool size | Peak connections in use, read from Npgsql's own pool count (sampled every 20 ms), including the pending queue's |
| Cache-miss queries let through, all clients | `CacheMissLimits:Global`, or a target of 10 | Peak cache-miss queries running at once |
| Cache-miss queries let through, one client | `CacheMissLimits:PerClient`, or a target of 5 | The same, for the busiest IP address |
| Pending queue failures, p95 | 0, under 500 ms | The staff member's requests |

Without limits configured, the queries are measured against the targets. On the current code every row fails: the crawler takes all 15 connections and about half the pending requests fail.

The app identifies clients by IP address, and every k6 container gets its own. To crawl from several addresses, start the run above, then a few seconds later start more containers with `CRAWL_ONLY=1`. They add crawlers but leave the staff member, the reset and the report to the first run:

```sh
docker compose run --rm --no-deps -e CRAWL_ONLY=1 k6 run --quiet /scripts/crawl-pending.js
```

`CRAWLERS` sets the number of crawler users per container (default 40).
