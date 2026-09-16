# Contributing

Consortium meetings and context: [GRAPHROOTS.md](GRAPHROOTS.md). How to run the tree: [README.md](README.md).

## Setup

1. Install the .NET 10 SDK pinned in [global.json](global.json).
2. Copy `GH_IO.dll` from a local Rhino 8 install to `lib/GH_IO.dll` ([lib/README.md](lib/README.md)).
3. Set Neo4j credentials with user secrets or `NEO4J_PASSWORD` ([README.md](README.md)). Optional Compose app database: `docker compose -f docker/neo4j/compose.yaml up -d --wait neo4j-app`.

On macOS / Linux, install `libgdiplus` if you import binary `.gh` files.

## Build

The solution treats warnings as errors. A contribution should build clean:

```bash
dotnet build GraphRoots.sln
```

Style lives in [.editorconfig](.editorconfig). File-scoped and block namespaces are both used; do not mass-convert them.

## Tests

```bash
dotnet test GraphRoots.sln
```

Live Neo4j tests skip unless `NEO4J_TEST_URI` and `NEO4J_TEST_DATABASE` are set. The disposable Compose database:

```bash
docker compose -f docker/neo4j/compose.yaml up -d --wait
dotnet test GraphDbTests --settings docker/neo4j/live-tests.runsettings
```

Do not point those variables at `GraphRoots`, `neo4j`, or `system`.

## Collaboration

We meet biweekly. Appointment schedule: [Google Calendar](https://calendar.google.com/calendar/appointments/schedules/AcZssZ3MHGFxNyo_51N_Q2cCg3OST3vNfHb87aGz7R2pIgCKnjQEsbsvXZU7n4IVvMkQaVZSOSTw_2Xz).
