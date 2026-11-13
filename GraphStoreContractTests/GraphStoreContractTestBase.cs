using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphStoreContractTests;

/// <summary>
/// Behaviour every <see cref="IGraphStore"/> backend must show. A backend's test project derives a
/// <c>[TestClass]</c>, creates the store in <c>[ClassInitialize]</c> (calling <see cref="IGraphStoreAdmin.Initialize"/>),
/// and returns it from <see cref="Store"/> / <see cref="Admin"/>. Data is seeded only through the public
/// store contract (mostly <see cref="IGraphStore.ImportSnapshot"/>), never through backend internals.
/// </summary>
public abstract class GraphStoreContractTestBase
{
    protected abstract IGraphStore Store { get; }

    protected abstract IGraphStoreAdmin Admin { get; }

    [TestInitialize]
    public Task PurgeBeforeEachTest() => Admin.Purge();

    protected static ImportDocument GhDocument(string documentId, Guid versionId, bool? isNested = null, DateTime? createdUtc = null) => new()
    {
        DocumentId = documentId,
        VersionId = versionId,
        Origin = GraphOrigins.Grasshopper,
        FileName = documentId,
        IsNested = isNested,
        FileCreationTimeUtc = createdUtc,
    };

    protected static ImportNode GhNode(string documentId, Guid versionId, string nodeId, string? kind, string typeId = "gh-type", string? name = null, float? x = null) => new()
    {
        DocumentId = documentId,
        VersionId = versionId,
        NodeId = nodeId,
        Origin = GraphOrigins.Grasshopper,
        TypeId = typeId,
        Kind = kind,
        Name = name,
        X = x,
    };

    /// <summary>Import Grasshopper documents and nodes, adding a catalog <see cref="ImportNodeType"/> per distinct type id.</summary>
    protected Task<ImportSnapshotResult> ImportGrasshopper(
        IReadOnlyList<ImportDocument> documents,
        IReadOnlyList<ImportNode>? nodes = null,
        IReadOnlyList<ImportNest>? nests = null)
    {
        nodes ??= [];
        return Store.ImportSnapshot(new ImportSnapshot
        {
            Documents = documents,
            Nodes = nodes,
            Nests = nests ?? [],
            NodeTypes = nodes
                .Select(n => n.TypeId!)
                .Distinct(StringComparer.Ordinal)
                .Select(t => new ImportNodeType { Origin = GraphOrigins.Grasshopper, TypeId = t, Name = t })
                .ToList(),
        });
    }

