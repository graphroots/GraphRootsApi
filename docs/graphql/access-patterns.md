# Access patterns

GraphQL is a tree language; the store is a typed attributed digraph. Hierarchical field walks cover the explorer. First-class operations cover questions a selection set cannot express.

## Identity

Root fields for every stored type by its equality key. `entity(id)` refetches from the opaque Relay key.

```graphql
query {
  entity(id: "…") { ... on Document { documentId versionId } }
  document(documentId: "98f91b77-0811-4935-8a79-e601ad06b86a", versionId: "…") {
    fileName
    origin
  }
  node(versionId: "…", nodeId: "…") { name kind typeId }
  port(versionId: "…", portId: "…") { name direction }
  edge(versionId: "…", sourcePortId: "…", targetPortId: "…") { sourceName }
  nodeType(origin: GRASSHOPPER, typeId: "…") { name }
  library(origin: GRASSHOPPER, libraryId: "…") { name }
  libraryVersion(origin: GRASSHOPPER, libraryId: "…", version: "1.0", assemblyVersion: "unknown") { name }
}
```

## Collections, predicates, page

Cursors are opaque keysets on the equality key plus the requested sort field (tie-broken by that key). Default `first` is 50; maximum is 500. `totalCount` is computed only when that field is selected.

```graphql
query {
  nodes(
    filter: {
      versionId: "…"
      kind: OPERATOR
      typeId: "…"
      bbox: { minX: 0, minY: 0, maxX: 400, maxY: 200 }
      extensions: [{ key: "gh.componentGuid", equals: "…" }]
    }
    sort: { field: NAME, direction: ASC }
    first: 50
  ) {
    totalCount
    nodes { nodeId name x y }
    pageInfo { endCursor hasNextPage }
  }
}
```

Useful collection filters:

- Documents: `origin`, `isNested`, `committed`, `fileNameContains`, `libraryId`, date range
- Nodes: `versionId`, `documentId`, `kind`, `typeId`, `name` / `nickName`, `locked`, `language`, `hasSource`, canvas `bbox`, extensions
- Ports: `versionId`, `direction`, `access`, `name`
- Edges: `versionId`
- Catalog: `origin`, `nameContains`

## Relationship walk

Every model relationship is a field in both directions.

```graphql
query {
  document(documentId: "…", versionId: "…") {
    nodes(first: 20) { nodes { nodeId name } }
    nestedDocuments { documentId versionId isNested }
    parentDocuments { documentId }
    basedOn { documentId versionId }
    derivedDocuments { documentId versionId }
    historyAncestors(minHops: 1, maxHops: 8) { versionId }
    libraries { version library { name } }
  }
  node(versionId: "…", nodeId: "…") {
    document { fileName }
    ports { name direction }
    nodeType { name }
    groups { name }
    members { nodeId }
    clusterDocument { documentId versionId }
  }
  port(versionId: "…", portId: "…") {
    node { name }
    outgoingEdges { target { name } targetNode { name } }
    incomingEdges { source { name } sourceNode { name } }
  }
  nodeType(origin: GRASSHOPPER, typeId: "…") {
    instances(first: 20) { nodes { nodeId versionId } }
    definedBy { version name }
  }
}
```

`clusterDocument` resolves `gh.clusterVersionId` when present. Do not assume `direction == OUT` on synthetic floating-param ports (`IN` / `OUT` / `BOTH`).

`parentDocuments` is reverse `NESTS` (which graphs nest this cluster), not version history. Version lineage is `basedOn` / `derivedDocuments` / `historyAncestors` / `historyDescendants`. `CREATED_AT` document sort is `FileCreationTimeUtc` and is not commit order.

## Dataflow

Node-level neighbors collapse `HAS_PORT` + `EDGE`.

```graphql
query {
  node(versionId: "…", nodeId: "…") {
    outgoing { nodeId name }
    incoming { nodeId name }
    downstream(minHops: 1, maxHops: 3) { nodeId name }
    upstream(maxHops: 2) { nodeId name }
  }
  path(documentId: "…", versionId: "…", fromNodeId: "…", toNodeId: "…") {
    nodes { nodeId }
    ports { portId }
    edges { sourcePortId targetPortId }
  }
}
```

## Structure and catalog

```graphql
query {
  document(documentId: "…", versionId: "…") {
    isolatedNodes { nodeId kind }
    nodes(filter: { kind: ANNOTATION }) { nodes { text } }
    nodes(filter: { hasSource: true }) { nodes { language source } }
  }
  documents(filter: { libraryId: "…" }) { nodes { fileName } }
  nodes(filter: { typeId: "…" }) { totalCount nodes { versionId nodeId } }
}
```

## Dense document payload

Explorer / canvas draw. Loaded in one store round-trip. Not paginated.

```graphql
query {
  document(documentId: "…", versionId: "…") {
    graph {
      nodes { nodeId name kind typeId x y }
      ports { portId nodeId name direction }
      edges { sourcePortId targetPortId sourceName targetName }
    }
    stats { nodeCount edgeCount countsByKind { key count } countsByTypeId { key count } }
  }
}
```

## Version comparison

Two imports of one file, or a working copy and the snapshot it was forked from. Nodes match by `nodeId`, ports by `portId`, edges by their port pair; both documents are loaded densely, so each is subject to the dense payload limit. Changes are structured, not text: `kind` plus `before` / `after` values. A port present in both versions that was renamed or changed direction is reported on its node as `PORT_NAME` / `PORT_DIRECTION` with the port id in `key`; ports added or removed show as one `PORTS` change. Selecting a component in Grasshopper and saving is not a change. Comparing a version with itself returns no changes without loading the graph. Tool-specific state arrives as an `EXTENSION` change with its key (Grasshopper imports carry `gh.stateHash`, see [graph-model.md](../graph-model.md#extension-properties)); clients decide how to label it.

```graphql
query {
  compareDocuments(
    before: { documentId: "…", versionId: "…" }
    after: { documentId: "…", versionId: "…" }
    positionTolerance: 0.5
  ) {
    hasChanges
    addedNodes { id }
    removedNodes { id }
    changedNodes { after { id nodeId } changes { kind key before after } }
    addedEdges { id }
    removedEdges { id }
  }
}
```

Select only `id` to keep the response small and refetch details later with `entity(id:)`. A removed node's `id` refers to the `before` version, so it still resolves.

## Subgraph occurrence

See [subgraph-matching.md](subgraph-matching.md). Mode 1 (ports + edges) or mode 2 (connections), not both.

```graphql
query {
  matchSubgraph(
    pattern: {
      nodes: [
        { key: "slider", kind: PARAMETER }
        { key: "op", kind: OPERATOR }
      ]
      connections: [{ sourceNode: "slider", targetNode: "op" }]
    }
    scope: { versionId: "…" }
    first: 20
  ) {
    nodes {
      document { fileName }
      nodes { key node { nodeId name } }
      connections { key edges { sourcePortId targetPortId } }
    }
  }
}
```
