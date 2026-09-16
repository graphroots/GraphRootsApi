using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using GraphRoots.Grasshopper;
using GraphRoots.Grasshopper.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo4j.Driver;

namespace GraphRoots.GraphDbTests
{
    [TestClass]
    public class GhxLoaderTest
    {
        static IDriver? Driver;

        static IDbOperations? DbOperations;

        static IGraphStore? Store;

        IGhxArchiveParser GhxArchiveParser = TestParserFactory.Create();
        IGhxSnapshotBuilder Builder = new GhxSnapshotBuilder();

        [ClassInitialize]
        public static async Task ClassInitialize(TestContext _context)
        {
            var settings = LiveNeo4j.Require();
            Driver = GraphDatabase.Driver(settings.Uri, AuthTokens.Basic(settings.User, settings.Password));
            DbOperations = new DbOperations(Driver, settings.Database);
            Store = new GraphStore(DbOperations);
            await DbOperations.EnsureSchema();
        }

        [TestInitialize]
        public async Task TestInitialize()
        {
            await DbOperations!.PurgeDatabase();
        }

        [TestMethod]
        public async Task Test_00_GhxLoader()
        {
            var filePath = TestUtilities.TestFilePath("ParserExample1.ghx");
            var context = LoaderContext.FromFile(GhxArchiveParser, filePath);
            var snapshot = Builder.Build(context);
            var result = await Store!.ImportSnapshot(snapshot);
            Assert.AreEqual(snapshot.Nodes.Count, result.NodeCount);
        }

        [TestMethod]
        public async Task Test_01_GhxLoader()
        {
            var files = TestUtilities.GetGrasshopperFilesInPath(string.Empty);
            foreach (var file in files)
            {
                Console.WriteLine($"Loading file: {file}");
                var context = LoaderContext.FromFile(GhxArchiveParser, file);
                var snapshot = Builder.Build(context);
                await Store!.ImportSnapshot(snapshot);
            }
        }

        [TestMethod]
        public async Task Test_02a_CreateNode()
        {
            var node = new Node
            {
                VersionId = Guid.NewGuid(),
                NodeId = Guid.NewGuid().ToString(),
                Origin = GraphOrigins.GraphRoots,
                Name = "Test",
            };
            await DbOperations!.CreateNode(node);
        }

        [TestMethod]
        public async Task Test_02b_MergeNode()
        {
            var node = new Node
            {
                VersionId = Guid.NewGuid(),
                NodeId = Guid.NewGuid().ToString(),
                Origin = GraphOrigins.GraphRoots,
                Name = "Test",
            };
            await DbOperations!.MergeNode(node);
            await DbOperations.MergeNode(node);
        }

        [TestMethod]
        public async Task Test_03_NodesAndEdges()
        {
            var versionId = Guid.NewGuid();
            var port1 = new Port { VersionId = versionId, PortId = Guid.NewGuid().ToString(), Name = "A", Direction = PortDirections.Out };
            var port2 = new Port { VersionId = versionId, PortId = Guid.NewGuid().ToString(), Name = "B", Direction = PortDirections.In };
            var port3 = new Port { VersionId = versionId, PortId = Guid.NewGuid().ToString(), Name = "C", Direction = PortDirections.In };
            var edge1 = new Edge { VersionId = versionId, SourcePortId = port1.PortId, TargetPortId = port2.PortId, SourceName = "A", TargetName = "B" };
            var edge2 = new Edge { VersionId = versionId, SourcePortId = port2.PortId, TargetPortId = port3.PortId, SourceName = "B", TargetName = "C" };

            await DbOperations!.MergeNodes(new List<Port> { port1, port2, port3 });
            await DbOperations.MergeRelationships(new List<Tuple<Port, Port, Edge>>
            {
                Tuple.Create(port1, port2, edge1),
                Tuple.Create(port2, port3, edge2),
            });
        }

        class NodeExt : IGraphDbNode
        {
            public IReadOnlyList<string>? Labels { get; set; }
            public string? NodeId { get; set; }
            public string? ElementId { get; set; }
            public float? X { get; set; }
        }

        class QueryResult04
        {
            public NodeExt? GraphNode { get; set; }
            public string? NodeId { get; set; }
            public float? X { get; set; }
        }

        [TestMethod]
        public async Task Test_04_QueryNode()
        {
            var node = new Node
            {
                VersionId = Guid.NewGuid(),
                NodeId = Guid.NewGuid().ToString(),
                Origin = GraphOrigins.GraphRoots,
                Name = "Test",
                X = 1.34f,
                Extensions = new Dictionary<string, object?> { [GhExtensionKeys.ComponentGuid] = "type-1" },
            };
            await DbOperations!.CreateNode(node);

            var query = "MATCH (n:Node {NodeId: $nodeId}) RETURN n AS GraphNode, n.NodeId AS NodeId, n.X AS X";
            var queriedNode = await DbOperations.RunQuery<QueryResult04>(query, new { nodeId = node.NodeId });

            Assert.AreEqual(1, queriedNode.Count);
            Assert.AreEqual(node.NodeId, queriedNode[0].NodeId);
            Assert.AreEqual(node.X, queriedNode[0].X);
            Assert.IsNotNull(queriedNode[0].GraphNode);
            Assert.AreEqual(node.NodeId, queriedNode[0].GraphNode!.NodeId);
            Assert.AreEqual("Node", queriedNode[0].GraphNode!.Labels!.First());

            var mapped = await DbOperations.RunQuery<QueryNodeRecord>(
                "MATCH (n:Node {NodeId: $nodeId}) RETURN n",
                new { nodeId = node.NodeId });
            Assert.AreEqual(1, mapped.Count);
            Assert.IsNotNull(mapped[0].n);
            Assert.AreEqual("type-1", mapped[0].n!.Extensions![GhExtensionKeys.ComponentGuid]);
        }

