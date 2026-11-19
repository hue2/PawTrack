# PawTrack

ASP.NET Core 10 (Razor Pages) app that lists vet visits for users and their pets.

## Data model

```
User 1--* Pet 1--* VetVisit *--1 Veterinarian
```

The app seeds about 5,000 users, 12,000 pets, 40 vets and 60,000 visits into SQLite on first start.

| Page / endpoint | Query |
|---|---|
| `/` | Visits joined with pets, owners and vets, filterable by species and city, 500 rows per page |
| `/Owners` | Owners joined with pets and visits, grouped by owner (pet count, visit count, total spend, last visit) |
| `/api/visits`, `/api/owners` | The same queries returned as JSON |
| `/health` | Liveness check (does not touch Redis) |

The LINQ queries are in `src/PawTrack.Web/Queries/VetVisitQueries.cs`. Results are cached in Redis for one minute through `ICacheStore`.

## Running locally

Dev (needs a Redis instance on `localhost:6379`):

```sh
docker run -d -p 6379:6379 redis:7.4-alpine
dotnet run --project src/PawTrack.Web
```

Production-like (app and Redis in Docker):

```sh
docker compose up -d --build
# http://localhost:8080
```

## Load testing

k6 runs as a compose service under the `loadtest` profile, so it doesn't need a local install:

```sh
docker compose --profile loadtest run --rm k6
```

The script is `loadtest/browse-visits.js`. It ramps up to 150 virtual users that browse the visits page, the owners page and the API.
