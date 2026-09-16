# Computational graph model

GraphRoots stores computational graphs in Neo4j using **tool-neutral labels**. The store (`GraphDbInterfaces` / `GraphDb`) has no Grasshopper dependency. Grasshopper is a first-class importer (`GrasshopperInterfaces` / `GrasshopperLib`): the GHX parser stays faithful to GH, and the snapshot builder maps core nodes plus `gh.*` extension properties. Persistence is GraphQL `importSnapshot` (MERGE). Dynamo (and later solvers) use the same core and their own prefix (`dyn.*`).

Schema version on every persisted type is **2**. This is a breaking change; re-import with `--purge --yes` or a new database. There is no migrator. `--purge` wipes all nodes and requires `--yes`; the CLI prints the target URI before running.

## Core topology

```
Library -[:HAS_VERSION]-> LibraryVersion -[:DEFINES]-> NodeType
LibraryVersion -[:USED_BY]-> Document
NodeType -[:HAS_INSTANCE]-> Node
Document -[:CONTAINS]-> Node -[:HAS_PORT]-> Port
Port -[:EDGE]-> Port
Document -[:NESTS]-> Document
Document -[:BASED_ON]-> Document   (version parent; child → older snapshot)
Node -[:MEMBER_OF]-> Node   (member to group)
```

A solver walks `Document → Node → Port → EDGE → Port → Node`. Nested graphs (clusters, custom nodes, compound ops) are `NESTS`. Version history is `BASED_ON` (git-like: newer document points at parent snapshot(s)). Do not confuse `BASED_ON` with `NESTS` / GraphQL `parentDocuments`.

| Type | Label | Equality | Role |
| --- | --- | --- | --- |
| `Document` | `Document` | `DocumentId` (string) + `VersionId` (Guid) | GH document / cluster, Dynamo workspace, solver graph |
| `Node` | `Node` | `VersionId` + `NodeId` (string) | Operator, parameter, group, annotation, cluster reference |
| `Port` | `Port` | `VersionId` + `PortId` (string) | Input or output; floating GH params use a synthetic port |
| `Edge` | `EDGE` | `VersionId` + `SourcePortId` + `TargetPortId` | Wire / connector |
| `NodeType` | `NodeType` | `Origin` + `TypeId` | GH component GUID, Dynamo creationName |
| `Library` | `Library` | `Origin` + `LibraryId` | GH plugin, Dynamo package |
| `LibraryVersion` | `LibraryVersion` | library key + `Version` + `AssemblyVersion` | Reproducible dependency |

`Origin` is `grasshopper`, `dynamo`, or `graphroots`. Identities that come from a source tool are **strings**, not .NET `Guid`s.

Shared node fields: `Kind` (`Operator`, `Parameter`, `Group`, `Annotation`, `Cluster`), `TypeId` (catalog key, same as `NodeType.TypeId`; `HAS_INSTANCE` is still required), `Name`, `NickName`, `Locked`, layout `X`/`Y`, optional `Source` + `Language` (scripts / code blocks), `Text` (annotations). Ports have `Direction` (`In` / `Out` / `Both`) and optional `Access` (`item`, `list`, `tree`, or a lacing string).

Floating GH parameters use one synthetic `Port` whose `PortId` equals the node instance GUID. `Direction` is set from EDGE incidence (`In` if only a target, `Out` if only a source, `Both` if both). Groups and annotations get no port unless they have container-level sources. Declared `param_input` / `param_output` ports stay `In` / `Out`.

## Extension properties

Tool-specific data is a dictionary on the C# type (`Extensions`), flattened to Neo4j properties on write. Keys **must** be namespaced (`gh.componentGuid`, later `dyn.…`) and must not collide with a core property name (including `gh.Name`). Cypher can still filter on these keys.

Grasshopper keys (see `GhExtensionKeys`): `gh.componentGuid`, `gh.scriptSource`, `gh.usingSource`, `gh.additionalSource`, `gh.scriptText`, `gh.clusterParamMap`, `gh.clusterVersionId`, `gh.clusterSourcePortId`, `gh.clusterTargetPortId`, `gh.assemblyName`.

## Mapping table