        class QueryNodeRecord
        {
            public Node? n { get; set; }
        }

        class QueryDocumentRecord
        {
            public Document? d { get; set; }
        }

        [TestMethod]
        public async Task Test_04b_QueryDocumentDateTime()
        {
            var created = new DateTime(2024, 6, 15, 12, 30, 45, 123, DateTimeKind.Utc);
            var written = new DateTime(2024, 6, 16, 8, 0, 0, DateTimeKind.Utc);
            var document = new Document
            {
                DocumentId = Guid.NewGuid().ToString(),
                VersionId = Guid.NewGuid(),
                Origin = GraphOrigins.GraphRoots,
                FileCreationTimeUtc = created,
                FileLastWriteTimeUtc = written,
            };
            await DbOperations!.MergeNode(document);

            var mapped = await DbOperations.RunQuery<QueryDocumentRecord>(
                "MATCH (d:Document {DocumentId: $id, VersionId: $versionId}) RETURN d",
                new { id = document.DocumentId, versionId = document.VersionId.ToString() });

            Assert.AreEqual(1, mapped.Count);
            Assert.IsNotNull(mapped[0].d);
            AssertUtcClose(created, mapped[0].d!.FileCreationTimeUtc);
            AssertUtcClose(written, mapped[0].d!.FileLastWriteTimeUtc);
        }

        static void AssertUtcClose(DateTime expected, DateTime? actual)
        {
            Assert.IsNotNull(actual);
            var actualUtc = actual.Value.Kind == DateTimeKind.Utc
                ? actual.Value
                : actual.Value.ToUniversalTime();
            var delta = Math.Abs((expected - actualUtc).TotalMilliseconds);
            Assert.IsTrue(delta < 1, $"Expected {expected:o}, got {actual:o}");
        }

        class PortExt : IGraphDbNode
        {
            public IReadOnlyList<string>? Labels { get; set; }
            public string? PortId { get; set; }
            public string? ElementId { get; set; }
        }

        class EdgeExt : IGraphDbRelationship
        {
            public string? Type { get; set; }
            public string? StartNodeElementId { get; set; }
            public string? EndNodeElementId { get; set; }
            public string? ElementId { get; set; }
        }

        class PathExt : IGraphDbPath<PortExt, EdgeExt>
        {
            public IReadOnlyList<PortExt> Nodes { get; set; } = Array.Empty<PortExt>();
            public IReadOnlyList<EdgeExt> Relationships { get; set; } = Array.Empty<EdgeExt>();
            public PortExt Start { get; set; } = new PortExt();
            public PortExt End { get; set; } = new PortExt();
        }

        class QueryResult05
        {
            public EdgeExt? Rel { get; set; }
            public PortExt? StartNode { get; set; }
            public PortExt? EndNode { get; set; }
        }

        class QueryResult06
        {
            public PathExt? Path { get; set; }
        }

        [TestMethod]
        public async Task Test_05_QueryRelationship()
        {
            var versionId = Guid.NewGuid();
            var port1 = new Port { VersionId = versionId, PortId = Guid.NewGuid().ToString(), Name = "A", Direction = PortDirections.Out };
            var port2 = new Port { VersionId = versionId, PortId = Guid.NewGuid().ToString(), Name = "B", Direction = PortDirections.In };
            var edge = new Edge { VersionId = versionId, SourcePortId = port1.PortId, TargetPortId = port2.PortId, SourceName = "A", TargetName = "B" };

            await DbOperations!.MergeNodes(new List<Port> { port1, port2 });
            await DbOperations.MergeRelationships(new List<Tuple<Port, Port, Edge>>
            {
                Tuple.Create(port1, port2, edge),
            });

            var query = "MATCH (n:Port {PortId: $portId})-[w:EDGE]->(m:Port) RETURN w AS Rel, n AS StartNode, m AS EndNode";
            var result = await DbOperations.RunQuery<QueryResult05>(query, new { portId = port1.PortId });

            Assert.AreEqual(1, result.Count);
            Assert.IsNotNull(result[0].Rel);
            Assert.IsNotNull(result[0].StartNode);
            Assert.IsNotNull(result[0].EndNode);
            Assert.AreEqual(result[0].Rel!.StartNodeElementId, result[0].StartNode!.ElementId);
            Assert.AreEqual(result[0].Rel!.EndNodeElementId, result[0].EndNode!.ElementId);
            Assert.AreEqual(port1.PortId, result[0].StartNode!.PortId);
            Assert.AreEqual("Port", result[0].StartNode!.Labels!.First());
            Assert.AreEqual("EDGE", result[0].Rel!.Type);

            var query2 = "MATCH p=(n:Port {PortId: $portId})-[w:EDGE]->(m:Port) RETURN p AS Path";
            var result2 = await DbOperations.RunQuery<QueryResult06>(query2, new { portId = port1.PortId });

            Assert.AreEqual(1, result2.Count);
            Assert.IsNotNull(result2[0].Path);
            Assert.AreEqual(port1.PortId, result2[0].Path!.Start.PortId);
            Assert.AreEqual("Port", result2[0].Path!.Start.Labels!.First());
            Assert.AreEqual(result2[0].Path!.Start.ElementId, result2[0].Path!.Relationships.First().StartNodeElementId);
        }

        [TestCleanup]
        public void TestCleanup()
        {
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            Driver?.Dispose();
        }
    }
}
