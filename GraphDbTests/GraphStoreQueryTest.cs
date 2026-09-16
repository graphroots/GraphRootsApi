using System;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests;

[TestClass]
public class GraphStoreQueryTest
{
    [TestMethod]
    public async Task GetLibraryVersion_WithoutAssemblyVersion_MatchesAnyAssembly()
    {
        var db = new RecordingDbOperations();
        var store = new GraphStore(db);
        await store.GetLibraryVersion(GraphOrigins.Grasshopper, "lib-1", "1.0.0", null);
        Assert.AreEqual(1, db.Queries.Count);
        StringAssert.Contains(db.Queries[0], "ORDER BY n.AssemblyVersion ASC");
        StringAssert.Contains(db.Queries[0], "LIMIT 1");
        Assert.IsFalse(db.Queries[0].Contains("AssemblyVersion:", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetIsolatedNodes_ExcludesGroupAnnotationCluster()
    {
        var db = new RecordingDbOperations();
        var store = new GraphStore(db);
        await store.GetIsolatedNodes("doc", Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        Assert.AreEqual(1, db.Queries.Count);
        StringAssert.Contains(db.Queries[0], $"'{NodeKinds.Group}'");
        StringAssert.Contains(db.Queries[0], $"'{NodeKinds.Annotation}'");
        StringAssert.Contains(db.Queries[0], $"'{NodeKinds.Cluster}'");
        StringAssert.Contains(db.Queries[0], "n.Kind IS NULL OR NOT n.Kind IN");
    }

    [TestMethod]
    public async Task ListDocuments_UnknownOrigin_ExcludesKnownOrigins()
    {
        var db = new RecordingDbOperations();
        var store = new GraphStore(db);
        await store.ListDocuments(
            new DocumentFilter { Origin = GraphOrigins.UnknownFilter },
            new PageRequest { First = 1, IncludeTotalCount = false });
        Assert.AreEqual(1, db.Queries.Count);
        StringAssert.Contains(db.Queries[0], "NOT coalesce(n.Origin, '') IN");
        Assert.IsFalse(db.Queries[0].Contains(GraphOrigins.UnknownFilter, StringComparison.Ordinal));
    }
}
