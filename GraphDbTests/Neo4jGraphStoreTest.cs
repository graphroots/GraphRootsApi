using System;
using System.Linq;
using System.Threading.Tasks;
using GraphRoots.GraphDb;
using GraphRoots.GraphStoreContractTests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests;

/// <summary>
/// Neo4j-only behaviour: data written outside the store contract (older schemas, direct Cypher).
/// Backend-agnostic behaviour belongs in <see cref="GraphStoreContractTestBase"/>.
/// </summary>
[TestClass]
public sealed class Neo4jGraphStoreTest
{
    static Neo4jTestBackend? Backend;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _) => Backend = await LiveNeo4j.Start();

    [ClassCleanup]
    public static async Task ClassCleanup()
    {
        if (Backend != null)
            await Backend.DisposeAsync();
    }

    [TestInitialize]
    public Task TestInitialize() => Backend!.Admin.Purge();

    [TestMethod]
    public async Task ListDocuments_IsNestedFalseIncludesMissingProperty()
    {
        var db = Backend!.Db;
        await db.CreateNode(new Document
        {
            DocumentId = "nested",
            VersionId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Origin = GraphOrigins.Grasshopper,
            IsNested = true,
        });
        await db.CreateNode(new Document
        {
            DocumentId = "unset",
            VersionId = Guid.Parse("bbbbbbbb-cccc-dddd-eeee-ffffffffffff"),
            Origin = GraphOrigins.Grasshopper,
        });
        await db.CreateNode(new Document
        {
            DocumentId = "top",
            VersionId = Guid.Parse("cccccccc-dddd-eeee-ffff-000000000000"),
            Origin = GraphOrigins.Grasshopper,
            IsNested = false,
        });

        var store = Backend.Store;
        var nested = await store.ListDocuments(new DocumentFilter { IsNested = true }, new PageRequest { First = 10, IncludeTotalCount = true });
        Assert.AreEqual(1, nested.TotalCount);
        Assert.AreEqual("nested", nested.Items[0].DocumentId);

        var top = await store.ListDocuments(new DocumentFilter { IsNested = false }, new PageRequest { First = 10, IncludeTotalCount = true });
        Assert.AreEqual(2, top.TotalCount);
        CollectionAssert.AreEquivalent(new[] { "unset", "top" }, top.Items.Select(d => d.DocumentId).ToArray());
    }
}