| Grasshopper | GraphRoots core | Dynamo (planned) |
| --- | --- | --- |
| Document / cluster document | `Document` (`Origin=grasshopper`, `IsNested` for clusters) | Workspace / custom-node workspace |
| Object / container | `Node` (`NodeId` = instance GUID) | Node (`NodeId` = Dynamo id) |
| `param_input` / `param_output` | `Port` (`PortId` = param instance GUID) | In/out port (index + name) |
| Floating parameter | `Node` `Kind=Parameter` + synthetic `Port` (`PortId` = instance GUID, `Direction` from EDGE incidence) | Standalone input/output node + ports |
| Wire | `EDGE` between ports | Connector |
| Cluster | Nested `Document` + `NESTS`; node `Kind=Cluster`; ParamMap → `gh.clusterParamMap` | Custom node + nested workspace |
| Group | `Node` `Kind=Group` + `MEMBER_OF` (member → group) | Group / note group |
| Scribble | `Node` `Kind=Annotation` + `Text` | Annotation / note |
| Component GUID + name | `Node.TypeId` + `NodeType` (`TypeId` = component GUID) + `HAS_INSTANCE` | `creationName` / concrete type → `Node.TypeId` + `NodeType` |
| GHA library | `Library` / `LibraryVersion` (`LibraryId` = plugin GUID string) | Package + package version |
| Script component | `Source` + `Language`; legacy split in `gh.scriptSource` / `Using` / `Additional` | Code block / Python node (`Source` + `Language`) |
| Pivot | `Node.X` / `Node.Y` | Node canvas position |
| File path / timestamps | `Document` source-artifact fields | DYN path / timestamps |

Dynamo import and GHX/DYN export are **not** implemented. The Dynamo column is the contract for a later loader.

## Solver assumptions

A future local or cloud solver may assume:

- Topology is `Document` ⊃ `Node` ⊃ `Port` connected by directed `EDGE`.
- Dispatch key is `Node.TypeId` / `NodeType` (`Origin` + `TypeId`) plus `LibraryVersion`.
- Nested graphs are `NESTS`.
- Disabled/locked is `Node.Locked`.
- Walk EDGE for connectivity. Do not filter floating params by `Direction == Out`; synthetic ports may be `In`, `Out`, or `Both`.
- Values, dirty bits, and runtime artifacts are **later** labels, not this schema. For what GH archives actually contain that the importer drops, see [Grasshopper data ignored by the importer](importer-ignored-gh-data.md).

`schemaVersion` is a stamp written on every SET. Bumping it does not run a migration; the next MERGE overwrites the stamp.

## Content-addressed identity (UUID5)

`Document.VersionId` is a RFC 4122 UUID version 5. Namespaces are fixed; do not change them without treating the result as a new schema:

| Use | Namespace | Name bytes | Where |
| --- | --- | --- | --- |
| File / source artifact `VersionId` | `65BA840B-4FC0-4EC0-86D4-CF9226B23895` | File contents | `LoaderContext.FromFile` |
| Nested cluster document `VersionId` | `E375A37E-75CD-4384-9506-BA3FF670A199` | Cluster `gh_bytearray` Base64 | `GhxArchive` |

Working copies created through GraphQL (`forkDocument`, `createDocument`) use a new random `versionId`; they are not content-addressed. `commitDocument` / `mergeDocument` copy a working copy onto a new `versionId` with `Committed=true` and record `BASED_ON` parents. Working copies stay mutable (`origin=graphroots` and `Committed=false`) and have at most one outgoing `BASED_ON` (HEAD). Snapshots are imported documents (`origin` grasshopper / dynamo) or `Committed` GraphRoots nodes; they are never patched.

## Schema constraints and indexes

`IDbOperations.EnsureSchema` (called once by GraphApi startup and live tests, not by the import CLI) is idempotent:

1. Drops leftover **unscoped** range index names from before indexes were label-scoped (`node_range_index_VersionId`, `node_range_index_DocumentId_VersionId`, and the other `node_range_index_{properties}` names). Current indexes are `node_range_index_{label}_{properties}`.
2. Creates uniqueness constraints:
   - `Document` `(DocumentId, VersionId)` — `NODE KEY` on Enterprise; `UNIQUE` on Community
   - `Node` `(VersionId, NodeId)` — `NODE KEY` on Enterprise; `UNIQUE` on Community
   - `Port` `(VersionId, PortId)` — `NODE KEY` on Enterprise; `UNIQUE` on Community
   - `NodeType` `(Origin, TypeId)` — `NODE KEY` on Enterprise; `UNIQUE` on Community
   - `Library` `(Origin, LibraryId)` — `NODE KEY` on Enterprise; `UNIQUE` on Community
   - `LibraryVersion` `(Origin, LibraryId, Version, AssemblyVersion)` — unique only (`AssemblyVersion` is nullable)
   - `EDGE` `(VersionId, SourcePortId, TargetPortId)` — relationship uniqueness (`UNIQUE` on both editions)

   `EnsureSchema` tries `IS NODE KEY` first and falls back to `IS UNIQUE` when Neo4j reports that node keys are Enterprise-only. Constraint names differ (`node_key_*` vs `node_unique_*`). Relationship uniqueness for `EDGE` is always `UNIQUE`.
3. Keeps the existing **label-scoped** range indexes on each equality property (and the composite).

Empty relationship types (`CONTAINS`, `HAS_PORT`, `HAS_INSTANCE`, `NESTS`, `BASED_ON`, `MEMBER_OF`, `HAS_VERSION`, `DEFINES`, `USED_BY`) stay MERGE-on-type; they have no uniqueness constraint.
