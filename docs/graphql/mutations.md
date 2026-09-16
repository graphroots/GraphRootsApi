# Mutations

Writes follow **fork-then-edit**, then optional **commit** / **merge**. Imported documents (`origin` grasshopper / dynamo) and GraphRoots documents with `committed: true` are immutable snapshots. GraphQL never patches them in place.

`importSnapshot` is the MERGE ingestion path (CLI GHX/GH client, later a Dynamo loader). `applyGraphPatch` is the editor/agent path on GRAPHROOTS working copies.

Working copies (`origin: GRAPHROOTS`, `committed: false`) stay mutable. They have at most one outgoing `BASED_ON` edge (HEAD). `CREATED_AT` sort is `FileCreationTimeUtc` and is not history order — walk `basedOn` / `historyAncestors`.

## forkDocument

Copies one document’s `CONTAINS` subgraph onto a new working document:

- New `documentId` (optional client value; otherwise a new GUID) and new `versionId` (new GUID; working copies are not content-addressed).
- `origin` is `GRAPHROOTS`, `committed` is `false` (even when forking a commit).
- Copied: `Node`, `Port`, `HAS_PORT`, `EDGE`, `MEMBER_OF`, `CONTAINS`. Copied nodes keep their source `origin` (a Grasshopper instance stays `GRASSHOPPER`).
- Linked, not copied: `NodeType`, `Library`, `LibraryVersion`, `HAS_INSTANCE`, `USED_BY`, `DEFINES`. Catalog identity therefore still resolves after fork.
- `NESTS` keeps pointing at the original nested documents. Those stay immutable; fork a nested document separately to edit a cluster.
- Lineage: if the source is a snapshot (imported or `committed`), `dest -[:BASED_ON]-> source`. If the source is a working copy, the dest copies the dirty graph but `BASED_ON` points at the source’s HEAD, not the mutable source.

```graphql
mutation {
  forkDocument(input: {
    documentId: "…"
    versionId: "…"
    newDocumentId: "my-working-copy"
  }) {
    document { documentId versionId origin committed basedOn { versionId } }
  }
}
```

## createDocument

Creates an empty graphroots working copy (`committed: false`, no `BASED_ON`).

```graphql
mutation {
  createDocument(input: { documentId: "blank", fileName: "untitled" }) {
    document { documentId versionId committed }
  }
}
```

## commitDocument

Copies the working copy onto a new snapshot (`same documentId`, new `versionId`, `committed: true`). The snapshot inherits the working copy’s HEAD as `BASED_ON` parents (none for the first commit). The working copy keeps its `versionId`, stays mutable, and HEAD is repointed at the new snapshot.

```graphql
mutation {
  commitDocument(input: { documentId: "…", versionId: "…" }) {
    document { versionId committed basedOn { versionId } }
    workingDocument { versionId committed basedOn { versionId } }
  }
}
```

## mergeDocument

Records a merge commit. There is no automatic merge of `CONTAINS` / `EDGE` topology — the working copy **is** the merge result (patch it first if you need combined graphs).

- Other parents must be snapshots, must not be the working copy, and must not already be ancestors.
- The new snapshot has `BASED_ON` to `distinct(HEAD ∪ otherParents)` (at least two).
- Working HEAD is repointed at the merge snapshot.

```graphql
mutation {
  mergeDocument(input: {
    documentId: "…"
    versionId: "…"
    otherParents: [{ documentId: "…", versionId: "…" }]
  }) {
    document { versionId committed basedOn { documentId versionId } }
    workingDocument { basedOn { versionId } }
  }
}
```

## importSnapshot

MERGEs a tool-neutral snapshot in one store transaction: documents, nodes, ports, edges, group memberships, nested documents (`NESTS`), and catalog (`Library` / `LibraryVersion` / `NodeType` plus `HAS_VERSION` / `DEFINES` / `USED_BY` / `HAS_INSTANCE`). `CONTAINS` and `HAS_PORT` are inferred from `documentId`+`versionId` (and `nodeId` on ports). `committed` is always stored `false`; immutability is `origin != graphroots`.

The payload is flat (no recursive GraphQL input types). Exactly one root document (`isNested` not true). Origin must be `GRASSHOPPER` or `DYNAMO` (`GRAPHROOTS` / `UNKNOWN` fail `INVALID_ARGUMENT`). Re-import of the same content-hash keys overwrites properties; it does not prune leftover entities from an older different snapshot.

