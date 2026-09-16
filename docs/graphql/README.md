# GraphQL API

The GraphQL SDL ([schema.graphql](schema.graphql)) is the consortium contract for reading and writing computational graphs. The .NET host (`GraphApi`) is the reference implementation. It sits on the tool-neutral store ([graph-model.md](../graph-model.md)) and has no Grasshopper dependency. CLI `import` parses GHX/GH and POSTs `importSnapshot`.

Setup (SDK, Neo4j secrets, `GH_IO.dll`, tests): [README.md](../../README.md).

See also [access-patterns.md](access-patterns.md), [subgraph-matching.md](subgraph-matching.md), and [mutations.md](mutations.md).

## Principles

- **SDL is authoritative.** The checked-in schema is the contract. The reference host must export an identical schema (`SchemaSnapshotTest` compares canonical SDL, including arguments, nullability, defaults, enums, and descriptions).
- **No Cypher in the public API.** Pattern matching is a typed input, compiled server-side to parameterized Cypher. Extension keys are never interpolated into identifiers.
- **Composite keys stay explicit.** Store identity is `documentId` + `versionId`, `versionId` + `nodeId`, and the other equality keys from the graph model. `id` on each entity is an opaque refetch key derived from those fields, not a replacement for them. `Query.entity(id)` round-trips that key.
- **Imported documents and commits are immutable.** Mutations targeting `origin != GRAPHROOTS` or `committed: true` fail with `IMMUTABLE_DOCUMENT`, except `importSnapshot`, which MERGEs grasshopper/dynamo source snapshots. Edits apply to a `forkDocument` working copy (`origin: GRAPHROOTS`, `committed: false`). `commitDocument` / `mergeDocument` freeze new snapshots and record `BASED_ON` parents. Copied nodes keep their original `origin` and `HAS_INSTANCE` catalog links.
- **Extensions stay namespaced key/value** (`gh.*`, later `dyn.*`). Keys must match `prefix.localName` with a lowercase namespace. Values are scalars or arrays of scalars (not nested JSON objects).
- **Cost limits.** Query depth, field cost, result size, pattern size, and match timeout are enforced. Corpus-wide `matchSubgraph` requires an explicit `first` / limit.

## Hosting

```
dotnet run --project GraphApi
```

Default bind: `http://127.0.0.1:5088`. Banana Cake Pop is at `/graphql`. There is no authentication in v1.

The host refuses to start without `NEO4J_PASSWORD`, verifies connectivity to `NEO4J_DATABASE` (not the server default `neo4j`), and applies the expected constraints/indexes (`EnsureSchema`) before serving traffic.

| Variable | Default |
| --- | --- |
| `NEO4J_URI` | `neo4j://127.0.0.1` |
| `NEO4J_USER` | `neo4j` |
| `NEO4J_PASSWORD` | (required) |
| `NEO4J_DATABASE` | `GraphRoots` |
| `ASPNETCORE_URLS` | `http://127.0.0.1:5088` |

Identity constraints use Neo4j Enterprise `NODE KEY` when available and fall back to `UNIQUE` on Community. Relationship uniqueness for `EDGE` is always `UNIQUE`.

Live store tests require an isolated database and never purge `GraphRoots` / `neo4j` / `system`:

| Variable | Meaning |
| --- | --- |
| `NEO4J_TEST_URI` | Bolt URI for live tests (required; skip if unset) |
| `NEO4J_TEST_DATABASE` | Disposable database name (required; reserved names are refused) |
| `NEO4J_TEST_USER` / `NEO4J_TEST_PASSWORD` | Optional overrides; otherwise `NEO4J_USER` / `NEO4J_PASSWORD` |

A Compose service `neo4j-test` in [docker/neo4j/compose.yaml](../../docker/neo4j/compose.yaml) provides that database (`graphtest` on `bolt://127.0.0.1:17687`, Neo4j `2026.08-community`, Cypher 25). Do not point GraphApi or the CLI at it; tests purge it before each case.

For the CLI and API, run Neo4j yourself or start the optional `neo4j-app` service (defaults: `neo4j://127.0.0.1`, database `GraphRoots`; a local Neo4j on 7687/7474 will conflict):

```bash
docker compose -f docker/neo4j/compose.yaml up -d --wait neo4j-app
dotnet user-secrets set NEO4J_PASSWORD "graphroots-app" --project GraphDbCli
```

Live tests:

```bash
docker compose -f docker/neo4j/compose.yaml up -d --wait
dotnet test GraphDbTests --settings docker/neo4j/live-tests.runsettings
```

`docker compose -f docker/neo4j/compose.yaml down -v` recreates `graphtest` and drops `neo4j-app-data` (the Community default database name is set only on first DBMS create). Offline `dotnet test` without `NEO4J_TEST_*` still skips the live classes.

## Limits

| Limit | Value |
| --- | --- |
| Query depth | 16 |
| Request timeout | 120s (`importSnapshot` payloads are large; other operations share this host timeout) |
| `matchSubgraph` timeout | 10s (`MATCH_TIMEOUT`) |
| Field cost | 1000 (`importSnapshot` is cost 0) |
| Type cost | 100 000 (raised so thousand-object import inputs do not fail `MaxTypeCost`) |
| Collection `first` | default 50, max 500 |
| Match `first` | max 100; required when unscoped |
| Dense `Document.graph` / `importSnapshot` | 100 000 entities across nodes+ports+edges (`RESULT_TOO_LARGE`) |
| Patch operations | 500 (200 nodes / 400 ports / 400 edges) |

## Errors

GraphQL errors use `extensions.code`:

| Code | Meaning |
| --- | --- |
| `NOT_FOUND` | Entity key does not exist |
| `IMMUTABLE_DOCUMENT` | Write targeted a non-`graphroots` document or a `committed` snapshot |
| `INVALID_PATTERN` | Pattern failed validation (mode, keys, references) |
| `PATTERN_TOO_LARGE` | Pattern exceeded node/port/edge limits |
| `LIMIT_REQUIRED` | Unscoped match without `first` |
| `CONFLICT` | Create on an existing key, or patch invariant broken |
| `INVALID_ARGUMENT` | Filter, hop range, extension key, or patch field is invalid |
| `MATCH_TIMEOUT` | Matcher exceeded its 10s budget |
| `RESULT_TOO_LARGE` | Dense graph or result exceeded configured size |
| `STORE_ERROR` | Neo4j or unexpected store failure |

Unknown stored origins are reported as `UNKNOWN`. `UNKNOWN` is not a writable origin.

## Out of scope (v1)

`purgeDatabase` mutation, Dynamo-specific types, subscriptions, auth/tenancy, raw Cypher, in-place edit of imported versions, rewriting `NESTS` on fork, solver/runtime labels. Server-side `.gh`/`.ghx` upload is also out of scope (`GraphApi` stays Grasshopper-free).
