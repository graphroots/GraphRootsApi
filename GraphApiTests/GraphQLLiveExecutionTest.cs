using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using GraphRoots.GraphApi;
using GraphRoots.GraphDb;
using GraphRoots.Grasshopper;
using GraphRoots.Grasshopper.Parser;
using HotChocolate;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Neo4j.Driver;

namespace GraphRoots.GraphApiTests;

[TestClass]
public class GraphQLLiveExecutionTest
{
    static IDriver? Driver;
    static IGraphStore? Store;
    static IDbOperations? Db;
    static ServiceProvider? Services;
    static IRequestExecutor? Executor;

    [ClassInitialize]
    public static async Task ClassInitialize(TestContext _)
    {
        var settings = LiveNeo4j.Require();
        Driver = GraphDatabase.Driver(settings.Uri, AuthTokens.Basic(settings.User, settings.Password));
        Db = new DbOperations(Driver, settings.Database);
        Store = new GraphStore(Db);
        await Db.EnsureSchema();

        var services = new ServiceCollection();
        services.AddSingleton<IGraphStore>(Store!);
        services.AddGraphRootsGraphQL();
        Services = services.BuildServiceProvider();
        Executor = await Services.GetRequiredService<IRequestExecutorResolver>().GetRequestExecutorAsync();
    }

    [TestInitialize]
    public async Task TestInitialize()
    {
        await Db!.PurgeDatabase();
    }

