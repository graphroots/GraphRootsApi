using System;
using System.Linq;
using System.Text;
using GraphRoots.GraphDb;

namespace GraphRoots.GraphApi;

public static class RelayIds
{
    public const string Version = "v1";
    const char Sep = '\u001f';

    public static string Document(Document document) =>
        Encode("Document", document.DocumentId, document.VersionId.ToString());

    public static string Node(Node node) =>
        Encode("Node", node.VersionId.ToString(), node.NodeId);

    public static string Port(Port port) =>
        Encode("Port", port.VersionId.ToString(), port.PortId);

    public static string Edge(Edge edge) =>
        Encode("Edge", edge.VersionId.ToString(), edge.SourcePortId, edge.TargetPortId);

    public static string NodeType(NodeType nodeType) =>
        Encode("NodeType", nodeType.Origin, nodeType.TypeId);

    public static string Library(Library library) =>
        Encode("Library", library.Origin, library.LibraryId);

    public static string LibraryVersion(LibraryVersion version) =>
        Encode("LibraryVersion", version.Origin, version.LibraryId, version.Version, version.AssemblyVersion ?? "");

    public static (string Type, string[] Parts) Decode(string id)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(id));
            var parts = raw.Split(Sep);
            if (parts.Length < 2 || parts[0] != Version)
                throw GraphStoreException.InvalidArgument("Invalid entity id.");
            return (parts[1], parts[2..]);
        }
        catch (FormatException)
        {
            throw GraphStoreException.InvalidArgument("Invalid entity id.");
        }
    }

    static string Encode(params string[] parts)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(Sep, new[] { Version }.Concat(parts))));
    }
}
