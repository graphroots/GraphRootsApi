using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using GraphRoots.GraphDb;
using GraphRoots.Grasshopper;
using GraphRoots.Grasshopper.Parser;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests
{
    [TestClass]
    public class GhStateHashTest
    {
        const string SliderValue = "<item name=\"Value\" type_name=\"gh_double\" type_code=\"6\">0.25</item>";

        IGhxArchiveParser Parser = TestParserFactory.Create();
        IGhxSnapshotBuilder Builder = new GhxSnapshotBuilder();

        static string FixturePath => TestUtilities.TestFilePath("ParserExample3_CanvasGroup.ghx");

        Dictionary<string, ImportNode> Build(string xml)
        {
            var context = new LoaderContext(Parser.ParseArchiveString(xml), FixturePath, Encoding.UTF8.GetBytes(xml));
            return Builder.Build(context).Nodes.ToDictionary(n => n.NodeId);
        }

        static string Hash(ImportNode node) => (string)node.Extensions![GhExtensionKeys.StateHash]!;

        [TestMethod]
        public void StateHash_ChangesOnlyForTheEditedSlider()
        {
            var xml = File.ReadAllText(FixturePath);
            Assert.AreEqual(1, Regex.Matches(xml, Regex.Escape(SliderValue)).Count);

            var before = Build(xml);
            var after = Build(xml.Replace(SliderValue, SliderValue.Replace(">0.25<", ">0.75<")));

            Assert.IsTrue(before.Values.All(n => Regex.IsMatch(Hash(n), "^[0-9a-f]{64}$")));
            var changed = before.Keys.Where(id => Hash(before[id]) != Hash(after[id])).ToList();
            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual("Number Slider", after[changed[0]].Name);
        }

        [TestMethod]
        public void StateHash_IgnoresRenameAndMove()
        {
            var xml = File.ReadAllText(FixturePath);
            var edited = Regex.Replace(xml,
                "(<item name=\"NickName\" type_name=\"gh_string\" type_code=\"10\">)([^<]*)(</item>)",
                "$1$2 renamed$3");
            edited = Regex.Replace(edited,
                "(<item name=\"Pivot\"[^>]*>\\s*<X>)([^<]+)(</X>)",
                m => m.Groups[1].Value
                    + (double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) + 40).ToString(CultureInfo.InvariantCulture)
                    + m.Groups[3].Value);

            var before = Build(xml);
            var after = Build(edited);

            CollectionAssert.AreEquivalent(before.Keys.ToList(), after.Keys.ToList());
            Assert.IsTrue(before.Keys.Any(id => before[id].NickName != after[id].NickName));
            Assert.IsTrue(before.Keys.Any(id => before[id].X != after[id].X));
            foreach (var id in before.Keys)
                Assert.AreEqual(Hash(before[id]), Hash(after[id]), $"node {id} ({before[id].Name})");
        }

        [TestMethod]
        public void StateHash_IgnoresSelection()
        {
            var path = TestUtilities.TestFilePath("ParserExample2_VariableInputs.ghx");
            var xml = File.ReadAllText(path);
            const string selected = "<item name=\"Selected\" type_name=\"gh_bool\" type_code=\"1\">true</item>";
            Assert.IsTrue(xml.Contains(selected), "fixture has selected components");
            var deselected = xml.Replace(selected, selected.Replace(">true<", ">false<"));

            var before = Build(xml);
            var after = Build(deselected);

            CollectionAssert.AreEquivalent(before.Keys.ToList(), after.Keys.ToList());
            foreach (var id in before.Keys)
                Assert.AreEqual(Hash(before[id]), Hash(after[id]), $"node {id} ({before[id].Name})");
        }

        [TestMethod]
        public void StateHash_ChangesWhenAParameterFlagIsToggled()
        {
            const string hidden = "<item name=\"Hidden\" type_name=\"gh_bool\" type_code=\"1\">true</item>";
            var xml = File.ReadAllText(FixturePath);
            Assert.AreEqual(1, Regex.Matches(xml, Regex.Escape(hidden)).Count);

            var before = Build(xml);
            var after = Build(xml.Replace(hidden, hidden.Replace(">true<", ">false<")));

            Assert.AreEqual(1, before.Keys.Count(id => Hash(before[id]) != Hash(after[id])));
        }
    }
}
