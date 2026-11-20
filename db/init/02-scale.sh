#!/bin/sh
# Copies the restored backup so the tables hold PAWTRACK_SCALE times the data, making cache misses expensive.
# The postgres entrypoint sources this file, so it must not call exit.

scale="${PAWTRACK_SCALE:-1}"

if [ "$scale" -gt 1 ]; then
    echo "Scaling PawTrack data ${scale}x..."
    psql -v ON_ERROR_STOP=1 -v scale="$scale" --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" <<'EOSQL'
CREATE TEMP TABLE base AS
SELECT (SELECT max("Id") FROM "Users") AS users,
       (SELECT max("Id") FROM "Pets") AS pets,
       (SELECT max("Id") FROM "VetVisits") AS visits;

-- Copy k shifts every id by k times the original max id, so foreign keys stay consistent within each copy.
INSERT INTO "Users" ("Id", "FirstName", "LastName", "Email", "City", "CreatedAt")
SELECT u."Id" + k * b.users, u."FirstName", u."LastName", k || '.' || u."Email", u."City", u."CreatedAt"
FROM "Users" u, base b, generate_series(1, :scale - 1) k;

INSERT INTO "Pets" ("Id", "UserId", "Name", "Species", "Breed", "DateOfBirth")
SELECT p."Id" + k * b.pets, p."UserId" + k * b.users, p."Name", p."Species", p."Breed", p."DateOfBirth"
FROM "Pets" p, base b, generate_series(1, :scale - 1) k;

INSERT INTO "VetVisits" ("Id", "PetId", "VeterinarianId", "VisitDate", "Reason", "Diagnosis", "Cost")
SELECT v."Id" + k * b.visits, v."PetId" + k * b.pets, v."VeterinarianId", v."VisitDate", v."Reason", v."Diagnosis", v."Cost"
FROM "VetVisits" v, base b, generate_series(1, :scale - 1) k;

SELECT setval(pg_get_serial_sequence('"Users"', 'Id'), max("Id")) FROM "Users";
SELECT setval(pg_get_serial_sequence('"Pets"', 'Id'), max("Id")) FROM "Pets";
SELECT setval(pg_get_serial_sequence('"VetVisits"', 'Id'), max("Id")) FROM "VetVisits";

ANALYZE;
EOSQL
fi
