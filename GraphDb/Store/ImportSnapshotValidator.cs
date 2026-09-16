using System;
using System.Collections.Generic;
using System.Linq;

namespace GraphRoots.GraphDb;

public static class ImportSnapshotValidator
{
    public static void Validate(ImportSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var documents = snapshot.Documents ?? [];
        var nodes = snapshot.Nodes ?? [];
        var ports = snapshot.Ports ?? [];
        var edges = snapshot.Edges ?? [];
        var memberships = snapshot.GroupMemberships ?? [];
        var nests = snapshot.Nests ?? [];
        var libraries = snapshot.Libraries ?? [];
        var libraryVersions = snapshot.LibraryVersions ?? [];
        var nodeTypes = snapshot.NodeTypes ?? [];
        var usedBy = snapshot.UsedBy ?? [];

        var entityCount = nodes.Count + ports.Count + edges.Count;
        if (entityCount > GraphStore.MaxDenseGraphEntities)
        {
            throw GraphStoreException.ResultTooLarge(
                $"Import snapshot has {entityCount} nodes, ports, and edges; the limit is {GraphStore.MaxDenseGraphEntities}.");
        }

        var roots = documents.Where(d => d.IsNested != true).ToList();
        if (roots.Count != 1)
            throw GraphStoreException.InvalidArgument("importSnapshot requires exactly one root document (isNested != true).");

        RequireUnique(documents.Select(DocumentKey), "documents");
        RequireUnique(nodes.Select(n => NodeKey(n.VersionId, n.NodeId)), "nodes");
        RequireUnique(ports.Select(p => PortKey(p.VersionId, p.PortId)), "ports");
        RequireUnique(edges.Select(e => EdgeKey(e.VersionId, e.SourcePortId, e.TargetPortId)), "edges");
        RequireUnique(libraries.Select(LibraryKey), "libraries");
        RequireUnique(libraryVersions.Select(LibraryVersionKey), "libraryVersions");
        RequireUnique(nodeTypes.Select(NodeTypeKey), "nodeTypes");

        var documentKeys = new HashSet<string>(documents.Select(DocumentKey), StringComparer.Ordinal);
        var nodeKeys = new HashSet<string>(nodes.Select(n => NodeKey(n.VersionId, n.NodeId)), StringComparer.Ordinal);
        var portKeys = new HashSet<string>(ports.Select(p => PortKey(p.VersionId, p.PortId)), StringComparer.Ordinal);
        var libraryKeys = new HashSet<string>(libraries.Select(LibraryKey), StringComparer.Ordinal);
        var libraryVersionKeys = new HashSet<string>(libraryVersions.Select(LibraryVersionKey), StringComparer.Ordinal);
        var nodeTypeKeys = new HashSet<string>(nodeTypes.Select(NodeTypeKey), StringComparer.Ordinal);

        var nodeByKey = nodes.ToDictionary(n => NodeKey(n.VersionId, n.NodeId), StringComparer.Ordinal);

        foreach (var document in documents)
        {
            RequireId(document.DocumentId, "documents.documentId");
            RequireVersion(document.VersionId, "documents.versionId");
            RequireImportOrigin(document.Origin, "documents.origin");
            ValidateExtensions(document.Extensions, typeof(Document), "documents");
        }

        foreach (var node in nodes)
        {
            RequireId(node.DocumentId, "nodes.documentId");
            RequireVersion(node.VersionId, "nodes.versionId");
            RequireId(node.NodeId, "nodes.nodeId");
            RequireImportOrigin(node.Origin, "nodes.origin");
            RequireId(node.TypeId, "nodes.typeId");
            if (!documentKeys.Contains(DocumentKey(node.DocumentId, node.VersionId)))
                throw GraphStoreException.InvalidArgument($"Node '{node.NodeId}' references unknown document '{node.DocumentId}' version '{node.VersionId}'.");
            if (!string.IsNullOrEmpty(node.Kind) && !IsKnownKind(node.Kind))
                throw GraphStoreException.InvalidArgument($"Unknown node kind '{node.Kind}'.");
            if (!nodeTypeKeys.Contains(NodeTypeKey(node.Origin, node.TypeId!)))
                throw GraphStoreException.InvalidArgument($"Node '{node.NodeId}' references unknown node type '{node.TypeId}'.");
            ValidateExtensions(node.Extensions, typeof(Node), "nodes");
        }

        foreach (var port in ports)
        {
            RequireId(port.DocumentId, "ports.documentId");
            RequireVersion(port.VersionId, "ports.versionId");
            RequireId(port.NodeId, "ports.nodeId");
            RequireId(port.PortId, "ports.portId");
            if (!documentKeys.Contains(DocumentKey(port.DocumentId, port.VersionId)))
                throw GraphStoreException.InvalidArgument($"Port '{port.PortId}' references unknown document '{port.DocumentId}' version '{port.VersionId}'.");
            if (!nodeKeys.Contains(NodeKey(port.VersionId, port.NodeId)))
                throw GraphStoreException.InvalidArgument($"Port '{port.PortId}' references unknown node '{port.NodeId}'.");
            if (!string.IsNullOrEmpty(port.Direction) && !IsKnownDirection(port.Direction))
                throw GraphStoreException.InvalidArgument($"Unknown port direction '{port.Direction}'.");
            ValidateExtensions(port.Extensions, typeof(Port), "ports");
        }

        foreach (var edge in edges)
        {
            RequireId(edge.DocumentId, "edges.documentId");
            RequireVersion(edge.VersionId, "edges.versionId");
            RequireId(edge.SourcePortId, "edges.sourcePortId");
            RequireId(edge.TargetPortId, "edges.targetPortId");
            if (string.Equals(edge.SourcePortId, edge.TargetPortId, StringComparison.Ordinal))
                throw GraphStoreException.InvalidArgument("edges cannot wire a port to itself.");
            if (!documentKeys.Contains(DocumentKey(edge.DocumentId, edge.VersionId)))
                throw GraphStoreException.InvalidArgument($"Edge '{edge.SourcePortId}->{edge.TargetPortId}' references unknown document '{edge.DocumentId}' version '{edge.VersionId}'.");
            if (!portKeys.Contains(PortKey(edge.VersionId, edge.SourcePortId)))
                throw GraphStoreException.InvalidArgument($"Edge source port '{edge.SourcePortId}' was not found.");
            if (!portKeys.Contains(PortKey(edge.VersionId, edge.TargetPortId)))
                throw GraphStoreException.InvalidArgument($"Edge target port '{edge.TargetPortId}' was not found.");
            ValidateExtensions(edge.Extensions, typeof(Edge), "edges");
        }

        foreach (var membership in memberships)
        {
            RequireVersion(membership.VersionId, "groupMemberships.versionId");
            RequireId(membership.MemberNodeId, "groupMemberships.memberNodeId");
            RequireId(membership.GroupNodeId, "groupMemberships.groupNodeId");
            if (string.Equals(membership.MemberNodeId, membership.GroupNodeId, StringComparison.Ordinal))
                throw GraphStoreException.InvalidArgument("A node cannot be a member of itself.");
            if (!nodeKeys.Contains(NodeKey(membership.VersionId, membership.MemberNodeId)))
                throw GraphStoreException.InvalidArgument($"groupMemberships member '{membership.MemberNodeId}' was not found.");
            var groupKey = NodeKey(membership.VersionId, membership.GroupNodeId);
            if (!nodeByKey.TryGetValue(groupKey, out var group))
                throw GraphStoreException.InvalidArgument($"groupMemberships group '{membership.GroupNodeId}' was not found.");
            if (!string.Equals(group.Kind, NodeKinds.Group, StringComparison.Ordinal))
                throw GraphStoreException.InvalidArgument($"groupMemberships group '{membership.GroupNodeId}' must have kind GROUP.");
        }

        foreach (var nest in nests)
        {
            RequireId(nest.ParentDocumentId, "nests.parentDocumentId");
            RequireVersion(nest.ParentVersionId, "nests.parentVersionId");
            RequireId(nest.ChildDocumentId, "nests.childDocumentId");
            RequireVersion(nest.ChildVersionId, "nests.childVersionId");
            if (!documentKeys.Contains(DocumentKey(nest.ParentDocumentId, nest.ParentVersionId)))
                throw GraphStoreException.InvalidArgument($"nests parent '{nest.ParentDocumentId}' version '{nest.ParentVersionId}' was not found.");
            if (!documentKeys.Contains(DocumentKey(nest.ChildDocumentId, nest.ChildVersionId)))
                throw GraphStoreException.InvalidArgument($"nests child '{nest.ChildDocumentId}' version '{nest.ChildVersionId}' was not found.");
        }

        foreach (var library in libraries)
        {
            RequireImportOrigin(library.Origin, "libraries.origin");
            RequireId(library.LibraryId, "libraries.libraryId");
            ValidateExtensions(library.Extensions, typeof(Library), "libraries");
        }

        foreach (var version in libraryVersions)
        {
            RequireImportOrigin(version.Origin, "libraryVersions.origin");
            RequireId(version.LibraryId, "libraryVersions.libraryId");
            RequireId(version.Version, "libraryVersions.version");
            if (!libraryKeys.Contains(LibraryKey(version.Origin, version.LibraryId)))
                throw GraphStoreException.InvalidArgument($"LibraryVersion '{version.LibraryId}' references unknown library.");
            ValidateExtensions(version.Extensions, typeof(LibraryVersion), "libraryVersions");
        }

        foreach (var nodeType in nodeTypes)
        {
            RequireImportOrigin(nodeType.Origin, "nodeTypes.origin");
            RequireId(nodeType.TypeId, "nodeTypes.typeId");
            var hasLibrary = !string.IsNullOrWhiteSpace(nodeType.LibraryId);
            if (hasLibrary)
            {
                RequireId(nodeType.Version, "nodeTypes.version");
                if (!libraryVersionKeys.Contains(LibraryVersionKey(nodeType.Origin, nodeType.LibraryId!, nodeType.Version!, nodeType.AssemblyVersion)))
                    throw GraphStoreException.InvalidArgument($"NodeType '{nodeType.TypeId}' references unknown library version '{nodeType.LibraryId}'.");
            }
            else if (!string.IsNullOrWhiteSpace(nodeType.Version) || !string.IsNullOrWhiteSpace(nodeType.AssemblyVersion))
            {
                throw GraphStoreException.InvalidArgument($"NodeType '{nodeType.TypeId}' library version fields require libraryId.");
            }
            ValidateExtensions(nodeType.Extensions, typeof(NodeType), "nodeTypes");
        }

        foreach (var link in usedBy)
        {
            RequireImportOrigin(link.Origin, "usedBy.origin");
            RequireId(link.LibraryId, "usedBy.libraryId");
            RequireId(link.Version, "usedBy.version");
            RequireId(link.DocumentId, "usedBy.documentId");
            RequireVersion(link.VersionId, "usedBy.versionId");
            if (!libraryVersionKeys.Contains(LibraryVersionKey(link.Origin, link.LibraryId, link.Version, link.AssemblyVersion)))
                throw GraphStoreException.InvalidArgument($"usedBy references unknown library version '{link.LibraryId}'.");
            if (!documentKeys.Contains(DocumentKey(link.DocumentId, link.VersionId)))
                throw GraphStoreException.InvalidArgument($"usedBy references unknown document '{link.DocumentId}' version '{link.VersionId}'.");
        }
    }