Size cap matches dense-graph reads: 100 000 entities across nodes+ports+edges in the snapshot (including nested docs). Oversized snapshots fail `RESULT_TOO_LARGE` before write. Host execution timeout is 120s; `importSnapshot` is cost 0 so large input lists do not hit field-cost limits.

```graphql
mutation {
  importSnapshot(input: {
    documents: [{ documentId: "…", versionId: "…", origin: GRASSHOPPER, fileName: "model.ghx" }]
    nodes: [{ documentId: "…", versionId: "…", nodeId: "n1", origin: GRASSHOPPER, typeId: "…", kind: OPERATOR }]
    ports: [{ documentId: "…", versionId: "…", nodeId: "n1", portId: "p1", direction: IN }]
    edges: [{ documentId: "…", versionId: "…", sourcePortId: "…", targetPortId: "p1" }]
  }) {
    document { documentId versionId origin committed }
    nestedDocumentCount
    nodeCount
    portCount
    edgeCount
  }
}
```

CLI: parse with `GrasshopperLib`, POST this mutation (`--graphql-url` / `GRAPHQL_URL`, default `http://127.0.0.1:5088/graphql`). `--purge --yes` is still a direct Neo4j path, not GraphQL.

## applyGraphPatch

One store transaction (`ExecuteWrite`) applying creates, updates, and deletes. This is the editor/agent batch path. Fine-grained UI actions (wire, move, set extensions) are the same patch with a single operation group.

```graphql
mutation {
  applyGraphPatch(input: {
    documentId: "…"
    versionId: "…"
    createNodes: [{ nodeId: "n1", name: "Add", kind: OPERATOR, typeId: "…" }]
    createPorts: [{ portId: "p1", nodeId: "n1", name: "A", direction: IN }]
    createEdges: [{ sourcePortId: "…", targetPortId: "p1" }]
    updateNodes: [{ nodeId: "n2", x: 120, y: 40 }]
    deleteNodes: ["obsolete"]
    addToGroups: [{ memberNodeId: "n1", groupNodeId: "g1" }]
  }) {
    document { versionId }
    createdNodeIds
    deletedNodeIds
  }
}
```

Rules:

- The target document must exist (`documentId` + `versionId`) and be a writable working copy (`origin == GRAPHROOTS` and `committed == false`; `IMMUTABLE_DOCUMENT` otherwise).
- Duplicate or overlapping operations, empty ids, self-group membership, self-loop edges, reserved extension keys, and oversized patches fail with `INVALID_ARGUMENT` before any write.
- `addToGroups` requires the target `groupNodeId` to be a `GROUP` node (`INVALID_ARGUMENT` otherwise).
- `create*` fails with `CONFLICT` if the key exists. Constraint races also map to `CONFLICT`.
- `update*` / `delete*` fail with `NOT_FOUND` if the key is missing. A nil `versionId` is treated as a missing document key (`NOT_FOUND`), not as an omitted argument.
- Creating a node also `CONTAINS` it in the document for `versionId`. Optional `origin` defaults to `GRAPHROOTS`. Changing a node’s display name does not rename the shared `NodeType`.
- Creating a port also `HAS_PORT` from the given node. Node and port must share `versionId`.
- Creating an edge requires both ports in the same version. `sourcePortId` and `targetPortId` must differ.
- Deleting a node cascades with `DETACH DELETE`: its ports and incident `EDGE`s are deleted.
- Deleting a port deletes incident `EDGE`s.
- Nullable updates use GraphQL `null` to clear a field; omitted fields are left unchanged.
- Changing `typeId` rewires `HAS_INSTANCE` to the new catalog type.
- `setExtensions` replaces the listed keys on one entity (omit a key to leave it; pass JSON `null` to remove). Document extension `id` must match the patch `documentId`.

## Fine-grained wrappers

`wirePorts`, `unwirePorts`, `moveNode`, and `setExtensions` are single-operation patches on the same transaction path.

```graphql
mutation {
  wirePorts(input: {
    documentId: "…"
    versionId: "…"
    sourcePortId: "…"
    targetPortId: "…"
  }) { document { versionId } }
}
```

## Error codes

See [README.md](README.md). Mutation-specific: `IMMUTABLE_DOCUMENT`, `NOT_FOUND`, `CONFLICT`. `importSnapshot` also uses `INVALID_ARGUMENT` (origin, missing root, broken refs) and `RESULT_TOO_LARGE`.

v1 does not expose `purgeDatabase`.
