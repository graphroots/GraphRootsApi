using System;
using System.Linq;
using GraphRoots.GraphDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests;

[TestClass]
public class SubgraphMatcherTest
{
    [TestMethod]
    public void DetectMode_RejectsBothEdgesAndConnections()
    {
        var pattern = new SubgraphPattern
        {
            Nodes = [new PatternNode { Key = "a" }, new PatternNode { Key = "b" }],
            Ports = [new PatternPort { Key = "p", Node = "a" }],
            Edges = [new PatternEdge { SourcePort = "p", TargetPort = "p" }],
            Connections = [new PatternConnection { SourceNode = "a", TargetNode = "b" }],
        };
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => SubgraphMatcher.DetectMode(pattern));
        Assert.AreEqual(GraphStoreException.Codes.InvalidPattern, ex.Code);
    }

    [TestMethod]
    public void Validate_RejectsUnknownPortReference()
    {
        var pattern = new SubgraphPattern
        {
            Nodes = [new PatternNode { Key = "a" }],
            Ports = [new PatternPort { Key = "p", Node = "missing" }],
            Edges = [new PatternEdge { SourcePort = "p", TargetPort = "p" }],
        };
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => SubgraphMatcher.Validate(pattern));
        Assert.AreEqual(GraphStoreException.Codes.InvalidPattern, ex.Code);
    }

    [TestMethod]
    public void Validate_RejectsOversizedPattern()
    {
        var nodes = Enumerable.Range(0, SubgraphMatcher.MaxPatternNodes + 1)
            .Select(i => new PatternNode { Key = $"n{i}" })
            .ToList();
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => SubgraphMatcher.Validate(new SubgraphPattern { Nodes = nodes }));
        Assert.AreEqual(GraphStoreException.Codes.PatternTooLarge, ex.Code);
    }

    [TestMethod]
    public void Compile_PortExplicit_UsesParametersNotInterpolation()
    {
        var compiled = SubgraphMatcher.Compile(
            new SubgraphPattern
            {
                Nodes =
                [
                    new PatternNode { Key = "slider", Kind = NodeKinds.Parameter },
                    new PatternNode { Key = "op", TypeId = "type-1" },
                ],
                Ports =
                [
                    new PatternPort { Key = "y", Node = "slider", Direction = PortDirections.Out },
                    new PatternPort { Key = "r", Node = "op", Direction = PortDirections.In, Name = "Radius" },
                ],
                Edges = [new PatternEdge { Key = "wire", SourcePort = "y", TargetPort = "r" }],
            },
            new MatchScope { VersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") },
            new MatchOptions { Injective = true },
            afterCursor: null,
            limit: 10);

        Assert.AreEqual(SubgraphPatternMode.PortExplicit, compiled.Mode);
        StringAssert.Contains(compiled.Cypher, "MATCH");
        StringAssert.Contains(compiled.Cypher, "[:HAS_PORT]");
        StringAssert.Contains(compiled.Cypher, ":EDGE");
        StringAssert.Contains(compiled.Cypher, "$scopeVersionId");
        StringAssert.Contains(compiled.Cypher, "LIMIT $limit");
        Assert.IsFalse(compiled.Cypher.Contains("SKIP", StringComparison.Ordinal));
        Assert.IsFalse(compiled.Cypher.Contains("type-1", StringComparison.Ordinal));
        Assert.IsFalse(compiled.Cypher.Contains("Radius", StringComparison.Ordinal));
        Assert.IsTrue(compiled.Parameters.Values.Contains("type-1"));
        Assert.IsTrue(compiled.Parameters.Values.Contains("Radius"));
        Assert.IsTrue(compiled.NodeKeys.Contains("slider"));
        Assert.IsTrue(compiled.PortKeys.Contains("y"));
        Assert.IsTrue(compiled.EdgeKeys.Contains("wire"));
        AssertNoConsecutiveWhere(compiled.Cypher);
    }

    [TestMethod]
    public void Compile_NodeConnection_EmitsPortEdgeWalk()
    {
        var compiled = SubgraphMatcher.Compile(
            new SubgraphPattern
            {
                Nodes =
                [
                    new PatternNode { Key = "slider", Kind = NodeKinds.Parameter },
                    new PatternNode { Key = "op", Kind = NodeKinds.Operator },
                ],
                Connections =
                [
                    new PatternConnection
                    {
                        Key = "feed",
                        SourceNode = "slider",
                        TargetNode = "op",
                        SourceDirection = PortDirections.Out,
                    },
                ],
            },
            new MatchScope { DocumentId = "doc-1" },
            new MatchOptions(),
            afterCursor: null,
            limit: 5);

        Assert.AreEqual(SubgraphPatternMode.NodeConnection, compiled.Mode);
        StringAssert.Contains(compiled.Cypher, "HAS_PORT");
        StringAssert.Contains(compiled.Cypher, "EDGE");
        StringAssert.Contains(compiled.Cypher, "scopeVersionIds");
        Assert.IsFalse(compiled.Cypher.Contains("doc-1", StringComparison.Ordinal));
        Assert.AreEqual("doc-1", compiled.Parameters["scopeDocumentId"]);
        Assert.IsTrue(compiled.ConnectionKeys.Contains("feed"));
        AssertNoConsecutiveWhere(compiled.Cypher);
    }

    [TestMethod]
    public void Compile_OrdersByTypeIdSelectivity()
    {
        var compiled = SubgraphMatcher.Compile(
            new SubgraphPattern
            {
                Nodes =
                [
                    new PatternNode { Key = "any" },
                    new PatternNode { Key = "typed", TypeId = "rare" },
                ],
            },
            null,
            null,
            null,
            1);

        var typedPos = compiled.Cypher.IndexOf("n0:Node", StringComparison.Ordinal);
        var anyPos = compiled.Cypher.IndexOf("n1:Node", StringComparison.Ordinal);
        Assert.IsTrue(typedPos >= 0 && anyPos > typedPos);
        Assert.IsTrue(compiled.Parameters.Values.Contains("rare"));
        AssertNoConsecutiveWhere(compiled.Cypher);
    }

    [TestMethod]
    public void Validate_RejectsDuplicateKeys()
    {
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => SubgraphMatcher.Validate(new SubgraphPattern
        {
            Nodes = [new PatternNode { Key = "a" }, new PatternNode { Key = "a" }],
        }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidPattern, ex.Code);
    }

    [TestMethod]
    public void Compile_GeneratesUniqueEdgeKeysWhenOmitted()
    {
        var compiled = SubgraphMatcher.Compile(
            new SubgraphPattern
            {
                Nodes = [new PatternNode { Key = "a" }],
                Ports = [new PatternPort { Key = "p", Node = "a" }, new PatternPort { Key = "q", Node = "a" }],
                Edges =
                [
                    new PatternEdge { SourcePort = "p", TargetPort = "q" },
                    new PatternEdge { SourcePort = "q", TargetPort = "p" },
                ],
            },
            null,
            null,
            null,
            1);
        Assert.AreEqual(2, compiled.EdgeKeys.Distinct().Count());
    }

    [TestMethod]
    public void Compile_MultiHop_BindsPathAndEndpointPredicates()
    {
        var compiled = SubgraphMatcher.Compile(
            new SubgraphPattern
            {
                Nodes =
                [
                    new PatternNode { Key = "src" },
                    new PatternNode { Key = "dst" },
                ],
                Connections =
                [
                    new PatternConnection
                    {
                        Key = "path",
                        SourceNode = "src",
                        TargetNode = "dst",
                        SourcePortName = "Y",
                        TargetPortName = "A",
                        SourceDirection = PortDirections.Out,
                        TargetDirection = PortDirections.In,
                        MinHops = 2,
                        MaxHops = 3,
                    },
                ],
            },
            new MatchScope { DocumentId = "doc", VersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") },
            new MatchOptions { Injective = true, Induced = true },
            afterCursor: null,
            limit: 4);

        StringAssert.Contains(compiled.Cypher, "{2,3}");
        StringAssert.Contains(compiled.Cypher, "cports0");
        StringAssert.Contains(compiled.Cypher, "cedges0");
        StringAssert.Contains(compiled.Cypher, "AS conn_path_ports");
        StringAssert.Contains(compiled.Cypher, "ORDER BY");
        StringAssert.Contains(compiled.Cypher, "LIMIT $limit");
        Assert.IsTrue(compiled.Parameters.Values.Contains("Y"));
        Assert.IsTrue(compiled.Parameters.Values.Contains("A"));
        StringAssert.Contains(compiled.Cypher, "elementId");
        StringAssert.Contains(compiled.Cypher, "NOT EXISTS");
        Assert.IsFalse(compiled.Cypher.Contains("doc", StringComparison.Ordinal));
        AssertNoConsecutiveWhere(compiled.Cypher);
        StringAssert.Contains(compiled.Cypher, "WITH * WHERE");
    }

    [TestMethod]
    public void Compile_RejectsHostileExtensionKey()
    {
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => SubgraphMatcher.Compile(
            new SubgraphPattern
            {
                Nodes =
                [
                    new PatternNode
                    {
                        Key = "n",
                        Extensions = [new ExtensionPredicate { Key = "n.TypeId} DETACH DELETE n //", EqualsValue = "x" }],
                    },
                ],
            },
            null,
            null,
            null,
            1));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void Compile_UsesDynamicPropertyForExtensions()
    {
        var compiled = SubgraphMatcher.Compile(
            new SubgraphPattern
            {
                Nodes =
                [
                    new PatternNode
                    {
                        Key = "n",
                        Extensions = [new ExtensionPredicate { Key = "gh.componentGuid", EqualsValue = "abc" }],
                    },
                ],
            },
            null,
            null,
            null,
            1);
        StringAssert.Contains(compiled.Cypher, "n0[$");
        Assert.IsFalse(compiled.Cypher.Contains("gh.componentGuid", StringComparison.Ordinal));
        Assert.IsTrue(compiled.Parameters.Values.Contains("gh.componentGuid"));
        Assert.IsTrue(compiled.Parameters.Values.Contains("abc"));
    }

    [TestMethod]
    public void Compile_UnknownOrigin_ExcludesKnownOrigins()
    {
        var compiled = SubgraphMatcher.Compile(
            new SubgraphPattern
            {
                Nodes = [new PatternNode { Key = "n", Origin = GraphOrigins.UnknownFilter }],
            },
            new MatchScope { Origin = GraphOrigins.UnknownFilter },
            new MatchOptions(),
            afterCursor: null,
            limit: 1);

        StringAssert.Contains(compiled.Cypher, "NOT coalesce(n0.Origin, '') IN $scopeOrigin");
        StringAssert.Contains(compiled.Cypher, "NOT coalesce(n0.Origin, '') IN $n0_Origin");
        Assert.IsFalse(compiled.Parameters.Values.Contains(GraphOrigins.UnknownFilter));
        Assert.IsTrue(compiled.Parameters.Values.OfType<string[]>().Any(a => a.SequenceEqual(GraphOrigins.Known)));
    }

    [TestMethod]
    public void Compile_TwoInstanceNodes_DefaultInjective_DoesNotEmitConsecutiveWhere()
    {
        var compiled = SubgraphMatcher.Compile(
            new SubgraphPattern
            {
                Nodes = [new PatternNode { Key = "a" }, new PatternNode { Key = "b" }],
            },
            new MatchScope { DocumentId = "doc", VersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") },
            new MatchOptions(),
            afterCursor: null,
            limit: 10);

        AssertNoConsecutiveWhere(compiled.Cypher);
        StringAssert.Contains(compiled.Cypher, "WITH * WHERE");
        StringAssert.Contains(compiled.Cypher, "elementId");
    }

    [TestMethod]
    public void Compile_IncludeNested_CapsNestsAndCollectsVersionIds()
    {
        var compiled = SubgraphMatcher.Compile(
            new SubgraphPattern { Nodes = [new PatternNode { Key = "n" }] },
            new MatchScope
            {
                DocumentId = "parent",
                VersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                IncludeNested = true,
            },
            new MatchOptions { Injective = false },
            afterCursor: null,
            limit: 1);

        StringAssert.Contains(compiled.Cypher, $"NESTS*0..{SubgraphMatcher.MaxHops}");
        StringAssert.Contains(compiled.Cypher, "collect(DISTINCT nestedDoc.VersionId) AS scopeVersionIds");
        Assert.IsFalse(compiled.Cypher.Contains("+ scopeDoc.VersionId", StringComparison.Ordinal));
        AssertNoConsecutiveWhere(compiled.Cypher);
    }

    static void AssertNoConsecutiveWhere(string cypher)
    {
        Assert.IsFalse(
            System.Text.RegularExpressions.Regex.IsMatch(cypher, @"\nWHERE .+\nWHERE "),
            "Compiled Cypher must not emit consecutive WHERE clauses.\n" + cypher);
    }
}