    static void RequireImportOrigin(string origin, string label)
    {
        if (string.Equals(origin, GraphOrigins.Grasshopper, StringComparison.Ordinal) ||
            string.Equals(origin, GraphOrigins.Dynamo, StringComparison.Ordinal))
            return;
        throw GraphStoreException.InvalidArgument($"{label} must be grasshopper or dynamo.");
    }

    static void RequireId(string? value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw GraphStoreException.InvalidArgument($"{label} is required.");
    }

    static void RequireVersion(Guid versionId, string label)
    {
        if (versionId == Guid.Empty)
            throw GraphStoreException.InvalidArgument($"{label} must be a UUID.");
    }

    static void ValidateExtensions(IDictionary<string, object?>? extensions, Type entityType, string label)
    {
        if (extensions == null || extensions.Count == 0)
            return;
        var core = DbExtensionProperties.CorePropertyNames(entityType);
        foreach (var key in extensions.Keys)
        {
            try
            {
                DbExtensionProperties.ValidateKey(key, core);
            }
            catch (GraphStoreException ex)
            {
                throw GraphStoreException.InvalidArgument($"{label} extension: {ex.Message}");
            }
        }
    }

    static bool IsKnownKind(string kind) =>
        kind is NodeKinds.Operator or NodeKinds.Parameter or NodeKinds.Group or NodeKinds.Annotation or NodeKinds.Cluster;

