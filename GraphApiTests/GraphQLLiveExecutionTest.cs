using System;
using System.Collections.Generic;
using System.Linq;
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

    [TestMethod]
    public async Task ImportSnapshot_StateHashRoundTripsAndFlagsOnlyTheEditedSlider()
    {
        const string sliderValue = "<item name=\"Value\" type_name=\"gh_double\" type_code=\"6\">0.25</item>";
        var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata", "ParserExample3_CanvasGroup.ghx");
        var original = System.IO.File.ReadAllText(filePath);
        var edited = original.Replace(sliderValue, sliderValue.Replace(">0.25<", ">0.75<", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.AreNotEqual(original, edited);

        var before = await ImportAndReadStateHashes(original, filePath);
        var after = await ImportAndReadStateHashes(edited, filePath);

        CollectionAssert.AreEquivalent(before.Keys.ToList(), after.Keys.ToList());
        Assert.IsTrue(before.Values.All(n => n.Hash.Length == 64), "every stored node carries gh.stateHash");
        var changed = before.Keys.Where(id => before[id].Hash != after[id].Hash).ToList();
        Assert.AreEqual(1, changed.Count);
        Assert.AreEqual("Number Slider", after[changed[0]].Name);
    }

    [TestMethod]
    public async Task CompareDocuments_TwoImports_ReportsStateAndNickNameChanges()
    {
        const string sliderValue = "<item name=\"Value\" type_name=\"gh_double\" type_code=\"6\">0.25</item>";
        const string nickName = "<item name=\"NickName\" type_name=\"gh_string\" type_code=\"10\">SphXY</item>";
        var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata", "ParserExample3_CanvasGroup.ghx");
        var original = System.IO.File.ReadAllText(filePath);
        var edited = original
            .Replace(sliderValue, sliderValue.Replace(">0.25<", ">0.75<", StringComparison.Ordinal), StringComparison.Ordinal)
            .Replace(nickName, nickName.Replace("SphXY", "Sphere", StringComparison.Ordinal), StringComparison.Ordinal);
        var before = await ImportXml(original, filePath);
        var after = await ImportXml(edited, filePath);

        var json = await Execute(CompareQuery(before, after));
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        using var doc = JsonDocument.Parse(json);
        var comparison = doc.RootElement.GetProperty("data").GetProperty("compareDocuments");
        Assert.IsTrue(comparison.GetProperty("hasChanges").GetBoolean());
        foreach (var list in new[] { "addedNodes", "removedNodes", "addedPorts", "removedPorts", "addedEdges", "removedEdges" })
            Assert.AreEqual(0, comparison.GetProperty(list).GetArrayLength(), list);

        var changes = comparison.GetProperty("changedNodes").EnumerateArray().ToDictionary(
            c => c.GetProperty("after").GetProperty("name").GetString()!,
            c => c.GetProperty("changes").EnumerateArray()
                .Select(f => $"{f.GetProperty("kind").GetString()}:{f.GetProperty("key").GetString()}:{f.GetProperty("before")}->{f.GetProperty("after")}")
                .ToList());
        Assert.AreEqual(2, changes.Count, json);
        Assert.AreEqual(1, changes["Number Slider"].Count, json);
        StringAssert.StartsWith(changes["Number Slider"][0], "EXTENSION:gh.stateHash:");
        CollectionAssert.AreEqual(new[] { "NICK_NAME::SphXY->Sphere" }, changes.Single(c => c.Key != "Number Slider").Value, json);

        var same = await Execute(CompareQuery(before, before));
        Assert.IsFalse(same.Contains("\"errors\"", StringComparison.Ordinal), same);
        Assert.IsFalse(JsonBool(same, "data", "compareDocuments", "hasChanges"));
    }

    [TestMethod]
    public async Task CompareDocuments_ForkWithPatch_ReportsAddedAndRemovedNodes()
    {
        var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata", "ParserExample1.ghx");
        var baseKey = await ImportXml(System.IO.File.ReadAllText(filePath), filePath);
        var graph = await Execute($@"{{ document(documentId: ""{baseKey.DocumentId}"", versionId: ""{baseKey.VersionId}"") {{ graph {{ nodes {{ nodeId }} edges {{ sourcePortId }} }} }} }}");
        var deletedNodeId = JsonStringAt(graph, "data", "document", "graph", "nodes", 0, "nodeId");

        var fork = await Apply($@"
            mutation {{
              forkDocument(input: {{ documentId: ""{baseKey.DocumentId}"", versionId: ""{baseKey.VersionId}"" }}) {{
                document {{ documentId versionId }}
              }}
            }}");
        var forkKey = (
            JsonString(fork, "data", "forkDocument", "document", "documentId"),
            JsonString(fork, "data", "forkDocument", "document", "versionId"));
        await Apply($@"
            mutation {{
              applyGraphPatch(input: {{
                documentId: ""{forkKey.Item1}""
                versionId: ""{forkKey.Item2}""
                createNodes: [{{ nodeId: ""added-in-fork"", kind: PARAMETER }}]
                deleteNodes: [""{deletedNodeId}""]
              }}) {{ createdNodeIds }}
            }}");

        var json = await Execute(CompareQuery(baseKey, forkKey));
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        Assert.IsTrue(JsonBool(json, "data", "compareDocuments", "hasChanges"));
        Assert.AreEqual("added-in-fork", JsonStringAt(json, "data", "compareDocuments", "addedNodes", 0, "nodeId"));
        Assert.AreEqual(deletedNodeId, JsonStringAt(json, "data", "compareDocuments", "removedNodes", 0, "nodeId"));
        using var doc = JsonDocument.Parse(json);
        var comparison = doc.RootElement.GetProperty("data").GetProperty("compareDocuments");
        Assert.AreEqual(1, comparison.GetProperty("addedNodes").GetArrayLength(), json);
        Assert.AreEqual(1, comparison.GetProperty("removedNodes").GetArrayLength(), json);
        Assert.AreEqual(0, comparison.GetProperty("changedNodes").GetArrayLength(), json);
    }

    [TestMethod]
    public async Task CompareDocuments_UnknownVersion_IsNotFound()
    {
        var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata", "ParserExample1.ghx");
        var existing = await ImportXml(System.IO.File.ReadAllText(filePath), filePath);
        var json = await Execute(CompareQuery(existing, (existing.DocumentId, Guid.NewGuid().ToString())));
        StringAssert.Contains(json, "NOT_FOUND");
    }

    [TestMethod]
    public async Task CompareDocuments_SelectionOnlyDifference_HasNoChanges()
    {
        const string selected = "<item name=\"Selected\" type_name=\"gh_bool\" type_code=\"1\">true</item>";
        var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata", "ParserExample2_VariableInputs.ghx");
        var original = System.IO.File.ReadAllText(filePath);
        Assert.IsTrue(original.Contains(selected, StringComparison.Ordinal));
        var deselected = original.Replace(selected, selected.Replace(">true<", ">false<", StringComparison.Ordinal), StringComparison.Ordinal);
        var before = await ImportXml(original, filePath);
        var after = await ImportXml(deselected, filePath);
        Assert.AreNotEqual(before.VersionId, after.VersionId, "different bytes give different versions");

        var json = await Execute(CompareQuery(before, after));
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        Assert.IsFalse(JsonBool(json, "data", "compareDocuments", "hasChanges"), json);
    }

    [TestMethod]
    public async Task CompareDocuments_SameVersionTwice_HasNoChanges()
    {
        var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata", "ParserExample1.ghx");
        var key = await ImportXml(System.IO.File.ReadAllText(filePath), filePath);
        var json = await Execute(CompareQuery(key, key));
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        Assert.IsFalse(JsonBool(json, "data", "compareDocuments", "hasChanges"), json);
        using var doc = JsonDocument.Parse(json);
        var comparison = doc.RootElement.GetProperty("data").GetProperty("compareDocuments");
        foreach (var list in new[] { "addedNodes", "removedNodes", "changedNodes", "addedPorts", "removedPorts", "addedEdges", "removedEdges" })
            Assert.AreEqual(0, comparison.GetProperty(list).GetArrayLength(), list);
    }

    [TestMethod]
    public async Task CompareDocuments_SameUnknownVersionTwice_IsNotFound()
    {
        var key = ("no-such-document", Guid.NewGuid().ToString());
        var json = await Execute(CompareQuery(key, key));
        StringAssert.Contains(json, "NOT_FOUND");
    }

    [TestMethod]
    public async Task CompareDocuments_UnknownBeforeVersion_IsNotFound()
    {
        var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata", "ParserExample1.ghx");
        var existing = await ImportXml(System.IO.File.ReadAllText(filePath), filePath);
        var json = await Execute(CompareQuery((existing.DocumentId, Guid.NewGuid().ToString()), existing));
        StringAssert.Contains(json, "NOT_FOUND");
    }

    [TestMethod]
    public async Task CompareDocuments_PortRename_IsReportedOnTheOwningNode()
    {
        const string portName = "<item name=\"Name\" type_name=\"gh_string\" type_code=\"10\">Tip</item>";
        var filePath = System.IO.Path.Combine(AppContext.BaseDirectory, "testdata", "ParserExample3_CanvasGroup.ghx");
        var original = System.IO.File.ReadAllText(filePath);
        Assert.IsTrue(original.Contains(portName, StringComparison.Ordinal));
        var renamed = original.Replace(portName, portName.Replace(">Tip<", ">Apex<", StringComparison.Ordinal), StringComparison.Ordinal);
        var before = await ImportXml(original, filePath);
        var after = await ImportXml(renamed, filePath);

        var json = await Execute(CompareQuery(before, after));
        Assert.IsFalse(json.Contains("\"errors\"", StringComparison.Ordinal), json);
        Assert.IsTrue(JsonBool(json, "data", "compareDocuments", "hasChanges"), json);
        using var doc = JsonDocument.Parse(json);
        var changed = doc.RootElement.GetProperty("data").GetProperty("compareDocuments").GetProperty("changedNodes");
        Assert.AreEqual(1, changed.GetArrayLength(), json);
        var change = changed[0].GetProperty("changes").EnumerateArray().Single();
        Assert.AreEqual("PORT_NAME", change.GetProperty("kind").GetString(), json);
        Assert.IsFalse(string.IsNullOrEmpty(change.GetProperty("key").GetString()), json);
        Assert.AreEqual("Tip", change.GetProperty("before").GetString(), json);
        Assert.AreEqual("Apex", change.GetProperty("after").GetString(), json);
    }

    static string CompareQuery((string DocumentId, string VersionId) before, (string DocumentId, string VersionId) after) => $@"
        {{
          compareDocuments(
            before: {{ documentId: ""{before.DocumentId}"", versionId: ""{before.VersionId}"" }}
            after: {{ documentId: ""{after.DocumentId}"", versionId: ""{after.VersionId}"" }}
          ) {{
            hasChanges
            addedNodes {{ nodeId }}
            removedNodes {{ nodeId }}
            changedNodes {{ after {{ nodeId name }} changes {{ kind key before after }} }}
            addedPorts {{ portId }}
            removedPorts {{ portId }}
            addedEdges {{ sourcePortId targetPortId }}
            removedEdges {{ sourcePortId targetPortId }}
          }}
        }}";

    async Task<(string DocumentId, string VersionId)> ImportXml(string xml, string filePath)
    {
        var parser = new GhxArchiveParser(new GhxArchiveConverter());
        var context = new LoaderContext(parser.ParseArchiveString(xml), filePath, System.Text.Encoding.UTF8.GetBytes(xml));
        var request = OperationRequestBuilder.New()
            .SetDocument("""
                mutation ImportSnapshot($input: ImportSnapshotInput!) {
                  importSnapshot(input: $input) { document { documentId versionId } }
                }
                """)
            .SetVariableValues(new Dictionary<string, object?> { ["input"] = ImportSnapshotGql.From(new GhxSnapshotBuilder().Build(context)) })
            .Build();
        var imported = (await Executor!.ExecuteAsync(request)).ToJson();
        Assert.IsFalse(imported.Contains("\"errors\"", StringComparison.Ordinal), imported);
        return (
            JsonString(imported, "data", "importSnapshot", "document", "documentId"),
            JsonString(imported, "data", "importSnapshot", "document", "versionId"));
    }

    async Task<Dictionary<string, (string? Name, string Hash)>> ImportAndReadStateHashes(string xml, string filePath)
    {
        var (documentId, versionId) = await ImportXml(xml, filePath);
        var read = await Execute($@"
            {{
              document(documentId: ""{documentId}"", versionId: ""{versionId}"") {{
                graph {{ nodes {{ nodeId name stateHash: extension(key: ""gh.stateHash"") }} }}
              }}
            }}");
        Assert.IsFalse(read.Contains("\"errors\"", StringComparison.Ordinal), read);
        using var doc = JsonDocument.Parse(read);
        return doc.RootElement.GetProperty("data").GetProperty("document").GetProperty("graph").GetProperty("nodes")
            .EnumerateArray()
            .ToDictionary(
                n => n.GetProperty("nodeId").GetString()!,
                n => (n.GetProperty("name").GetString(), n.GetProperty("stateHash").GetString() ?? ""));
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

    static bool JsonBool(string json, params string[] path) => JsonBoolString(json, path) == "true";

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
