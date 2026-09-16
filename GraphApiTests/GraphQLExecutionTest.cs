using System.Threading.Tasks;
using GraphRoots.GraphApi;
using GraphRoots.GraphDb;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphApiTests;

[TestClass]
public class GraphQLExecutionTest
{
    [TestMethod]
    public async Task Documents_ReturnIdsAndOptionalTotalCount()
    {
        var json = await Execute("{ documents { nodes { documentId versionId origin } pageInfo { endCursor } } }");
        StringAssert.Contains(json, "\"documentId\": \"doc-1\"");
        StringAssert.Contains(json, "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        StringAssert.Contains(json, "GRAPHROOTS");
        Assert.IsFalse(json.Contains("totalCount", System.StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task Documents_ResolveNestedLibrariesFromParent()
    {
        var store = new FakeGraphStore();
        var json = await Execute("""
            query ListDocuments {
              documents(
                first: 20
                sort: { field: FILE_NAME, direction: ASC }
                filter: { isNested: true }
              ) {
                totalCount
                nodes {
                  id
                  documentId
                  versionId
                  origin
                  committed
                  fileName
                  isNested
                  libraries { id }
                  nestedDocuments { documentId }
                  parentDocuments { documentId }
                  basedOn { documentId }
                  derivedDocuments { documentId }
                  isolatedNodes { nodeId }
                  stats { nodeCount }
                }
                pageInfo {
                  endCursor
                  hasNextPage
                }
              }
            }
            """, store);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        StringAssert.Contains(json, RelayIds.LibraryVersion(store.SampleLibrary));
    }

    [TestMethod]
    public async Task LibraryVersion_RequiresExactAssemblyVersion()
    {
        var store = new FakeGraphStore();
        var json = await Execute("""
            {
              libraryVersion(origin: GRASSHOPPER, libraryId: "lib-1", version: "1.0.0", assemblyVersion: "1.0.0.0") {
                name
                assemblyVersion
              }
            }
            """, store);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        StringAssert.Contains(json, "SampleLib");
        StringAssert.Contains(json, "1.0.0.0");
    }

    [TestMethod]
    public async Task LibraryVersion_UnknownAssemblyLooksUpExactKey()
    {
        var store = new FakeGraphStore();
        var json = await Execute("""
            {
              libraryVersion(origin: GRASSHOPPER, libraryId: "lib-1", version: "unknown", assemblyVersion: "unknown") {
                name
                assemblyVersion
              }
            }
            """, store);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        StringAssert.Contains(json, "UnknownAsm");
        StringAssert.Contains(json, "\"unknown\"");
    }

    [TestMethod]
    public async Task LibraryVersion_OmittingAssemblyVersionIsSchemaError()
    {
        var json = await Execute("""
            {
              libraryVersion(origin: GRASSHOPPER, libraryId: "lib-1", version: "1.0.0") {
                name
              }
            }
            """);
        StringAssert.Contains(json, "\"errors\"");
        Assert.IsFalse(json.Contains("STORE_ERROR", System.StringComparison.Ordinal), json);
        Assert.IsTrue(
            json.Contains("assemblyVersion", System.StringComparison.Ordinal),
            json);
    }

    [TestMethod]
    public async Task Entity_LibraryVersionEmptyAssemblyUsesUnknown()
    {
        var store = new FakeGraphStore();
        var id = RelayIds.LibraryVersion(new LibraryVersion
        {
            Origin = GraphOrigins.Grasshopper,
            LibraryId = "lib-1",
            Version = "unknown",
            AssemblyVersion = "",
        });
        var json = await Execute($@"{{ entity(id: ""{id}"") {{ ... on LibraryVersion {{ name assemblyVersion }} }} }}", store);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        StringAssert.Contains(json, "UnknownAsm");
        StringAssert.Contains(json, "\"unknown\"");
    }

    [TestMethod]
    public async Task Documents_UnknownOriginFilterDoesNotThrow()
    {
        var json = await Execute("{ documents(filter: { origin: UNKNOWN }) { totalCount nodes { origin } } }");
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        Assert.IsFalse(json.Contains("writable origin", System.StringComparison.Ordinal), json);
    }

    [TestMethod]
    public async Task NodeType_UnknownOriginReturnsNull()
    {
        var json = await Execute("{ nodeType(origin: UNKNOWN, typeId: \"t\") { name } }");
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        StringAssert.Contains(json, "\"nodeType\": null");
    }

    [TestMethod]
    public async Task Document_StatsMapKindKeysToGraphqlEnums()
    {
        var json = await Execute("""
            {
              document(documentId: "doc-1", versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") {
                stats { countsByKind { key count } }
              }
            }
            """);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        StringAssert.Contains(json, "OPERATOR");
        StringAssert.Contains(json, "PARAMETER");
        Assert.IsFalse(json.Contains("\"Operator\"", System.StringComparison.Ordinal), json);
        Assert.IsFalse(json.Contains("\"Parameter\"", System.StringComparison.Ordinal), json);
    }

    [TestMethod]
    public async Task Document_NodesRejectConflictingVersionId()
    {
        var json = await Execute("""
            {
              document(documentId: "doc-1", versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") {
                nodes(filter: { versionId: "00000000-0000-0000-0000-000000000000" }) { totalCount }
              }
            }
            """);
        StringAssert.Contains(json, "INVALID_ARGUMENT");
        StringAssert.Contains(json, "versionId");
    }

    [TestMethod]
    public async Task Document_NodesAllowMatchingVersionId()
    {
        var json = await Execute("""
            {
              document(documentId: "doc-1", versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee") {
                nodes(filter: { versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" }) { totalCount }
              }
            }
            """);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
    }

    [TestMethod]
    public async Task ApplyGraphPatch_RejectsUnknownOrigin()
    {
        var json = await Execute("""
            mutation {
              applyGraphPatch(input: {
                documentId: "doc-1"
                versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                createNodes: [{ nodeId: "n1", origin: UNKNOWN }]
              }) { createdNodeIds }
            }
            """);
        StringAssert.Contains(json, "INVALID_ARGUMENT");
        StringAssert.Contains(json, "writable origin");
    }

    [TestMethod]
    public async Task MatchSubgraph_UnknownOriginDoesNotThrow()
    {
        var json = await Execute("""
            {
              matchSubgraph(
                pattern: { nodes: [{ key: "n" }] }
                scope: { origin: UNKNOWN }
                first: 1
              ) { totalCount }
            }
            """);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
    }

    [TestMethod]
    public async Task Entity_RefetchesDocument()
    {
        var store = new FakeGraphStore();
        var id = RelayIds.Document(store.Sample);
        var json = await Execute($@"{{ entity(id: ""{id}"") {{ ... on Document {{ documentId extension(key: ""gh.note"") }} }} }}", store);
        StringAssert.Contains(json, "\"documentId\": \"doc-1\"");
        StringAssert.Contains(json, "hello");
    }

    [TestMethod]
    public async Task Entity_RejectsInvalidId()
    {
        var json = await Execute(@"{ entity(id: ""not-a-valid-id"") { id } }");
        StringAssert.Contains(json, "INVALID_ARGUMENT");
    }

    [TestMethod]
    public async Task MatchSubgraph_RequiresLimitWhenUnscoped()
    {
        var json = await Execute("""
            { matchSubgraph(pattern: { nodes: [{ key: "n" }] }) { nodes { document { documentId } } } }
            """);
        StringAssert.Contains(json, "LIMIT_REQUIRED");
    }

    [TestMethod]
    public async Task ApplyGraphPatch_UsesDocumentId()
    {
        var store = new FakeGraphStore();
        var json = await Execute("""
            mutation {
              applyGraphPatch(input: {
                documentId: "doc-1"
                versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                createNodes: [{ nodeId: "n1", origin: GRAPHROOTS }]
              }) { createdNodeIds document { documentId } }
            }
            """, store);
        StringAssert.Contains(json, "\"n1\"");
        StringAssert.Contains(json, "createdNodeIds");
        Assert.IsNotNull(store.LastPatch);
        Assert.AreEqual("doc-1", store.LastPatch!.DocumentId);
    }

    [TestMethod]
    public async Task ImportSnapshot_UsesDocumentId()
    {
        var store = new FakeGraphStore();
        var json = await Execute("""
            mutation {
              importSnapshot(input: {
                documents: [{
                  documentId: "gh-doc"
                  versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                  origin: GRASSHOPPER
                  fileName: "model.ghx"
                }]
                nodes: [{
                  documentId: "gh-doc"
                  versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                  nodeId: "n1"
                  origin: GRASSHOPPER
                  typeId: "t1"
                  kind: OPERATOR
                }]
                nodeTypes: [{ origin: GRASSHOPPER, typeId: "t1", name: "Add" }]
              }) {
                nodeCount
                document { documentId origin }
              }
            }
            """, store);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        StringAssert.Contains(json, "\"nodeCount\": 1");
        StringAssert.Contains(json, "gh-doc");
        Assert.IsNotNull(store.LastSnapshot);
        Assert.AreEqual("gh-doc", store.LastSnapshot!.Documents[0].DocumentId);
        Assert.AreEqual(GraphOrigins.Grasshopper, store.LastSnapshot.Documents[0].Origin);
    }

    [TestMethod]
    public async Task ImportSnapshot_RejectsGraphRootsOrigin()
    {
        var json = await Execute("""
            mutation {
              importSnapshot(input: {
                documents: [{
                  documentId: "doc"
                  versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                  origin: GRAPHROOTS
                }]
              }) { nodeCount }
            }
            """);
        StringAssert.Contains(json, "INVALID_ARGUMENT");
    }

    [TestMethod]
    public async Task CommitDocument_ReturnsSnapshot()
    {
        var store = new FakeGraphStore();
        var json = await Execute("""
            mutation {
              commitDocument(input: {
                documentId: "doc-1"
                versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
              }) {
                document { committed documentId }
                workingDocument { documentId }
              }
            }
            """, store);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        StringAssert.Contains(json, "\"committed\": true");
        StringAssert.Contains(json, "doc-1");
    }

    [TestMethod]
    public async Task MergeDocument_RequiresOtherParents()
    {
        var json = await Execute("""
            mutation {
              mergeDocument(input: {
                documentId: "doc-1"
                versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                otherParents: [{ documentId: "other", versionId: "bbbbbbbb-cccc-dddd-eeee-ffffffffffff" }]
              }) {
                document { committed }
                workingDocument { documentId }
              }
            }
            """);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal), json);
        StringAssert.Contains(json, "\"committed\": true");
    }

    [TestMethod]
    public async Task WirePorts_WrapsPatch()
    {
        var store = new FakeGraphStore();
        var json = await Execute("""
            mutation {
              wirePorts(input: {
                documentId: "doc-1"
                versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                sourcePortId: "a"
                targetPortId: "b"
              }) { document { documentId } }
            }
            """, store);
        Assert.IsFalse(json.Contains("\"errors\"", System.StringComparison.Ordinal));
        Assert.AreEqual("a", store.LastPatch!.CreateEdges![0].SourcePortId);
    }

    [TestMethod]
    public async Task WirePorts_RejectsSelfLoop()
    {
        var json = await Execute("""
            mutation {
              wirePorts(input: {
                documentId: "doc-1"
                versionId: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"
                sourcePortId: "op-out"
                targetPortId: "op-out"
              }) { document { documentId } }
            }
            """);
        StringAssert.Contains(json, "INVALID_ARGUMENT");
    }

    [TestMethod]
    public async Task MatchSubgraph_UnexpectedStoreFailureHasStoreErrorCode()
    {
        var store = new FakeGraphStore { MatchFailure = new System.InvalidOperationException("Invalid input 'WHERE'") };
        var json = await Execute("""
            {
              matchSubgraph(pattern: { nodes: [{ key: "n" }] }, first: 1) { totalCount }
            }
            """, store);
        StringAssert.Contains(json, "STORE_ERROR");
        StringAssert.Contains(json, "WHERE");
    }

    [TestMethod]
    public void RelayIds_RoundTrip()
    {
        var store = new FakeGraphStore();
        var id = RelayIds.Document(store.Sample);
        var (type, parts) = RelayIds.Decode(id);
        Assert.AreEqual("Document", type);
        Assert.AreEqual("doc-1", parts[0]);
        Assert.AreEqual(store.Sample.VersionId.ToString(), parts[1]);
    }

    static async Task<string> Execute(string query, FakeGraphStore? store = null)
    {
        store ??= new FakeGraphStore();
        var services = new ServiceCollection();
        services.AddSingleton<IGraphStore>(store);
        services.AddGraphRootsGraphQL();
        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequiredService<IRequestExecutorResolver>().GetRequestExecutorAsync();
        var result = await executor.ExecuteAsync(query);
        return result.ToJson();
    }
}
