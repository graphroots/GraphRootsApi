using System;
using System.Collections.Generic;
using GraphRoots.GraphDb;

namespace GraphRoots.GraphDbCli
{
    static class GraphQlSnapshotEncoder
    {
        public static Dictionary<string, object?> Encode(ImportSnapshot snapshot)
        {
            return new Dictionary<string, object?>
            {
                ["documents"] = EncodeList(snapshot.Documents, EncodeDocument),
                ["nodes"] = EncodeList(snapshot.Nodes, EncodeNode),
                ["ports"] = EncodeList(snapshot.Ports, EncodePort),
                ["edges"] = EncodeList(snapshot.Edges, EncodeEdge),
                ["groupMemberships"] = EncodeList(snapshot.GroupMemberships, EncodeMembership),
                ["nests"] = EncodeList(snapshot.Nests, EncodeNest),
                ["libraries"] = EncodeList(snapshot.Libraries, EncodeLibrary),
                ["libraryVersions"] = EncodeList(snapshot.LibraryVersions, EncodeLibraryVersion),
                ["nodeTypes"] = EncodeList(snapshot.NodeTypes, EncodeNodeType),
                ["usedBy"] = EncodeList(snapshot.UsedBy, EncodeUsedBy),
            };
        }

        static List<Dictionary<string, object?>> EncodeList<T>(IReadOnlyList<T> items, Func<T, Dictionary<string, object?>> map)
        {
            var list = new List<Dictionary<string, object?>>(items.Count);
            foreach (var item in items)
                list.Add(map(item));
            return list;
        }

        static Dictionary<string, object?> EncodeDocument(ImportDocument document)
        {
            var map = new Dictionary<string, object?>
            {
                ["documentId"] = document.DocumentId,
                ["versionId"] = document.VersionId.ToString(),
                ["origin"] = Origin(document.Origin),
            };
            Set(map, "fileName", document.FileName);
            Set(map, "filePath", document.FilePath);
            if (document.IsNested.HasValue)
                map["isNested"] = document.IsNested.Value;
            if (document.FileCreationTimeUtc.HasValue)
                map["fileCreationTimeUtc"] = document.FileCreationTimeUtc.Value.ToUniversalTime().ToString("o");
            if (document.FileLastWriteTimeUtc.HasValue)
                map["fileLastWriteTimeUtc"] = document.FileLastWriteTimeUtc.Value.ToUniversalTime().ToString("o");
            SetExtensions(map, document.Extensions);
            return map;
        }

        static Dictionary<string, object?> EncodeNode(ImportNode node)
        {
            var map = new Dictionary<string, object?>
            {
                ["documentId"] = node.DocumentId,
                ["versionId"] = node.VersionId.ToString(),
                ["nodeId"] = node.NodeId,
                ["origin"] = Origin(node.Origin),
            };
            Set(map, "typeId", node.TypeId);
            Set(map, "name", node.Name);
            Set(map, "nickName", node.NickName);
            if (!string.IsNullOrEmpty(node.Kind))
                map["kind"] = Kind(node.Kind);
            if (node.Locked.HasValue)
                map["locked"] = node.Locked.Value;
            if (node.X.HasValue)
                map["x"] = node.X.Value;
            if (node.Y.HasValue)
                map["y"] = node.Y.Value;
            Set(map, "source", node.Source);
            Set(map, "language", node.Language);
            Set(map, "text", node.Text);
            SetExtensions(map, node.Extensions);
            return map;
        }

        static Dictionary<string, object?> EncodePort(ImportPort port)
        {
            var map = new Dictionary<string, object?>
            {
                ["documentId"] = port.DocumentId,
                ["versionId"] = port.VersionId.ToString(),
                ["nodeId"] = port.NodeId,
                ["portId"] = port.PortId,
            };
            Set(map, "name", port.Name);
            if (!string.IsNullOrEmpty(port.Direction))
                map["direction"] = Direction(port.Direction);
            Set(map, "access", port.Access);
            SetExtensions(map, port.Extensions);
            return map;
        }

        static Dictionary<string, object?> EncodeEdge(ImportEdge edge)
        {
            var map = new Dictionary<string, object?>
            {
                ["documentId"] = edge.DocumentId,
                ["versionId"] = edge.VersionId.ToString(),
                ["sourcePortId"] = edge.SourcePortId,
                ["targetPortId"] = edge.TargetPortId,
            };
            Set(map, "sourceName", edge.SourceName);
            Set(map, "targetName", edge.TargetName);
            SetExtensions(map, edge.Extensions);
            return map;
        }