    [TestMethod]
    public async Task CreatePatchForkAndMatch()
    {
        var created = await Store.CreateDocument("doc-a", "blank");
        Assert.AreEqual(GraphOrigins.GraphRoots, created.Origin);

        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes =
            [
                new CreateNodeOp { NodeId = "slider", Kind = NodeKinds.Parameter, Name = "Slider" },
                new CreateNodeOp { NodeId = "op", Kind = NodeKinds.Operator, TypeId = "add", Name = "Add" },
            ],
            CreatePorts =
            [
                new CreatePortOp { PortId = "out", NodeId = "slider", Name = "Y", Direction = PortDirections.Out },
                new CreatePortOp { PortId = "in", NodeId = "op", Name = "A", Direction = PortDirections.In },
            ],
            CreateEdges =
            [
                new CreateEdgeOp { SourcePortId = "out", TargetPortId = "in" },
            ],
        });

        var graph = await Store.GetDocumentGraph(created.DocumentId, created.VersionId);
        Assert.AreEqual(2, graph.Nodes.Count);
        Assert.AreEqual(2, graph.Ports.Count);
        Assert.AreEqual(1, graph.Edges.Count);

        var matches = await Store.MatchSubgraph(
            new SubgraphPattern
            {
                Nodes =
                [
                    new PatternNode { Key = "a", Kind = NodeKinds.Parameter },
                    new PatternNode { Key = "b", Kind = NodeKinds.Operator },
                ],
                Connections = [new PatternConnection { SourceNode = "a", TargetNode = "b" }],
            },
            new MatchScope { DocumentId = created.DocumentId, VersionId = created.VersionId },
            new MatchOptions(),
            new PageRequest { First = 10, IncludeTotalCount = true });
        Assert.AreEqual(1, matches.Items.Count);
        Assert.AreEqual("slider", matches.Items[0].Nodes.Single(n => n.Key == "a").Node.NodeId);
        Assert.AreEqual(1, matches.Items[0].Connections.Count);
        Assert.IsTrue(matches.Items[0].Connections[0].Ports.Count >= 2);
        Assert.AreEqual(1, matches.Items[0].Connections[0].Edges.Count);

        var importedVersion = Guid.NewGuid();
        var imported = (await ImportGrasshopper(
            [GhDocument("imported", importedVersion)],
            [GhNode("imported", importedVersion, "gh-node", NodeKinds.Operator, name: "Sphere")])).Document;

        var fork = await Store.ForkDocument(imported.DocumentId, imported.VersionId, "working");
        Assert.AreEqual(GraphOrigins.GraphRoots, fork.Origin);
        Assert.IsFalse(fork.Committed);
        var forkParents = await Store.GetBasedOn(fork.DocumentId, fork.VersionId);
        Assert.AreEqual(1, forkParents.Count);
        Assert.AreEqual(imported.DocumentId, forkParents[0].DocumentId);
        Assert.AreEqual(imported.VersionId, forkParents[0].VersionId);
        var forkedNode = await Store.GetNode(fork.VersionId, "gh-node");
        Assert.IsNotNull(forkedNode);
        Assert.AreEqual(GraphOrigins.Grasshopper, forkedNode.Origin);
        var types = await Store.GetNodeTypesForNodes([(fork.VersionId, "gh-node")]);
        Assert.AreEqual(GraphOrigins.Grasshopper, types[(fork.VersionId, "gh-node")]?.Origin);
        Assert.AreEqual("gh-type", types[(fork.VersionId, "gh-node")]?.TypeId);

        var immutable = await Assert.ThrowsExactlyAsync<GraphStoreException>(() =>
            Store.ApplyPatch(new GraphPatch
            {
                DocumentId = imported.DocumentId,
                VersionId = imported.VersionId,
                DeleteNodes = ["x"],
            }));
        Assert.AreEqual(GraphStoreException.Codes.ImmutableDocument, immutable.Code);
    }

    [TestMethod]
    public async Task CascadeDeleteRemovesPortsAndEdges()
    {
        var created = await Store.CreateDocument("doc-del", "blank");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes =
            [
                new CreateNodeOp { NodeId = "a", Kind = NodeKinds.Parameter },
                new CreateNodeOp { NodeId = "b", Kind = NodeKinds.Operator },
            ],
            CreatePorts =
            [
                new CreatePortOp { PortId = "out", NodeId = "a", Direction = PortDirections.Out },
                new CreatePortOp { PortId = "in", NodeId = "b", Direction = PortDirections.In },
            ],
            CreateEdges = [new CreateEdgeOp { SourcePortId = "out", TargetPortId = "in" }],
        });

        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            DeleteNodes = ["a"],
        });

        var graph = await Store.GetDocumentGraph(created.DocumentId, created.VersionId);
        Assert.AreEqual(1, graph.Nodes.Count);
        Assert.AreEqual("b", graph.Nodes[0].NodeId);
        Assert.AreEqual(1, graph.Ports.Count);
        Assert.AreEqual(0, graph.Edges.Count);
        Assert.IsNull(await Store.GetNode(created.VersionId, "a"));
        Assert.IsNull(await Store.GetPort(created.VersionId, "out"));
    }

    [TestMethod]
    public async Task PatchRequiresDocumentIdAndClearsNullableFields()
    {
        var created = await Store.CreateDocument("doc-opt", "blank");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes = [new CreateNodeOp { NodeId = "n1", Name = "Keep", TypeId = "t1", Text = "hello" }],
        });

        var missing = await Assert.ThrowsExactlyAsync<GraphStoreException>(() =>
            Store.ApplyPatch(new GraphPatch { VersionId = created.VersionId, DeleteNodes = ["n1"] }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, missing.Code);

        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            UpdateNodes = [new UpdateNodeOp { NodeId = "n1", Text = new OptionalSet<string?>(null), TypeId = new OptionalSet<string?>(null) }],
        });
        var node = await Store.GetNode(created.VersionId, "n1");
        Assert.IsNotNull(node);
        Assert.IsNull(node.Text);
        Assert.IsNull(node.TypeId);
        Assert.AreEqual("Keep", node.Name);
    }

    [TestMethod]
    public async Task MatchPagesAreDeterministic()
    {
        var created = await Store.CreateDocument("doc-page", "blank");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes =
            [
                new CreateNodeOp { NodeId = "n1", Kind = NodeKinds.Parameter },
                new CreateNodeOp { NodeId = "n2", Kind = NodeKinds.Parameter },
            ],
        });

        var page1 = await Store.MatchSubgraph(
            new SubgraphPattern { Nodes = [new PatternNode { Key = "p", Kind = NodeKinds.Parameter }] },
            new MatchScope { VersionId = created.VersionId, DocumentId = created.DocumentId },
            new MatchOptions(),
            new PageRequest { First = 1, IncludeTotalCount = true });
        Assert.AreEqual(1, page1.Items.Count);
        Assert.IsTrue(page1.HasNextPage);
        Assert.AreEqual(2, page1.TotalCount);
        Assert.IsFalse(string.IsNullOrEmpty(page1.Cursors[0]));

        var page2 = await Store.MatchSubgraph(
            new SubgraphPattern { Nodes = [new PatternNode { Key = "p", Kind = NodeKinds.Parameter }] },
            new MatchScope { VersionId = created.VersionId, DocumentId = created.DocumentId },
            new MatchOptions(),
            new PageRequest { First = 1, After = page1.EndCursor, IncludeTotalCount = false });
        Assert.AreEqual(1, page2.Items.Count);
        Assert.AreNotEqual(page1.Items[0].Nodes[0].Node.NodeId, page2.Items[0].Nodes[0].Node.NodeId);
        Assert.AreEqual(0, page2.TotalCount);
    }

    [TestMethod]
    public async Task ListDocuments_PagesPastNullCreatedAt()
    {
        await ImportGrasshopper([GhDocument("null-a", Guid.Parse("11111111-1111-1111-1111-111111111111"))]);
        await ImportGrasshopper([GhDocument("null-b", Guid.Parse("22222222-2222-2222-2222-222222222222"))]);
        await ImportGrasshopper([GhDocument(
            "dated",
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            createdUtc: new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc))]);

        var sort = new SortSpec { Field = "createdAt", Direction = StoreSortDirection.Desc };
        var page1 = await Store.ListDocuments(null, new PageRequest { First = 2, Sort = sort, IncludeTotalCount = true });
        Assert.AreEqual(3, page1.TotalCount);
        Assert.AreEqual(2, page1.Items.Count);
        Assert.IsTrue(page1.HasNextPage);
        Assert.IsTrue(page1.Items.All(d => d.FileCreationTimeUtc == null));

        var page2 = await Store.ListDocuments(null, new PageRequest { First = 2, After = page1.EndCursor, Sort = sort, IncludeTotalCount = false });
        Assert.AreEqual(1, page2.Items.Count);
        Assert.AreEqual("dated", page2.Items[0].FileName);
        Assert.IsNotNull(page2.Items[0].FileCreationTimeUtc);
        Assert.IsFalse(page2.HasNextPage);
    }

    [TestMethod]
    public async Task ListNodes_PagesPastNullX()
    {
        var version = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await ImportGrasshopper(
            [GhDocument("null-x", version)],
            [
                GhNode("null-x", version, "g1", NodeKinds.Group, "group", "Group"),
                GhNode("null-x", version, "g2", NodeKinds.Group, "group", "Group"),
                GhNode("null-x", version, "p1", NodeKinds.Parameter, "point", "Point", x: 10f),
                GhNode("null-x", version, "p2", NodeKinds.Parameter, "point", "Point", x: 20f),
            ]);

        var sort = new SortSpec { Field = "x", Direction = StoreSortDirection.Desc };
        var filter = new NodeFilter { VersionId = version };
        var page1 = await Store.ListNodes(filter, new PageRequest { First = 2, Sort = sort, IncludeTotalCount = true });
        Assert.AreEqual(4, page1.TotalCount);
        Assert.IsTrue(page1.HasNextPage);
        Assert.IsTrue(page1.Items.All(n => n.X == null));

        var page2 = await Store.ListNodes(filter, new PageRequest { First = 2, After = page1.EndCursor, Sort = sort, IncludeTotalCount = false });
        Assert.AreEqual(2, page2.Items.Count);
        CollectionAssert.AreEqual(new float?[] { 20f, 10f }, page2.Items.Select(n => n.X).ToArray());
        Assert.IsFalse(page2.HasNextPage);
    }

    [TestMethod]
    public async Task ListDocuments_FiltersByIsNested()
    {
        var root = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000");
        var cluster = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        await ImportGrasshopper(
            [GhDocument("top", root, isNested: false), GhDocument("nested", cluster, isNested: true)],
            nests: [new ImportNest { ParentDocumentId = "top", ParentVersionId = root, ChildDocumentId = "nested", ChildVersionId = cluster }]);
        await ImportGrasshopper([GhDocument("unset", Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"))]);

        var nested = await Store.ListDocuments(new DocumentFilter { IsNested = true }, new PageRequest { First = 10, IncludeTotalCount = true });
        Assert.AreEqual(1, nested.TotalCount);
        Assert.AreEqual("nested", nested.Items[0].DocumentId);

        var top = await Store.ListDocuments(new DocumentFilter { IsNested = false }, new PageRequest { First = 10, IncludeTotalCount = true });
        Assert.AreEqual(2, top.TotalCount);
        CollectionAssert.AreEquivalent(new[] { "unset", "top" }, top.Items.Select(d => d.DocumentId).ToArray());
    }

    [TestMethod]
    public async Task GetIsolatedNodes_ExcludesGroupsAndAnnotations()
    {
        var created = await Store.CreateDocument("iso", "iso");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes =
            [
                new CreateNodeOp { NodeId = "slider", Kind = NodeKinds.Parameter, Name = "Slider" },
                new CreateNodeOp { NodeId = "op", Kind = NodeKinds.Operator, Name = "Add" },
                new CreateNodeOp { NodeId = "g", Kind = NodeKinds.Group, Name = "Group" },
                new CreateNodeOp { NodeId = "note", Kind = NodeKinds.Annotation, Name = "Note" },
                new CreateNodeOp { NodeId = "orphan", Kind = NodeKinds.Parameter, Name = "Orphan" },
            ],
            CreatePorts =
            [
                new CreatePortOp { PortId = "out", NodeId = "slider", Name = "Y", Direction = PortDirections.Out },
                new CreatePortOp { PortId = "in", NodeId = "op", Name = "A", Direction = PortDirections.In },
            ],
            CreateEdges =
            [
                new CreateEdgeOp { SourcePortId = "out", TargetPortId = "in" },
            ],
        });

        var isolated = await Store.GetIsolatedNodes(created.DocumentId, created.VersionId);
        CollectionAssert.AreEqual(new[] { "orphan" }, isolated.Select(n => n.NodeId).ToArray());
    }

    [TestMethod]
    public async Task MatchSubgraph_TwoUnconstrainedNodes_DefaultInjective()
    {
        var created = await Store.CreateDocument("match-two", "match-two");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes =
            [
                new CreateNodeOp { NodeId = "n1", Kind = NodeKinds.Parameter },
                new CreateNodeOp { NodeId = "n2", Kind = NodeKinds.Operator },
            ],
        });

        var matches = await Store.MatchSubgraph(
            new SubgraphPattern
            {
                Nodes = [new PatternNode { Key = "a" }, new PatternNode { Key = "b" }],
            },
            new MatchScope { DocumentId = created.DocumentId, VersionId = created.VersionId },
            new MatchOptions(),
            new PageRequest { First = 10, IncludeTotalCount = true });
        Assert.AreEqual(2, matches.TotalCount);
        Assert.AreEqual(2, matches.Items.Count);
        Assert.AreNotEqual(matches.Items[0].Nodes[0].Node.NodeId, matches.Items[0].Nodes[1].Node.NodeId);
    }

    [TestMethod]
    public async Task MatchSubgraph_ConnectionDirections_DefaultInjective()
    {
        var created = await Store.CreateDocument("match-dir", "match-dir");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes =
            [
                new CreateNodeOp { NodeId = "slider", Kind = NodeKinds.Parameter },
                new CreateNodeOp { NodeId = "op", Kind = NodeKinds.Operator },
            ],
            CreatePorts =
            [
                new CreatePortOp { PortId = "out", NodeId = "slider", Name = "Y", Direction = PortDirections.Out },
                new CreatePortOp { PortId = "in", NodeId = "op", Name = "A", Direction = PortDirections.In },
            ],
            CreateEdges = [new CreateEdgeOp { SourcePortId = "out", TargetPortId = "in" }],
        });

        var matches = await Store.MatchSubgraph(
            new SubgraphPattern
            {
                Nodes =
                [
                    new PatternNode { Key = "src", Kind = NodeKinds.Parameter },
                    new PatternNode { Key = "dst", Kind = NodeKinds.Operator },
                ],
                Connections =
                [
                    new PatternConnection
                    {
                        SourceNode = "src",
                        TargetNode = "dst",
                        SourceDirection = PortDirections.Out,
                        TargetDirection = PortDirections.In,
                    },
                ],
            },
            new MatchScope { DocumentId = created.DocumentId, VersionId = created.VersionId },
            new MatchOptions(),
            new PageRequest { First = 10, IncludeTotalCount = true });
        Assert.AreEqual(1, matches.TotalCount);
        Assert.AreEqual("slider", matches.Items[0].Nodes.Single(n => n.Key == "src").Node.NodeId);
    }

    [TestMethod]
    public async Task MatchSubgraph_IncludeNested_ParentVersusChild()
    {
        var parent = GhDocument("nest-parent", Guid.NewGuid());
        var child = GhDocument("nest-child", Guid.NewGuid(), isNested: true);
        await ImportGrasshopper(
            [parent, child],
            [
                GhNode(parent.DocumentId, parent.VersionId, "parent-n", NodeKinds.Operator),
                GhNode(child.DocumentId, child.VersionId, "child-n", NodeKinds.Operator),
            ],
            [new ImportNest { ParentDocumentId = parent.DocumentId, ParentVersionId = parent.VersionId, ChildDocumentId = child.DocumentId, ChildVersionId = child.VersionId }]);

        var pattern = new SubgraphPattern { Nodes = [new PatternNode { Key = "n", Kind = NodeKinds.Operator }] };
        var scoped = new MatchScope { DocumentId = parent.DocumentId, VersionId = parent.VersionId };
        var page = new PageRequest { First = 10, IncludeTotalCount = true };

        var parentOnly = await Store.MatchSubgraph(pattern, scoped, new MatchOptions { Injective = false }, page);
        Assert.AreEqual(1, parentOnly.TotalCount);
        Assert.AreEqual("parent-n", parentOnly.Items[0].Nodes[0].Node.NodeId);

        var withNested = await Store.MatchSubgraph(
            pattern,
            new MatchScope { DocumentId = parent.DocumentId, VersionId = parent.VersionId, IncludeNested = true },
            new MatchOptions { Injective = false },
            page);
        Assert.AreEqual(2, withNested.TotalCount);
        CollectionAssert.AreEquivalent(
            new[] { "parent-n", "child-n" },
            withNested.Items.Select(m => m.Nodes[0].Node.NodeId).ToArray());
    }

    [TestMethod]
    public async Task MatchSubgraph_InducedAndInjective_RejectsExtraEdge()
    {
        var created = await Store.CreateDocument("induced", "induced");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes =
            [
                new CreateNodeOp { NodeId = "a", Kind = NodeKinds.Parameter },
                new CreateNodeOp { NodeId = "b", Kind = NodeKinds.Operator },
            ],
            CreatePorts =
            [
                new CreatePortOp { PortId = "out", NodeId = "a", Direction = PortDirections.Out },
                new CreatePortOp { PortId = "in", NodeId = "b", Direction = PortDirections.In },
            ],
            CreateEdges =
            [
                new CreateEdgeOp { SourcePortId = "out", TargetPortId = "in" },
                new CreateEdgeOp { SourcePortId = "in", TargetPortId = "out" },
            ],
        });

        var pattern = new SubgraphPattern
        {
            Nodes = [new PatternNode { Key = "src" }, new PatternNode { Key = "dst" }],
            Ports =
            [
                new PatternPort { Key = "y", Node = "src", Direction = PortDirections.Out },
                new PatternPort { Key = "r", Node = "dst", Direction = PortDirections.In },
            ],
            Edges = [new PatternEdge { SourcePort = "y", TargetPort = "r" }],
        };
        var scope = new MatchScope { DocumentId = created.DocumentId, VersionId = created.VersionId };
        var page = new PageRequest { First = 10, IncludeTotalCount = true };

        var loose = await Store.MatchSubgraph(pattern, scope, new MatchOptions { Injective = true, Induced = false }, page);
        Assert.AreEqual(1, loose.TotalCount);

        var induced = await Store.MatchSubgraph(pattern, scope, new MatchOptions { Injective = true, Induced = true }, page);
        Assert.AreEqual(0, induced.TotalCount);
    }

    [TestMethod]
    public async Task AttachNodeType_DoesNotOverwriteCatalogName()
    {
        var created = await Store.CreateDocument("catalog-name", "catalog-name");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes =
            [
                new CreateNodeOp { NodeId = "a", TypeId = "shared-type", Name = "Alpha" },
                new CreateNodeOp { NodeId = "b", TypeId = "shared-type", Name = "Beta" },
            ],
        });

        var type = await Store.GetNodeType(GraphOrigins.GraphRoots, "shared-type");
        Assert.IsNotNull(type);
        Assert.AreEqual("Alpha", type.Name);
        var a = await Store.GetNode(created.VersionId, "a");
        var b = await Store.GetNode(created.VersionId, "b");
        Assert.AreEqual("Alpha", a!.Name);
        Assert.AreEqual("Beta", b!.Name);
    }

    [TestMethod]
    public async Task GetIsolatedNodes_IncludesNullKindPortlessNode()
    {
        var created = await Store.CreateDocument("iso-null", "iso-null");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes = [new CreateNodeOp { NodeId = "gh-like", Name = "Bare" }],
        });

        var graph = await Store.GetDocumentGraph(created.DocumentId, created.VersionId);
        Assert.AreEqual(1, graph.Nodes.Count);
        Assert.IsNull(graph.Nodes[0].Kind);

        var isolated = await Store.GetIsolatedNodes(created.DocumentId, created.VersionId);
        CollectionAssert.AreEqual(new[] { "gh-like" }, isolated.Select(n => n.NodeId).ToArray());
    }

    [TestMethod]
    public async Task ApplyPatch_NilVersionId_NotFound()
    {
        var created = await Store.CreateDocument("nil-ver", "nil-ver");
        var ex = await Assert.ThrowsExactlyAsync<GraphStoreException>(() => Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = Guid.Empty,
            CreateNodes = [new CreateNodeOp { NodeId = "x" }],
        }));
        Assert.AreEqual(GraphStoreException.Codes.NotFound, ex.Code);
    }

    [TestMethod]
    public async Task AddToGroups_RequiresGroupKind()
    {
        var created = await Store.CreateDocument("group-kind", "group-kind");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes =
            [
                new CreateNodeOp { NodeId = "slider", Kind = NodeKinds.Parameter },
                new CreateNodeOp { NodeId = "note", Kind = NodeKinds.Annotation },
            ],
        });

        var ex = await Assert.ThrowsExactlyAsync<GraphStoreException>(() => Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            AddToGroups = [new GroupMembershipOp { MemberNodeId = "note", GroupNodeId = "slider" }],
        }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public async Task CreateEdge_RejectsSelfLoop()
    {
        var created = await Store.CreateDocument("self-loop", "self-loop");
        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateNodes = [new CreateNodeOp { NodeId = "op", Kind = NodeKinds.Operator }],
            CreatePorts = [new CreatePortOp { PortId = "out", NodeId = "op", Direction = PortDirections.Out }],
        });

        var ex = await Assert.ThrowsExactlyAsync<GraphStoreException>(() => Store.ApplyPatch(new GraphPatch
        {
            DocumentId = created.DocumentId,
            VersionId = created.VersionId,
            CreateEdges = [new CreateEdgeOp { SourcePortId = "out", TargetPortId = "out" }],
        }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public async Task VersionGraph_ForkCommitMergeAndHistory()
    {
        var importedVersion = Guid.NewGuid();
        var imported = (await ImportGrasshopper(
            [GhDocument("hist-a", importedVersion)],
            [GhNode("hist-a", importedVersion, "n1", NodeKinds.Operator)])).Document;

        var working = await Store.ForkDocument(imported.DocumentId, imported.VersionId, "hist-w");
        Assert.IsFalse(working.Committed);
        var based = await Store.GetBasedOn(working.DocumentId, working.VersionId);
        Assert.AreEqual(1, based.Count);
        Assert.AreEqual(imported.VersionId, based[0].VersionId);

        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = working.DocumentId,
            VersionId = working.VersionId,
            CreateNodes = [new CreateNodeOp { NodeId = "extra", Kind = NodeKinds.Parameter }],
        });

        var commit1 = await Store.CommitDocument(working.DocumentId, working.VersionId);
        Assert.IsTrue(commit1.Document.Committed);
        Assert.AreEqual(working.DocumentId, commit1.Document.DocumentId);
        Assert.AreEqual(working.VersionId, commit1.WorkingDocument.VersionId);
        Assert.IsFalse(commit1.WorkingDocument.Committed);
        Assert.IsNotNull(await Store.GetNode(commit1.Document.VersionId, "extra"));
        var commitParents = await Store.GetBasedOn(commit1.Document.DocumentId, commit1.Document.VersionId);
        Assert.AreEqual(1, commitParents.Count);
        Assert.AreEqual(imported.VersionId, commitParents[0].VersionId);
        var head = await Store.GetBasedOn(working.DocumentId, working.VersionId);
        Assert.AreEqual(1, head.Count);
        Assert.AreEqual(commit1.Document.VersionId, head[0].VersionId);

        var patchCommit = await Assert.ThrowsExactlyAsync<GraphStoreException>(() => Store.ApplyPatch(new GraphPatch
        {
            DocumentId = commit1.Document.DocumentId,
            VersionId = commit1.Document.VersionId,
            DeleteNodes = ["extra"],
        }));
        Assert.AreEqual(GraphStoreException.Codes.ImmutableDocument, patchCommit.Code);

        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = working.DocumentId,
            VersionId = working.VersionId,
            CreateNodes = [new CreateNodeOp { NodeId = "after-commit", Kind = NodeKinds.Annotation }],
        });

        var blank = await Store.CreateDocument("hist-blank", "blank");
        var first = await Store.CommitDocument(blank.DocumentId, blank.VersionId);
        Assert.AreEqual(0, (await Store.GetBasedOn(first.Document.DocumentId, first.Document.VersionId)).Count);

        var branch = await Store.ForkDocument(working.DocumentId, working.VersionId, "hist-b");
        var branchParents = await Store.GetBasedOn(branch.DocumentId, branch.VersionId);
        Assert.AreEqual(1, branchParents.Count);
        Assert.AreEqual(commit1.Document.VersionId, branchParents[0].VersionId);
        Assert.AreNotEqual(working.VersionId, branchParents[0].VersionId);

        await Store.ApplyPatch(new GraphPatch
        {
            DocumentId = branch.DocumentId,
            VersionId = branch.VersionId,
            CreateNodes = [new CreateNodeOp { NodeId = "branch-n", Kind = NodeKinds.Group }],
        });
        var branchCommit = await Store.CommitDocument(branch.DocumentId, branch.VersionId);

        var merge = await Store.MergeDocument(
            working.DocumentId,
            working.VersionId,
            [(branchCommit.Document.DocumentId, branchCommit.Document.VersionId)]);
        var mergeParents = await Store.GetBasedOn(merge.Document.DocumentId, merge.Document.VersionId);
        Assert.AreEqual(2, mergeParents.Count);
        var parentVersions = mergeParents.Select(p => p.VersionId).ToHashSet();
        Assert.IsTrue(parentVersions.Contains(commit1.Document.VersionId));
        Assert.IsTrue(parentVersions.Contains(branchCommit.Document.VersionId));

        var ancestors = await Store.TraverseHistory(working.DocumentId, working.VersionId, HistoryDirection.Ancestors, 1, 8);
        var ancestorVersions = ancestors.Select(a => a.VersionId).ToHashSet();
        Assert.IsTrue(ancestorVersions.Contains(merge.Document.VersionId));
        Assert.IsTrue(ancestorVersions.Contains(commit1.Document.VersionId));
        Assert.IsTrue(ancestorVersions.Contains(imported.VersionId));
        Assert.IsTrue(ancestorVersions.Contains(branchCommit.Document.VersionId));

        var ancestorEx = await Assert.ThrowsExactlyAsync<GraphStoreException>(() =>
            Store.MergeDocument(working.DocumentId, working.VersionId, [(commit1.Document.DocumentId, commit1.Document.VersionId)]));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ancestorEx.Code);

        var workingEx = await Assert.ThrowsExactlyAsync<GraphStoreException>(() =>
            Store.MergeDocument(working.DocumentId, working.VersionId, [(branch.DocumentId, branch.VersionId)]));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, workingEx.Code);

        var committedPage = await Store.ListDocuments(new DocumentFilter { Committed = true }, new PageRequest { First = 50, IncludeTotalCount = true });
        Assert.IsTrue(committedPage.Items.Any(d => d.VersionId == commit1.Document.VersionId));
        Assert.IsFalse(committedPage.Items.Any(d => d.VersionId == working.VersionId));
    }

    [TestMethod]
    public async Task MergeDocument_RejectsAncestorDeeperThanMaxHops()
    {
        var working = await Store.CreateDocument("deep-history", "deep-history");
        CommitDocumentResult? oldest = null;
        for (var i = 0; i < StoreLimits.MaxHops + 2; i++)
        {
            var commit = await Store.CommitDocument(working.DocumentId, working.VersionId);
            oldest ??= commit;
        }

        // The first commit is MaxHops + 2 BASED_ON hops behind the working copy; the ancestor check must still see it.
        var ex = await Assert.ThrowsExactlyAsync<GraphStoreException>(() =>
            Store.MergeDocument(working.DocumentId, working.VersionId, [(oldest!.Document.DocumentId, oldest.Document.VersionId)]));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
        StringAssert.Contains(ex.Message, "already an ancestor");
    }

    [TestMethod]
    public async Task CommitAndMerge_RejectSnapshots()
    {
        var version = Guid.NewGuid();
        await ImportGrasshopper([GhDocument("snap", version)]);

        var commit = await Assert.ThrowsExactlyAsync<GraphStoreException>(() => Store.CommitDocument("snap", version));
        Assert.AreEqual(GraphStoreException.Codes.ImmutableDocument, commit.Code);

        var merge = await Assert.ThrowsExactlyAsync<GraphStoreException>(() => Store.MergeDocument("snap", version, [("other", Guid.NewGuid())]));
        Assert.AreEqual(GraphStoreException.Codes.ImmutableDocument, merge.Code);

        var missing = await Assert.ThrowsExactlyAsync<GraphStoreException>(() => Store.CommitDocument("missing", Guid.NewGuid()));
        Assert.AreEqual(GraphStoreException.Codes.NotFound, missing.Code);

        var snapshot = await Store.GetDocument("snap", version);
        Assert.IsNotNull(snapshot);
        Assert.AreEqual(0, (await Store.GetDerivedDocuments("snap", version)).Count);
    }

    [TestMethod]
    public async Task ImportSnapshot_MergesGraphAndIsIdempotent()
    {
        var version = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var snapshot = new ImportSnapshot
        {
            Documents =
            [
                new ImportDocument
                {
                    DocumentId = "gh-doc",
                    VersionId = version,
                    Origin = GraphOrigins.Grasshopper,
                    FileName = "model.ghx",
                    IsNested = false,
                },
            ],
            Nodes =
            [
                new ImportNode
                {
                    DocumentId = "gh-doc",
                    VersionId = version,
                    NodeId = "n1",
                    Origin = GraphOrigins.Grasshopper,
                    TypeId = "type-1",
                    Name = "Add",
                    Kind = NodeKinds.Operator,
                },
            ],
            Ports =
            [
                new ImportPort
                {
                    DocumentId = "gh-doc",
                    VersionId = version,
                    NodeId = "n1",
                    PortId = "p1",
                    Name = "A",
                    Direction = PortDirections.In,
                },
            ],
            NodeTypes =
            [
                new ImportNodeType { Origin = GraphOrigins.Grasshopper, TypeId = "type-1", Name = "Add" },
            ],
        };

        var first = await Store.ImportSnapshot(snapshot);
        Assert.AreEqual("gh-doc", first.Document.DocumentId);
        Assert.AreEqual(1, first.NodeCount);
        Assert.AreEqual(GraphOrigins.Grasshopper, first.Document.Origin);
        Assert.IsFalse(first.Document.Committed);

        var renamed = new ImportSnapshot
        {
            Documents = snapshot.Documents,
            Nodes =
            [
                new ImportNode
                {
                    DocumentId = "gh-doc",
                    VersionId = version,
                    NodeId = "n1",
                    Origin = GraphOrigins.Grasshopper,
                    TypeId = "type-1",
                    Name = "Add renamed",
                    Kind = NodeKinds.Operator,
                },
            ],
            Ports = snapshot.Ports,
            NodeTypes = snapshot.NodeTypes,
        };
        var second = await Store.ImportSnapshot(renamed);
        Assert.AreEqual(first.Document.VersionId, second.Document.VersionId);
        var node = await Store.GetNode(version, "n1");
        Assert.AreEqual("Add renamed", node!.Name);

        var graph = await Store.GetDocumentGraph("gh-doc", version);
        Assert.AreEqual(1, graph.Nodes.Count);
        Assert.AreEqual(1, graph.Ports.Count);

        var immutable = await Assert.ThrowsExactlyAsync<GraphStoreException>(() =>
            Store.ApplyPatch(new GraphPatch
            {
                DocumentId = "gh-doc",
                VersionId = version,
                DeleteNodes = ["n1"],
            }));
        Assert.AreEqual(GraphStoreException.Codes.ImmutableDocument, immutable.Code);
    }
}
