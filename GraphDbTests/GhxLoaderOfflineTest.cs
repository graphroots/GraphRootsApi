using System;
using System.Collections.Generic;
using System.Linq;
using GraphRoots.GraphDb;
using GraphRoots.Grasshopper;
using GraphRoots.Grasshopper.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests
{
    [TestClass]
    public class GhxLoaderOfflineTest
    {
        IGhxArchiveParser Parser = TestParserFactory.Create();
        IGhxSnapshotBuilder Builder = new GhxSnapshotBuilder();

        static HashSet<Guid> ReferencedSources(IGhxChunkDefinition definition)
        {
            var ids = new HashSet<Guid>();
            foreach (var o in definition.DefinitionObjects.Objects)
            {
                if (o.Container.Sources != null)
                {
                    foreach (var source in o.Container.Sources)
                        ids.Add(source);
                }
                foreach (var input in o.Container.ParamInputs)
                {
                    if (input.Sources == null)
                        continue;
                    foreach (var source in input.Sources)
                        ids.Add(source);
                }
            }
            return ids;
        }

        static HashSet<string> ExpectedPortIds(IGhxChunkDefinition definition)
        {
            var referenced = ReferencedSources(definition);
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var o in definition.DefinitionObjects.Objects)
            {
                foreach (var p in o.Container.ParamInputs)
                    ids.Add(p.InstanceGuid.ToString());
                foreach (var p in o.Container.ParamOutputs)
                    ids.Add(p.InstanceGuid.ToString());

                var hasDeclared = o.Container.ParamInputs.Count > 0 || o.Container.ParamOutputs.Count > 0;
                var hasContainerSources = o.Container.Sources != null && o.Container.Sources.Count > 0;
                var isGroupOrAnnotation = o.Guid == GhComponentIds.Group || o.Guid == GhComponentIds.Scribble;
                if (!hasDeclared && (hasContainerSources
                    || (referenced.Contains(o.Container.InstanceGuid) && !isGroupOrAnnotation)))
                    ids.Add(o.Container.InstanceGuid.ToString());
            }
            return ids;
        }

        static string? ExpectedSyntheticDirection(string portId, IReadOnlyList<ImportEdge> edges)
        {
            var usedAsSource = edges.Any(e => string.Equals(e.SourcePortId, portId, StringComparison.OrdinalIgnoreCase));
            var usedAsTarget = edges.Any(e => string.Equals(e.TargetPortId, portId, StringComparison.OrdinalIgnoreCase));
            if (usedAsSource && usedAsTarget)
                return PortDirections.Both;
            if (usedAsTarget)
                return PortDirections.In;
            if (usedAsSource)
                return PortDirections.Out;
            return null;
        }

        [TestMethod]
        public void Load_Example1_RecordsDocumentNodesPortsAndEdges()
        {
            var filePath = TestUtilities.TestFilePath("ParserExample1.ghx");
            var context = LoaderContext.FromFile(Parser, filePath);
            var archive = context.GhxArchive;
            var snapshot = Builder.Build(context);
            ImportSnapshotValidator.Validate(snapshot);

            var documents = snapshot.Documents;
            Assert.AreEqual(1, documents.Count);
            Assert.AreEqual(false, documents[0].IsNested);
            Assert.AreEqual("ParserExample1.ghx", documents[0].FileName);
            Assert.AreEqual(GraphOrigins.Grasshopper, documents[0].Origin);

            var nodes = snapshot.Nodes.ToList();
            Assert.AreEqual(archive.Definition.DefinitionObjects.Objects.Count, nodes.Count);
            Assert.IsTrue(nodes.All(n => !string.IsNullOrEmpty(n.TypeId)), "Every node must have TypeId.");

            var typeKeys = snapshot.NodeTypes.Select(t => t.TypeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.IsTrue(nodes.All(n => typeKeys.Contains(n.TypeId!)));

            var ports = snapshot.Ports.ToList();
            Assert.IsTrue(ports.Count > 0, "Expected first-class ports.");
            var expectedPorts = ExpectedPortIds(archive.Definition);
            Assert.IsTrue(expectedPorts.All(id => ports.Any(p => p.PortId == id)), "Missing mapped ports.");

            var edges = snapshot.Edges;
            Assert.IsTrue(edges.Count > 0, "Expected at least one edge.");
            Assert.IsTrue(edges.All(e =>
                ports.Any(p => p.PortId == e.SourcePortId) &&
                ports.Any(p => p.PortId == e.TargetPortId)));

            var nodeIds = nodes.Select(n => n.NodeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var port in ports.Where(p => nodeIds.Contains(p.PortId)))
                Assert.AreEqual(ExpectedSyntheticDirection(port.PortId, edges), port.Direction, port.PortId);
        }

        [TestMethod]
        public void Load_Cluster_RecordsNestedDocumentAndNests()
        {
            var filePath = TestUtilities.TestFilePath("ParserExample4_Cluster.ghx");
            var context = LoaderContext.FromFile(Parser, filePath);
            var snapshot = Builder.Build(context);
            ImportSnapshotValidator.Validate(snapshot);

            var documents = snapshot.Documents.ToList();
            Assert.IsTrue(documents.Any(d => d.IsNested == false));
            Assert.IsTrue(documents.Any(d => d.IsNested == true));

            Assert.IsTrue(snapshot.Nests.Count > 0, "Expected a NESTS relationship.");
            var nodes = snapshot.Nodes.ToList();
            Assert.IsTrue(nodes.Any(n => n.Kind == NodeKinds.Cluster));
            Assert.IsTrue(nodes.All(n => !string.IsNullOrEmpty(n.TypeId)), "Every node must have TypeId.");
            var typeKeys = snapshot.NodeTypes.Select(t => t.TypeId).ToHashSet(StringComparer.OrdinalIgnoreCase);
            Assert.IsTrue(nodes.All(n => typeKeys.Contains(n.TypeId!)));
        }

        [TestMethod]
        public void Load_Group_RecordsMemberOf()
        {
            var filePath = TestUtilities.TestFilePath("ParserExample3_CanvasGroup.ghx");
            var context = LoaderContext.FromFile(Parser, filePath);
            var snapshot = Builder.Build(context);
            ImportSnapshotValidator.Validate(snapshot);

            var groups = snapshot.Nodes.Where(n => n.Kind == NodeKinds.Group).ToList();
            Assert.IsTrue(groups.Count > 0);
            Assert.IsTrue(groups.All(g => !string.IsNullOrEmpty(g.TypeId)));
            Assert.IsTrue(snapshot.GroupMemberships.Count > 0, "Expected MEMBER_OF edges.");
            Assert.IsFalse(
                snapshot.Ports.Any(p => groups.Any(g => g.NodeId == p.PortId)),
                "Unwired groups should not get a synthetic port.");
        }

        [TestMethod]
        public void Load_Scribble_IsAnnotationWithText()
        {
            var filePath = TestUtilities.TestFilePath("ParserExample6_Scribble.ghx");
            var context = LoaderContext.FromFile(Parser, filePath);
            var snapshot = Builder.Build(context);
            ImportSnapshotValidator.Validate(snapshot);

            var scribble = snapshot.Nodes.FirstOrDefault(n => n.Kind == NodeKinds.Annotation);
            Assert.IsNotNull(scribble);
            Assert.AreEqual("Doubleclick Me!", scribble.Text);
            Assert.IsFalse(string.IsNullOrEmpty(scribble.TypeId));
            Assert.IsFalse(snapshot.Ports.Any(p => p.PortId == scribble.NodeId),
                "Unwired annotation should not get a synthetic port.");
        }

        [TestMethod]
        public void Load_Rhino8Scripts_SetsLanguage()
        {
            var filePath = TestUtilities.TestFilePath("ParserExample7_Rhino8Scripts.ghx");
            var context = LoaderContext.FromFile(Parser, filePath);
            var snapshot = Builder.Build(context);
            ImportSnapshotValidator.Validate(snapshot);

            var scripts = snapshot.Nodes.Where(n => !string.IsNullOrEmpty(n.Language)).ToList();
            Assert.IsTrue(scripts.Count > 0, "Expected script language on nodes.");
            Assert.IsTrue(scripts.Any(n => n.Language!.Contains("rhinocode") || n.Source != null));
        }
    }
}