        static Dictionary<string, object?> EncodeMembership(ImportGroupMembership membership) => new()
        {
            ["versionId"] = membership.VersionId.ToString(),
            ["memberNodeId"] = membership.MemberNodeId,
            ["groupNodeId"] = membership.GroupNodeId,
        };

        static Dictionary<string, object?> EncodeNest(ImportNest nest) => new()
        {
            ["parentDocumentId"] = nest.ParentDocumentId,
            ["parentVersionId"] = nest.ParentVersionId.ToString(),
            ["childDocumentId"] = nest.ChildDocumentId,
            ["childVersionId"] = nest.ChildVersionId.ToString(),
        };

        static Dictionary<string, object?> EncodeLibrary(ImportLibrary library)
        {
            var map = new Dictionary<string, object?>
            {
                ["origin"] = Origin(library.Origin),
                ["libraryId"] = library.LibraryId,
            };
            Set(map, "name", library.Name);
            Set(map, "author", library.Author);
            SetExtensions(map, library.Extensions);
            return map;
        }

        static Dictionary<string, object?> EncodeLibraryVersion(ImportLibraryVersion version)
        {
            var map = new Dictionary<string, object?>
            {
                ["origin"] = Origin(version.Origin),
                ["libraryId"] = version.LibraryId,
                ["version"] = version.Version,
            };
            Set(map, "assemblyVersion", version.AssemblyVersion);
            Set(map, "name", version.Name);
            Set(map, "author", version.Author);
            SetExtensions(map, version.Extensions);
            return map;
        }

        static Dictionary<string, object?> EncodeNodeType(ImportNodeType nodeType)
        {
            var map = new Dictionary<string, object?>
            {
                ["origin"] = Origin(nodeType.Origin),
                ["typeId"] = nodeType.TypeId,
            };
            Set(map, "name", nodeType.Name);
            Set(map, "libraryId", nodeType.LibraryId);
            Set(map, "version", nodeType.Version);
            Set(map, "assemblyVersion", nodeType.AssemblyVersion);
            SetExtensions(map, nodeType.Extensions);
            return map;
        }

        static Dictionary<string, object?> EncodeUsedBy(ImportUsedBy usedBy)
        {
            var map = new Dictionary<string, object?>
            {
                ["origin"] = Origin(usedBy.Origin),
                ["libraryId"] = usedBy.LibraryId,
                ["version"] = usedBy.Version,
                ["documentId"] = usedBy.DocumentId,
                ["versionId"] = usedBy.VersionId.ToString(),
            };
            Set(map, "assemblyVersion", usedBy.AssemblyVersion);
            return map;
        }

        static void SetExtensions(Dictionary<string, object?> map, IDictionary<string, object?>? extensions)
        {
            if (extensions == null || extensions.Count == 0)
                return;
            var list = new List<Dictionary<string, object?>>();
            foreach (var (key, value) in extensions)
            {
                if (value == null)
                    continue;
                list.Add(new Dictionary<string, object?> { ["key"] = key, ["equals"] = value });
            }
            if (list.Count > 0)
                map["extensions"] = list;
        }

        static void Set(Dictionary<string, object?> map, string key, string? value)
        {
            if (!string.IsNullOrEmpty(value))
                map[key] = value;
        }

        static string Origin(string origin) => origin switch
        {
            GraphOrigins.Grasshopper => "GRASSHOPPER",
            GraphOrigins.Dynamo => "DYNAMO",
            GraphOrigins.GraphRoots => "GRAPHROOTS",
            _ => throw new InvalidOperationException($"Unknown origin '{origin}'."),
        };

        static string Kind(string kind) => kind switch
        {
            NodeKinds.Operator => "OPERATOR",
            NodeKinds.Parameter => "PARAMETER",
            NodeKinds.Group => "GROUP",
            NodeKinds.Annotation => "ANNOTATION",
            NodeKinds.Cluster => "CLUSTER",
            _ => throw new InvalidOperationException($"Unknown kind '{kind}'."),
        };

        static string Direction(string direction) => direction switch
        {
            PortDirections.In => "IN",
            PortDirections.Out => "OUT",
            PortDirections.Both => "BOTH",
            _ => throw new InvalidOperationException($"Unknown direction '{direction}'."),
        };
    }
}
