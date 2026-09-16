using System;
using System.IO;
using System.Linq;
using GraphRoots.Grasshopper.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests
{
    [TestClass]
    public class GhxArchiveParserTest
    {
        IGhxArchiveParser Parser = TestParserFactory.Create();

        [TestMethod]
        public void Parse_Example1_HasDocumentAndComponents()
        {
            var archive = Parser.ParseArchiveFile(TestUtilities.TestFilePath("ParserExample1.ghx"));

            Assert.IsNotNull(archive.Definition);
            Assert.AreEqual(Guid.Parse("98f91b77-0811-4935-8a79-e601ad06b86a"), archive.Definition.DocumentHeader.DocumentID);
            Assert.AreEqual(4, archive.Definition.DefinitionObjects.ObjectCount);
            Assert.AreEqual(4, archive.Definition.DefinitionObjects.Objects.Count);
            Assert.IsTrue(archive.Definition.DefinitionObjects.Objects.Any(o => o.DefinitionName == "Sphere"));
            Assert.IsTrue(archive.Definition.DefinitionObjects.Objects.Any(o => o.Container.Attributes.Pivot != null));
        }

        [TestMethod]
        public void Parse_Cluster_HasDefinitionAndHash()
        {
            var archive = Parser.ParseArchiveFile(TestUtilities.TestFilePath("ParserExample4_Cluster.ghx"));

            var clusters = archive.Definition.DefinitionObjects.Objects
                .Where(o => o.Container.ClusterDefinition != null)
                .ToList();

            Assert.IsTrue(clusters.Count > 0, "Expected at least one cluster component.");
            foreach (var cluster in clusters)
            {
                Assert.IsNotNull(cluster.Container.ClusterDefinition);
                Assert.IsTrue(cluster.Container.ClusterHash.HasValue);
            }
        }

        [TestMethod]
        public void Parse_Group_HasMemberGuids()
        {
            var archive = Parser.ParseArchiveFile(TestUtilities.TestFilePath("ParserExample3_CanvasGroup.ghx"));

            var groups = archive.Definition.DefinitionObjects.Objects
                .Where(o => o.Guid == GhComponentIds.Group)
                .ToList();

            Assert.IsTrue(groups.Count > 0, "Expected a group component.");
            var memberIds = groups[0].Container.Items!.GetItemsByName("ID").ToList();
            Assert.IsTrue(memberIds.Count > 0);
        }

        [TestMethod]
        public void Parse_Scribble_HasText()
        {
            var archive = Parser.ParseArchiveFile(TestUtilities.TestFilePath("ParserExample6_Scribble.ghx"));

            var scribble = archive.Definition.DefinitionObjects.Objects
                .FirstOrDefault(o => o.Guid == GhComponentIds.Scribble);

            Assert.IsNotNull(scribble);
            Assert.AreEqual("Doubleclick Me!", scribble.Container.Items!.GetItemByName("Text")!.Value);
        }

        [TestMethod]
        public void Parse_Rhino8Scripts_HasScriptLanguage()
        {
            var archive = Parser.ParseArchiveFile(TestUtilities.TestFilePath("ParserExample7_Rhino8Scripts.ghx"));

            var scripts = archive.Definition.DefinitionObjects.Objects
                .Where(o => GhComponentIds.IsRhinoCodeScriptComponent(o.Guid))
                .ToList();

            Assert.IsTrue(scripts.Count > 0, "Expected a RhinoCode script component.");
            foreach (var script in scripts)
            {
                Assert.IsFalse(string.IsNullOrEmpty(GhComponentIds.GetRhinoCodeScriptComponentLanguage(script.Guid)));
            }
        }

        [TestMethod]
        public void Convert_XmlToBinaryToXml_PreservesDocumentId()
        {
            var xml = File.ReadAllText(TestUtilities.TestFilePath("ParserExample4_Cluster.ghx"));
            var ghArchive = new GH_IO.Serialization.GH_Archive();
            Assert.IsTrue(ghArchive.Deserialize_Xml(xml));
            var bytes = ghArchive.Serialize_Binary();
            var converted = new GhxArchiveConverter().ConvertByteArrayToXml(bytes);
            var parsed = Parser.ParseArchiveString(converted);
            Assert.AreEqual(Guid.Parse("98f91b77-0811-4935-8a79-e601ad06b86a"), parsed.Definition.DocumentHeader.DocumentID);
            Assert.IsTrue(parsed.Definition.DefinitionObjects.Objects.Any(o => o.Container.ClusterDefinition != null));
        }

        [TestMethod]
        public void Parse_AllGhxFiles_DoesNotThrow()
        {
            foreach (var file in TestUtilities.GetGrasshopperFilesInPath(string.Empty))
            {
                var archive = Parser.ParseArchiveFile(file);
                Assert.IsNotNull(archive.Definition, file);
                Assert.AreNotEqual(Guid.Empty, archive.Definition.DocumentHeader.DocumentID, file);
            }
        }
    }
}
