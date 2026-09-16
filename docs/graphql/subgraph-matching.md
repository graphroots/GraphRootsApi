# Subgraph matching

`matchSubgraph` finds occurrences of a typed pattern in the store. The pattern is a GraphQL input, never a Cypher string.

## Modes

`SubgraphPatternInput` is valid in exactly one mode:

1. **Port-explicit** — `nodes` + `ports` + `edges`. Homomorphism onto `Node` / `Port` / `EDGE`.
2. **Node-connection** — `nodes` + `connections`. A connection is realized by a `Node-HAS_PORT-Port-EDGE-Port-HAS_PORT-Node` walk (optional port name/direction; optional hop range).

Supplying both `edges` and `connections`, or neither when the pattern has more than one node that must be related, is `INVALID_PATTERN`. A single-node pattern with no edges/connections is allowed (find instances).

Disconnected pattern components are allowed: the match is the Cartesian product inside the scope, still injective by default.

v1 edges and connections are required. No optional or negated edges.

## Formal match

Let *P* be the pattern and *H* the host graph restricted by `MatchScopeInput`.

A match is a mapping *φ* from pattern keys to host entities such that:

- Each pattern node maps to a `Node` whose attributes satisfy the pattern predicates.
- Mode 1: each pattern port maps to a `Port` of *φ(node)* that satisfies port predicates; each pattern edge maps to an `EDGE` from *φ(sourcePort)* to *φ(targetPort)*.
- Mode 2: each connection is witnessed by at least one port-edge path of length in `[minHops, maxHops]` (default 1) between *φ(sourceNode)* and *φ(targetNode)*, honoring optional port name/direction predicates on the endpoints.
- Predicates are conjunctive equalities (`typeId`, `kind`, `name`, `nickName`, `locked`, `language`, `origin`, `direction`, `access`, extension key/value).

### Injectivity (default on)

`options.injective` (default `true`): *φ* is injective on nodes and, in mode 1, on ports. Distinct pattern keys map to distinct host entities.

### Induced (default off)

`options.induced` (default `false`):

- Mode 1: no extra `EDGE` among the matched port set beyond the pattern edges.
- Mode 2: no extra node-to-node adjacency (via any port-EDGE-port) among the matched node set beyond the pattern connections.

## Scope

```graphql
input MatchScopeInput {
  versionId: ID
  documentId: ID
  origin: Origin
  includeNested: Boolean = false
}
```

`versionId` restricts instance data (`Node` / `Port` / `EDGE`) to that version. `documentId` restricts to documents with that id (optionally plus `versionId`). `includeNested` also walks `NESTS` from scoped documents (at most 8 hops). Origin-only scope does not apply `includeNested`.

Unscoped (corpus) search is allowed only when `first` is set (`LIMIT_REQUIRED` otherwise).

## Limits

| Limit | Default |
| --- | --- |
| Pattern nodes | 16 |
| Pattern ports | 32 |
| Pattern edges or connections | 32 |
| `maxHops` | 8 |
| Match timeout | 10s |
| `first` (match) | required if unscoped; max 100 |
| Match pages | keyset cursor on ordered `(VersionId, NodeId)` bindings; each edge has its own cursor |
| `totalCount` | computed only when selected |

Exceeding pattern-size limits is `PATTERN_TOO_LARGE`. Exceeding the 10s matcher budget is `MATCH_TIMEOUT`.

## Compilation

The reference matcher:

1. Validates unique keys (explicit or generated `edge_i` / `conn_i`) and referential integrity (`ports[].node`, `edges[].sourcePort` / `targetPort`, `connections[].sourceNode` / `targetNode`).
2. Orders pattern nodes by selectivity (`typeId`, then `kind`, then unconstrained).
3. Emits parameterized `MATCH` / `WHERE`. User strings and extension keys are never interpolated into Cypher identifiers (`n[$extKey] = $ext`).
4. Applies document scope through `CONTAINS` / `NESTS` (composite `documentId` + `versionId`), not `VersionId LIMIT 1`.
5. Binds a path for every connection, including hop ranges, and applies endpoint name/direction predicates to the first and last realizing ports.
6. Returns bindings: pattern `key` → host entity. Mode 2 also returns ordered realizing ports and edges so a client can highlight wires.

## Worked examples

### Mode 1 — slider output into an operator input

```graphql
query {
  matchSubgraph(
    pattern: {
      nodes: [
        { key: "slider", kind: PARAMETER }
        { key: "sphere", typeId: "…" }
      ]
      ports: [
        { key: "y", node: "slider", direction: OUT }
        { key: "r", node: "sphere", direction: IN, name: "Radius" }
      ]
      edges: [{ sourcePort: "y", targetPort: "r" }]
    }
    scope: { versionId: "…" }
    first: 10
  ) {
    nodes {
      nodes { key node { nodeId name } }
      ports { key port { portId name } }
    }
  }
}
```

### Mode 2 — same idea without naming ports

```graphql
query {
  matchSubgraph(
    pattern: {
      nodes: [
        { key: "slider", kind: PARAMETER }
        { key: "op", kind: OPERATOR }
      ]
      connections: [{
        sourceNode: "slider"
        targetNode: "op"
        sourceDirection: OUT
        targetDirection: IN
      }]
    }
    scope: { documentId: "…" }
    options: { injective: true, induced: false }
    first: 10
  ) {
    nodes {
      document { fileName versionId }
      nodes { key node { nodeId } }
      connections { key ports { portId } edges { sourcePortId targetPortId } }
    }
  }
}
```

Multi-hop “downstream of”:

```graphql
connections: [{ sourceNode: "a", targetNode: "b", minHops: 1, maxHops: 4 }]
```
