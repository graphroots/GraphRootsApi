using System.Collections.Generic;
using GraphRoots.GraphDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests;

[TestClass]
public class GraphPatchValidatorTest
{
    static GraphPatch Base() => new()
    {
        DocumentId = "doc",
        VersionId = System.Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
    };

    [TestMethod]
    public void RejectsMissingDocumentId()
    {
        var ex = Assert.ThrowsExactly<GraphStoreException>(() =>
            GraphPatchValidator.Validate(new GraphPatch { VersionId = Base().VersionId, DeleteNodes = ["n"] }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void RejectsCreateAndDeleteOverlap()
    {
        var patch = Base();
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => GraphPatchValidator.Validate(new GraphPatch
        {
            DocumentId = patch.DocumentId,
            VersionId = patch.VersionId,
            CreateNodes = [new CreateNodeOp { NodeId = "n1" }],
            DeleteNodes = ["n1"],
        }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void RejectsSelfGroupMembership()
    {
        var patch = Base();
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => GraphPatchValidator.Validate(new GraphPatch
        {
            DocumentId = patch.DocumentId,
            VersionId = patch.VersionId,
            AddToGroups = [new GroupMembershipOp { MemberNodeId = "g", GroupNodeId = "g" }],
        }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void RejectsHostileExtensionKey()
    {
        var patch = Base();
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => GraphPatchValidator.Validate(new GraphPatch
        {
            DocumentId = patch.DocumentId,
            VersionId = patch.VersionId,
            SetExtensions =
            [
                new SetExtensionsOp
                {
                    Entity = StoreEntityKind.Node,
                    Id = "n1",
                    Entries = [new ExtensionPredicate { Key = "Name", EqualsValue = "x" }],
                },
            ],
        }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void RejectsOversizedPatch()
    {
        var deletes = new List<string>();
        for (var i = 0; i < GraphPatchValidator.MaxOperations + 1; i++)
            deletes.Add($"n{i}");
        var patch = Base();
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => GraphPatchValidator.Validate(new GraphPatch
        {
            DocumentId = patch.DocumentId,
            VersionId = patch.VersionId,
            DeleteNodes = deletes,
        }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void AcceptsValidPatch()
    {
        GraphPatchValidator.Validate(new GraphPatch
        {
            DocumentId = "doc",
            VersionId = Base().VersionId,
            CreateNodes = [new CreateNodeOp { NodeId = "n1" }],
            CreatePorts = [new CreatePortOp { PortId = "p1", NodeId = "n1" }],
            SetExtensions =
            [
                new SetExtensionsOp
                {
                    Entity = StoreEntityKind.Node,
                    Id = "n1",
                    Entries = [new ExtensionPredicate { Key = "gh.note", EqualsValue = "ok" }],
                },
            ],
        });
    }

    [TestMethod]
    public void RejectsSelfLoopEdge()
    {
        var patch = Base();
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => GraphPatchValidator.Validate(new GraphPatch
        {
            DocumentId = patch.DocumentId,
            VersionId = patch.VersionId,
            CreateEdges = [new CreateEdgeOp { SourcePortId = "out", TargetPortId = "out" }],
        }));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void AcceptsNilVersionId()
    {
        GraphPatchValidator.Validate(new GraphPatch
        {
            DocumentId = "doc",
            VersionId = System.Guid.Empty,
            DeleteNodes = ["n1"],
        });
    }
}
