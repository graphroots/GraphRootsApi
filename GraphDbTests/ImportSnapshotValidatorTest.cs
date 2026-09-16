using System;
using System.Collections.Generic;
using GraphRoots.GraphDb;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphDbTests;

[TestClass]
public class ImportSnapshotValidatorTest
{
    static readonly Guid Version = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    static ImportSnapshot Minimal() => new()
    {
        Documents =
        [
            new ImportDocument
            {
                DocumentId = "doc",
                VersionId = Version,
                Origin = GraphOrigins.Grasshopper,
                IsNested = false,
            },
        ],
        Nodes =
        [
            new ImportNode
            {
                DocumentId = "doc",
                VersionId = Version,
                NodeId = "n1",
                Origin = GraphOrigins.Grasshopper,
                TypeId = "t1",
                Kind = NodeKinds.Operator,
            },
        ],
        NodeTypes =
        [
            new ImportNodeType { Origin = GraphOrigins.Grasshopper, TypeId = "t1", Name = "Type" },
        ],
    };

    [TestMethod]
    public void AcceptsMinimalSnapshot()
    {
        ImportSnapshotValidator.Validate(Minimal());
    }

    [TestMethod]
    public void RejectsMissingRoot()
    {
        var snapshot = new ImportSnapshot
        {
            Documents =
            [
                new ImportDocument
                {
                    DocumentId = "nested",
                    VersionId = Version,
                    Origin = GraphOrigins.Grasshopper,
                    IsNested = true,
                },
            ],
        };
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => ImportSnapshotValidator.Validate(snapshot));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void RejectsGraphRootsOrigin()
    {
        var snapshot = Minimal();
        snapshot = new ImportSnapshot
        {
            Documents =
            [
                new ImportDocument
                {
                    DocumentId = "doc",
                    VersionId = Version,
                    Origin = GraphOrigins.GraphRoots,
                    IsNested = false,
                },
            ],
            Nodes = snapshot.Nodes,
            NodeTypes = snapshot.NodeTypes,
        };
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => ImportSnapshotValidator.Validate(snapshot));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void RejectsBrokenEdge()
    {
        var snapshot = Minimal();
        snapshot = new ImportSnapshot
        {
            Documents = snapshot.Documents,
            Nodes = snapshot.Nodes,
            NodeTypes = snapshot.NodeTypes,
            Edges =
            [
                new ImportEdge
                {
                    DocumentId = "doc",
                    VersionId = Version,
                    SourcePortId = "missing",
                    TargetPortId = "also-missing",
                },
            ],
        };
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => ImportSnapshotValidator.Validate(snapshot));
        Assert.AreEqual(GraphStoreException.Codes.InvalidArgument, ex.Code);
    }

    [TestMethod]
    public void RejectsOversizedSnapshot()
    {
        var nodes = new List<ImportNode>();
        for (var i = 0; i < GraphStore.MaxDenseGraphEntities + 1; i++)
        {
            nodes.Add(new ImportNode
            {
                DocumentId = "doc",
                VersionId = Version,
                NodeId = $"n{i}",
                Origin = GraphOrigins.Grasshopper,
                TypeId = "t1",
            });
        }
        var snapshot = new ImportSnapshot
        {
            Documents = Minimal().Documents,
            Nodes = nodes,
            NodeTypes = Minimal().NodeTypes,
        };
        var ex = Assert.ThrowsExactly<GraphStoreException>(() => ImportSnapshotValidator.Validate(snapshot));
        Assert.AreEqual(GraphStoreException.Codes.ResultTooLarge, ex.Code);
    }
}
