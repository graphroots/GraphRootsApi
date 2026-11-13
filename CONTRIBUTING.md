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
dotnet test GraphApiTests --settings docker/neo4j/live-tests.runsettings
```

Run them one after the other; both purge the same test database. Do not point those variables at `GraphRoots`, `neo4j`, or `system`.

## Storage backends

`GraphApi` and `GraphDbCli` only see `IGraphStore` and `IGraphStoreAdmin`; `GraphDb.Hosting` picks the implementation from `GRAPHROOTS_STORE`. To add a backend:

1. Create `GraphDb.<Name>` referencing `GraphDbInterfaces` only, and implement `IGraphStore` and `IGraphStoreAdmin`.
2. Reuse the shared contract code instead of re-implementing it: `StoreLimits`, `ExtensionKeys`, `ImportSnapshotValidator`, `GraphPatchValidator`, `SubgraphPatternValidator`, and `DocumentLifecycle` (writability, fork parents, merge parents).
3. Add an `Add<Name>GraphStore(...)` registration and a case in `GraphStoreBackends` (`GraphDb.Hosting`).
4. In the backend's test project, derive a `[TestClass]` from `GraphStoreContractTestBase` (`GraphStoreContractTests`), and from `GraphQLStoreTestBase` in `GraphApiTests`. Behaviour every backend must share goes into those base classes, seeded only through the public store contract; backend-specific tests stay in the backend's own test project.

## Collaboration

We meet biweekly. Appointment schedule: [Google Calendar](https://calendar.google.com/calendar/appointments/schedules/AcZssZ3MHGFxNyo_51N_Q2cCg3OST3vNfHb87aGz7R2pIgCKnjQEsbsvXZU7n4IVvMkQaVZSOSTw_2Xz).