    [TestMethod]
    public async Task MatchSubgraph_TwoUnconstrainedNodes_DefaultInjective()
    {
        var created = await CreateBlank("gql-two");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                createNodes: [
                  {{ nodeId: ""n1"", kind: PARAMETER }}
                  {{ nodeId: ""n2"", kind: OPERATOR }}
                ]
              }}) {{ createdNodeIds }}
            }}");

        var json = await Execute($@"
            {{
              matchSubgraph(
                pattern: {{ nodes: [{{ key: ""a"" }}, {{ key: ""b"" }}] }}
                scope: {{ documentId: ""{created.DocumentId}"", versionId: ""{created.VersionId}"" }}
                first: 10
              ) {{ totalCount }}
            }}");
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        Assert.AreEqual(2, JsonInt(json, "data", "matchSubgraph", "totalCount"));
    }

    [TestMethod]
    public async Task MatchSubgraph_ConnectionDirections_DefaultInjective()
    {
        var created = await CreateBlank("gql-dir");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                createNodes: [
                  {{ nodeId: ""slider"", kind: PARAMETER }}
                  {{ nodeId: ""op"", kind: OPERATOR }}
                ]
                createPorts: [
                  {{ portId: ""out"", nodeId: ""slider"", name: ""Y"", direction: OUT }}
                  {{ portId: ""in"", nodeId: ""op"", name: ""A"", direction: IN }}
                ]
                createEdges: [{{ sourcePortId: ""out"", targetPortId: ""in"" }}]
              }}) {{ createdNodeIds }}
            }}");

        var json = await Execute($@"
            {{
              matchSubgraph(
                pattern: {{
                  nodes: [{{ key: ""src"", kind: PARAMETER }}, {{ key: ""dst"", kind: OPERATOR }}]
                  connections: [{{ sourceNode: ""src"", targetNode: ""dst"", sourceDirection: OUT, targetDirection: IN }}]
                }}
                scope: {{ documentId: ""{created.DocumentId}"", versionId: ""{created.VersionId}"" }}
                first: 10
              ) {{ totalCount }}
            }}");
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        Assert.AreEqual(1, JsonInt(json, "data", "matchSubgraph", "totalCount"));
    }

    [TestMethod]
    public async Task MatchSubgraph_IncludeNested_ParentVersusChild()
    {
        var parent = await CreateBlank("gql-nest-p");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{parent.DocumentId}""
                versionId: ""{parent.VersionId}""
                createNodes: [{{ nodeId: ""parent-n"", kind: OPERATOR }}]
              }}) {{ createdNodeIds }}
            }}");
        var child = await CreateBlank("gql-nest-c");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{child.DocumentId}""
                versionId: ""{child.VersionId}""
                createNodes: [{{ nodeId: ""child-n"", kind: OPERATOR }}]
              }}) {{ createdNodeIds }}
            }}");
        var parentDoc = await Store!.GetDocument(parent.DocumentId, Guid.Parse(parent.VersionId));
        var childDoc = await Store.GetDocument(child.DocumentId, Guid.Parse(child.VersionId));
        await Db!.MergeRelationship(parentDoc!, childDoc!, new Nests());

        var parentOnly = await Execute($@"
            {{
              matchSubgraph(
                pattern: {{ nodes: [{{ key: ""n"", kind: OPERATOR }}] }}
                scope: {{ documentId: ""{parent.DocumentId}"", versionId: ""{parent.VersionId}"", includeNested: false }}
                first: 10
              ) {{ totalCount }}
            }}");
        Assert.IsFalse(parentOnly.Contains("\"errors\"", StringComparison.Ordinal), parentOnly);
        Assert.AreEqual(1, JsonInt(parentOnly, "data", "matchSubgraph", "totalCount"));

        var nested = await Execute($@"
            {{
              matchSubgraph(
                pattern: {{ nodes: [{{ key: ""n"", kind: OPERATOR }}] }}
                scope: {{ documentId: ""{parent.DocumentId}"", versionId: ""{parent.VersionId}"", includeNested: true }}
                first: 10
              ) {{ totalCount }}
            }}");
        Assert.IsFalse(nested.Contains("\"errors\"", StringComparison.Ordinal), nested);
        Assert.AreEqual(2, JsonInt(nested, "data", "matchSubgraph", "totalCount"));
    }

    [TestMethod]
    public async Task ApplyGraphPatch_SharedTypeIdKeepsCatalogName()
    {
        var created = await CreateBlank("gql-type");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                createNodes: [
                  {{ nodeId: ""a"", typeId: ""shared-type"", name: ""Alpha"" }}
                  {{ nodeId: ""b"", typeId: ""shared-type"", name: ""Beta"" }}
                ]
              }}) {{ createdNodeIds }}
            }}");

        var json = await Execute("""
            { nodeType(origin: GRAPHROOTS, typeId: "shared-type") { name } }
            """);
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        Assert.AreEqual("Alpha", JsonString(json, "data", "nodeType", "name"));
    }

    [TestMethod]
    public async Task IsolatedNodes_IncludesNullKind()
    {
        var created = await CreateBlank("gql-iso");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                createNodes: [{{ nodeId: ""gh-like"", name: ""Bare"" }}]
              }}) {{ createdNodeIds }}
            }}");

        var json = await Execute($@"
            {{
              document(documentId: ""{created.DocumentId}"", versionId: ""{created.VersionId}"") {{
                isolatedNodes {{ nodeId }}
              }}
            }}");
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        StringAssert.Contains(json, "gh-like");
    }

    [TestMethod]
    public async Task ApplyGraphPatch_NilVersionId_NotFound()
    {
        var created = await CreateBlank("gql-nil");
        var json = await Execute($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""00000000-0000-0000-0000-000000000000""
                createNodes: [{{ nodeId: ""x"" }}]
              }}) {{ createdNodeIds }}
            }}");
        StringAssert.Contains(json, "NOT_FOUND");
    }

    [TestMethod]
    public async Task AddToGroups_RequiresGroupKind()
    {
        var created = await CreateBlank("gql-group");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                createNodes: [
                  {{ nodeId: ""slider"", kind: PARAMETER }}
                  {{ nodeId: ""note"", kind: ANNOTATION }}
                ]
              }}) {{ createdNodeIds }}
            }}");

        var json = await Execute($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                addToGroups: [{{ memberNodeId: ""note"", groupNodeId: ""slider"" }}]
              }}) {{ createdNodeIds }}
            }}");
        StringAssert.Contains(json, "INVALID_ARGUMENT");
    }

    [TestMethod]
    public async Task WirePorts_RejectsSelfLoop()
    {
        var created = await CreateBlank("gql-loop");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                createNodes: [{{ nodeId: ""op"", kind: OPERATOR }}]
                createPorts: [{{ portId: ""out"", nodeId: ""op"", direction: OUT }}]
              }}) {{ createdNodeIds }}
            }}");

        var json = await Execute($@"
            mutation {{
              wirePorts(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                sourcePortId: ""out""
                targetPortId: ""out""
              }}) {{ createdNodeIds }}
            }}");
        StringAssert.Contains(json, "INVALID_ARGUMENT");
    }

    [TestMethod]
    public async Task LibraryVersion_UnknownAssembly_ExactLookup()
    {
        await Db!.MergeNode(new LibraryVersion
        {
            Origin = GraphOrigins.Grasshopper,
            LibraryId = "gh",
            Version = "unknown",
            AssemblyVersion = "unknown",
            Name = "Grasshopper",
        });

        var json = await Execute("""
            {
              libraryVersion(origin: GRASSHOPPER, libraryId: "gh", version: "unknown", assemblyVersion: "unknown") {
                name
                assemblyVersion
              }
            }
            """);
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        Assert.AreEqual("Grasshopper", JsonString(json, "data", "libraryVersion", "name"));
        Assert.AreEqual("unknown", JsonString(json, "data", "libraryVersion", "assemblyVersion"));
    }

    [TestMethod]
    public async Task ImportSnapshot_ParserExample1()
    {
        var testdata = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata");
        var filePath = System.IO.Path.Combine(testdata, "ParserExample1.ghx");
        var parser = new GhxArchiveParser(new GhxArchiveConverter());
        var context = LoaderContext.FromFile(parser, filePath);
        var snapshot = new GhxSnapshotBuilder().Build(context);
        var input = ImportSnapshotGql.From(snapshot);

        var request = OperationRequestBuilder.New()
            .SetDocument("""
                mutation ImportSnapshot($input: ImportSnapshotInput!) {
                  importSnapshot(input: $input) {
                    nodeCount
                    portCount
                    edgeCount
                    nestedDocumentCount
                    document { documentId versionId origin }
                  }
                }
                """)
            .SetVariableValues(new Dictionary<string, object?> { ["input"] = input })
            .Build();
        var json = (await Executor!.ExecuteAsync(request)).ToJson();
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        Assert.AreEqual(snapshot.Nodes.Count, JsonInt(json, "data", "importSnapshot", "nodeCount"));

        var documentId = JsonString(json, "data", "importSnapshot", "document", "documentId");
        var versionId = JsonString(json, "data", "importSnapshot", "document", "versionId");
        var read = await Apply($@"
            query {{
              document(documentId: ""{documentId}"", versionId: ""{versionId}"") {{
                origin
                stats {{ nodeCount }}
              }}
            }}");
        Assert.AreEqual("GRASSHOPPER", JsonString(read, "data", "document", "origin"));
        Assert.AreEqual(snapshot.Nodes.Count, JsonInt(read, "data", "document", "stats", "nodeCount"));
    }

    async Task<(string DocumentId, string VersionId)> CreateBlank(string documentId)
    {
        var json = await Apply($@"
            mutation {{
              createDocument(input: {{ documentId: ""{documentId}"", fileName: ""{documentId}"" }}) {{
                document {{ documentId versionId }}
              }}
            }}");
        return (
            JsonString(json, "data", "createDocument", "document", "documentId"),
            JsonString(json, "data", "createDocument", "document", "versionId"));
    }

    [TestMethod]
    public async Task VersionGraph_CommitMergeAndBasedOn()
    {
        var created = await CreateBlank("gql-hist");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                createNodes: [{{ nodeId: ""n1"", kind: OPERATOR }}]
              }}) {{ createdNodeIds }}
            }}");

        var commitJson = await Apply($@"
            mutation {{
              commitDocument(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
              }}) {{
                document {{ documentId versionId committed }}
                workingDocument {{ versionId committed }}
              }}
            }}");
        Assert.AreEqual("true", JsonBoolString(commitJson, "data", "commitDocument", "document", "committed"));
        Assert.AreEqual("false", JsonBoolString(commitJson, "data", "commitDocument", "workingDocument", "committed"));
        var commitVersion = JsonString(commitJson, "data", "commitDocument", "document", "versionId");

        var query = await Apply($@"
            query {{
              document(documentId: ""{created.DocumentId}"", versionId: ""{created.VersionId}"") {{
                committed
                basedOn {{ versionId }}
              }}
            }}");
        Assert.AreEqual("false", JsonBoolString(query, "data", "document", "committed"));
        Assert.AreEqual(commitVersion, JsonStringAt(query, "data", "document", "basedOn", 0, "versionId"));

        var branch = await CreateBlank("gql-hist-b");
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{branch.DocumentId}""
                versionId: ""{branch.VersionId}""
                createNodes: [{{ nodeId: ""b1"", kind: PARAMETER }}]
              }}) {{ createdNodeIds }}
            }}");
        var branchCommitJson = await Apply($@"
            mutation {{
              commitDocument(input: {{
                documentId: ""{branch.DocumentId}""
                versionId: ""{branch.VersionId}""
              }}) {{
                document {{ documentId versionId }}
              }}
            }}");
        var branchCommitVersion = JsonString(branchCommitJson, "data", "commitDocument", "document", "versionId");

        var mergeJson = await Apply($@"
            mutation {{
              mergeDocument(input: {{
                documentId: ""{created.DocumentId}""
                versionId: ""{created.VersionId}""
                otherParents: [{{ documentId: ""{branch.DocumentId}"", versionId: ""{branchCommitVersion}"" }}]
              }}) {{
                document {{ versionId committed }}
              }}
            }}");
        var mergeVersion = JsonString(mergeJson, "data", "mergeDocument", "document", "versionId");
        var mergeQuery = await Apply($@"
            query {{
              document(documentId: ""{created.DocumentId}"", versionId: ""{mergeVersion}"") {{
                committed
                basedOn {{ versionId }}
                historyAncestors(minHops: 1, maxHops: 8) {{ versionId }}
              }}
            }}");
        Assert.AreEqual("true", JsonBoolString(mergeQuery, "data", "document", "committed"));
        Assert.AreEqual(2, JsonArrayLength(mergeQuery, "data", "document", "basedOn"));
        Assert.IsTrue(JsonArrayLength(mergeQuery, "data", "document", "historyAncestors") >= 2);
    }

    async Task<string> Apply(string query)
    {
        var json = await Execute(query);
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        return json;
    }

    static async Task<string> Execute(string query)
    {
        var result = await Executor!.ExecuteAsync(query);
        return result.ToJson();
    }

    static string JsonString(string json, params string[] path)
    {
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        foreach (var name in path)
            el = el.GetProperty(name);
        return el.GetString() ?? throw new AssertFailedException($"null at {string.Join('.', path)} in {json}");
    }

    static int JsonInt(string json, params string[] path)
    {
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        foreach (var name in path)
            el = el.GetProperty(name);
        return el.GetInt32();
    }

    static string JsonBoolString(string json, params string[] path)
    {
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        foreach (var name in path)
            el = el.GetProperty(name);
        return el.GetBoolean() ? "true" : "false";
    }

    static string JsonStringAt(string json, params object[] path)
    {
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        foreach (var part in path)
        {
            el = part switch
            {
                string name => el.GetProperty(name),
                int index => el[index],
                _ => throw new AssertFailedException($"Unsupported path part {part}"),
            };
        }
        return el.GetString() ?? throw new AssertFailedException($"null in {json}");
    }

    static int JsonArrayLength(string json, params string[] path)
    {
        using var doc = JsonDocument.Parse(json);
        var el = doc.RootElement;
        foreach (var name in path)
            el = el.GetProperty(name);
        return el.GetArrayLength();
    }
}