    static bool IsKnownDirection(string direction) =>
        direction is PortDirections.In or PortDirections.Out or PortDirections.Both;

    static string DocumentKey(ImportDocument document) => DocumentKey(document.DocumentId, document.VersionId);

    static string DocumentKey(string documentId, Guid versionId) => $"{documentId}\u001f{versionId:D}";

    static string NodeKey(Guid versionId, string nodeId) => $"{versionId:D}\u001f{nodeId}";

    static string PortKey(Guid versionId, string portId) => $"{versionId:D}\u001f{portId}";

    static string EdgeKey(Guid versionId, string source, string target) => $"{versionId:D}\u001f{source}\u001f{target}";

    static string LibraryKey(ImportLibrary library) => LibraryKey(library.Origin, library.LibraryId);

    static string LibraryKey(string origin, string libraryId) => $"{origin}\u001f{libraryId}";

    static string LibraryVersionKey(ImportLibraryVersion version) =>
        LibraryVersionKey(version.Origin, version.LibraryId, version.Version, version.AssemblyVersion);

    static string LibraryVersionKey(string origin, string libraryId, string version, string? assemblyVersion) =>
        $"{origin}\u001f{libraryId}\u001f{version}\u001f{assemblyVersion ?? ""}";

    static string NodeTypeKey(ImportNodeType nodeType) => NodeTypeKey(nodeType.Origin, nodeType.TypeId);

    static string NodeTypeKey(string origin, string typeId) => $"{origin}\u001f{typeId}";

    static void RequireUnique(IEnumerable<string> values, string label)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!seen.Add(value))
                throw GraphStoreException.InvalidArgument($"Duplicate {label} '{value}'.");
        }
    }
}
