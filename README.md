# GraphRoots

.NET reference implementation of the GraphRoots computational graph store: a tool-neutral Neo4j core, a Grasshopper importer, an import CLI, and a GraphQL API.

Consortium context (who we are, why this exists, how we meet): [GRAPHROOTS.md](GRAPHROOTS.md).

## What this is

| Project | Role |
| --- | --- |
| `GraphDbInterfaces` | Tool-neutral model, store contracts, UUID5 |
| `GraphDb` | Neo4j mapper, schema, GraphQL-facing store |
| `GrasshopperInterfaces` / `GrasshopperLib` | GHX/GH parser and snapshot builder (`gh.*` extensions) |
| `GraphDbCli` | `import` command: parse GHX/GH, POST `importSnapshot` |
| `GraphApi` | GraphQL host (no Grasshopper dependency) |
| `GraphDbTests` / `GraphApiTests` | Offline suite plus optional live Neo4j tests |

Also in the tree: [docs/](docs/README.md) (graph model and GraphQL contract), [testdata/](testdata/) (parser fixtures), [docker/neo4j/](docker/neo4j/compose.yaml) (optional app database and isolated live-test database), [lib/](lib/README.md) (local `GH_IO.dll`, not committed).

## What is not here

Dynamo loader, explorer UI, auth/tenancy, cloud solver. `GraphApi` is local-only (`AllowedHosts` is localhost; there is no authentication).

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) ([global.json](global.json) pins `10.0.300`)
- [Neo4j](https://neo4j.com/download/) for the GraphQL host (and CLI `--purge`; Community is supported), or Docker for the optional app database and live tests
- A Rhino 8 install to copy `GH_IO.dll` (needed for binary `.gh` files and nested cluster archives)
- On macOS / Linux, `libgdiplus` for binary `.gh` import: `brew install mono-libgdiplus` or `apt install libgdiplus`

## Obtain GH_IO

McNeel’s `GH_IO.dll` is covered by the Rhino EULA and is **not** in this repository. Copy the plugin assembly (not the `ref/net48` stub) to `lib/GH_IO.dll`. Paths and the Unix `libgdiplus` notes: [lib/README.md](lib/README.md).

XML `.ghx` files do not load this DLL for the outer archive. Binary `.gh` files and cluster documents stored as `gh_bytearray` do.

## Configure Neo4j

`GraphApi` and CLI `--purge` share a user-secrets store ([Directory.Build.props](Directory.Build.props)). The password comes **only** from user secrets or `NEO4J_PASSWORD` — never a CLI flag. GraphQL import does not need Neo4j credentials on the CLI.

```bash
dotnet user-secrets set NEO4J_PASSWORD "<password>" --project GraphDbCli
dotnet user-secrets set NEO4J_URI "neo4j://127.0.0.1" --project GraphDbCli
# optional: NEO4J_USER (default neo4j), NEO4J_DATABASE (default GraphRoots)
```

`GraphApi` reads the same secrets when `ASPNETCORE_ENVIRONMENT=Development` (the default launch profile).

| Variable | Default |
| --- | --- |
| `NEO4J_URI` | `neo4j://127.0.0.1` |
| `NEO4J_USER` | `neo4j` |
| `NEO4J_PASSWORD` | (required) |
| `NEO4J_DATABASE` | `GraphRoots` |

Point GraphApi (and CLI `--purge`) at a database you own. You can run Neo4j yourself, or start the optional Compose app database (Neo4j `2026.08-community`, Cypher 25, database `GraphRoots` on `neo4j://127.0.0.1`). A local Neo4j already bound on 7687/7474 will conflict.

```bash
docker compose -f docker/neo4j/compose.yaml up -d --wait neo4j-app
dotnet user-secrets set NEO4J_PASSWORD "graphroots-app" --project GraphDbCli
```

`docker compose -f docker/neo4j/compose.yaml down -v` also drops `neo4j-app-data` (the Community default database name is set only on first DBMS create).

The `neo4j-test` service (`graphtest` on port 17687) is a **test** instance and is purged per live test; do not use it as the app database.

## Run

Start GraphApi (it applies `EnsureSchema` on startup), then import. The CLI POSTs `importSnapshot` to GraphQL (default `http://127.0.0.1:5088/graphql`, or `GRAPHQL_URL` / `--graphql-url`).

```bash
dotnet run --project GraphApi
dotnet run --project GraphDbCli -- import --path testdata/ParserExample1.ghx
```

GraphQL playground (Banana Cake Pop): [http://127.0.0.1:5088/graphql](http://127.0.0.1:5088/graphql).

`--purge --yes` still opens Neo4j and wipes all nodes. The CLI prints the GraphQL target before import, and the Neo4j URI before purge. Import no longer calls `EnsureSchema`.

## Tests

Offline (live Neo4j classes skip unless `NEO4J_TEST_*` is set):

```bash
dotnet test GraphRoots.sln
```

Live store tests against the disposable Compose database:

```bash
docker compose -f docker/neo4j/compose.yaml up -d --wait
dotnet test GraphDbTests --settings docker/neo4j/live-tests.runsettings
dotnet test GraphApiTests --settings docker/neo4j/live-tests.runsettings
```

That container is Neo4j `2026.08-community`, database `graphtest`, Bolt `bolt://127.0.0.1:17687`. Tests refuse `GraphRoots` / `neo4j` / `system` and purge `graphtest` before each case.

`docker compose -f docker/neo4j/compose.yaml down -v` recreates `graphtest` and drops `neo4j-app-data` (the Community default database name is set only on first DBMS create).

## Further reading

- [Computational graph model](docs/graph-model.md)
- [GraphQL API](docs/graphql/README.md) — contract: [schema.graphql](docs/graphql/schema.graphql)
- [Contributing](CONTRIBUTING.md)

## License

[MIT](LICENSE)
