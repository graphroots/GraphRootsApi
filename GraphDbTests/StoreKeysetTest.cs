using System;
using System.Linq;
using GraphRoots.GraphDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests;

[TestClass]
public class StoreKeysetTest
{
    [TestMethod]
    public void EncodeSort_DistinguishesNullFromEmptyString()
    {
        Assert.AreEqual("N", StoreKeyset.EncodeSort(null));
        Assert.AreEqual("V", StoreKeyset.EncodeSort(""));
        Assert.AreEqual("Vplanarise", StoreKeyset.EncodeSort("planarise"));
    }

    [TestMethod]
    public void KindOf_UsesNativeTypesForLayoutAndTimestamps()
    {
        Assert.AreEqual(StoreKeyset.ValueKind.Number, StoreKeyset.KindOf("n.X"));
        Assert.AreEqual(StoreKeyset.ValueKind.Number, StoreKeyset.KindOf("n.Y"));
        Assert.AreEqual(StoreKeyset.ValueKind.DateTime, StoreKeyset.KindOf("n.FileCreationTimeUtc"));
        Assert.AreEqual(StoreKeyset.ValueKind.String, StoreKeyset.KindOf("n.FileName"));
        Assert.AreEqual(StoreKeyset.ValueKind.String, StoreKeyset.KindOf("n.NodeId"));
    }

    [TestMethod]
    public void AfterPredicate_AscPastNonNull_IncludesRemainingNulls()
    {
        var clause = StoreKeyset.AfterPredicate(
            "n.X",
            StoreKeyset.ValueKind.Number,
            desc: false,
            cursorNull: false,
            sortParam: "cSort_0",
            ties: ["n.VersionId", "n.NodeId"],
            tieParams: ["cTie_1", "cTie_2"]);
        StringAssert.Contains(clause, "n.X IS NULL");
        StringAssert.Contains(clause, "n.X > $cSort_0");
        Assert.IsFalse(clause.Contains("toString(", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AfterPredicate_DescPastNull_ContinuesToNonNullValues()
    {
        var clause = StoreKeyset.AfterPredicate(
            "n.FileCreationTimeUtc",
            StoreKeyset.ValueKind.DateTime,
            desc: true,
            cursorNull: true,
            sortParam: null,
            ties: ["n.DocumentId", "n.VersionId"],
            tieParams: ["cTie_1", "cTie_2"]);
        StringAssert.Contains(clause, "n.FileCreationTimeUtc IS NOT NULL");
        StringAssert.Contains(clause, "n.FileCreationTimeUtc IS NULL");
    }

    [TestMethod]
    public void AfterPredicate_DescPastNonNull_DoesNotReselectNulls()
    {
        var clause = StoreKeyset.AfterPredicate(
            "n.X",
            StoreKeyset.ValueKind.Number,
            desc: true,
            cursorNull: false,
            sortParam: "cSort_0",
            ties: ["n.VersionId", "n.NodeId"],
            tieParams: ["cTie_1", "cTie_2"]);
        StringAssert.Contains(clause, "n.X IS NOT NULL");
        StringAssert.Contains(clause, "n.X < $cSort_0");
        Assert.IsFalse(clause.Contains("n.X IS NULL)", StringComparison.Ordinal));
    }

    [TestMethod]
    public void AfterPredicate_DateTimeUsesDatetimeWrap()
    {
        var clause = StoreKeyset.AfterPredicate(
            "n.FileCreationTimeUtc",
            StoreKeyset.ValueKind.DateTime,
            desc: false,
            cursorNull: false,
            sortParam: "cSort_0",
            ties: ["n.DocumentId", "n.VersionId"],
            tieParams: ["cTie_1", "cTie_2"]);
        StringAssert.Contains(clause, "datetime($cSort_0)");
    }

    [TestMethod]
    public void ApplyAfter_RejectsLegacyUnprefixedCursor()
    {
        var clauses = new CypherClauses();
        var cursor = StoreCursor.Encode("", "doc-1", Guid.Empty.ToString());
        var ex = Assert.ThrowsExactly<GraphStoreException>(() =>
            StoreKeyset.ApplyAfter(clauses, cursor, 3, "n.FileCreationTimeUtc", "DESC", "n.DocumentId", "n.VersionId"));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void Encode_RoundTripsNullableLayoutAndTies()
    {
        var node = new Node
        {
            VersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            NodeId = "n1",
            X = 99.5f,
        };
        var cursor = StoreKeyset.Encode("n.X", node, node.VersionId.ToString(), node.NodeId);
        var clauses = new CypherClauses();
        StoreKeyset.ApplyAfter(clauses, cursor, 3, "n.X", "ASC", "n.VersionId", "n.NodeId");
        StringAssert.Contains(clauses.WherePrefix(), "n.X > $");
        Assert.IsTrue(clauses.Parameters.Values.OfType<double>().Any(v => Math.Abs(v - 99.5) < 0.0001));
    }
}
