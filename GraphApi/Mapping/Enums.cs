using GraphRoots.GraphDb;

namespace GraphRoots.GraphApi;

public enum GqlOrigin
{
    GRASSHOPPER,
    DYNAMO,
    GRAPHROOTS,
    UNKNOWN,
}

public enum GqlNodeKind
{
    OPERATOR,
    PARAMETER,
    GROUP,
    ANNOTATION,
    CLUSTER,
}

public enum GqlPortDirection
{
    IN,
    OUT,
    BOTH,
}

public enum GqlEntityKind
{
    DOCUMENT,
    NODE,
    PORT,
    EDGE,
}

public enum GqlSortDirection
{
    ASC,
    DESC,
}

public enum GqlDocumentSortField
{
    DOCUMENT_ID,
    VERSION_ID,
    FILE_NAME,
    CREATED_AT,
}

public enum GqlNodeSortField
{
    NODE_ID,
    VERSION_ID,
    NAME,
    TYPE_ID,
    KIND,
    X,
    Y,
}

public enum GqlPortSortField
{
    PORT_ID,
    VERSION_ID,
    NAME,
    DIRECTION,
}

public enum GqlEdgeSortField
{
    VERSION_ID,
    SOURCE_PORT_ID,
    TARGET_PORT_ID,
}

public enum GqlNodeTypeSortField
{
    ORIGIN,
    TYPE_ID,
    NAME,
}

public enum GqlLibrarySortField
{
    ORIGIN,
    LIBRARY_ID,
    NAME,
}

public static class EnumMapping
{
    public static string ToStore(GqlOrigin origin) => origin switch
    {
        GqlOrigin.GRASSHOPPER => GraphOrigins.Grasshopper,
        GqlOrigin.DYNAMO => GraphOrigins.Dynamo,
        GqlOrigin.GRAPHROOTS => GraphOrigins.GraphRoots,
        GqlOrigin.UNKNOWN => throw GraphStoreException.InvalidArgument("UNKNOWN is not a writable origin."),
        _ => throw GraphStoreException.InvalidArgument($"Unknown origin '{origin}'."),
    };

    public static string ToStoreFilter(GqlOrigin origin) => origin == GqlOrigin.UNKNOWN
        ? GraphOrigins.UnknownFilter
        : ToStore(origin);

    public static GqlOrigin ToGqlOrigin(string? origin) => origin switch
    {
        GraphOrigins.Grasshopper => GqlOrigin.GRASSHOPPER,
        GraphOrigins.Dynamo => GqlOrigin.DYNAMO,
        GraphOrigins.GraphRoots => GqlOrigin.GRAPHROOTS,
        null or "" => GqlOrigin.UNKNOWN,
        _ => GqlOrigin.UNKNOWN,
    };

    public static string ToStore(GqlNodeKind kind) => kind switch
    {
        GqlNodeKind.OPERATOR => NodeKinds.Operator,
        GqlNodeKind.PARAMETER => NodeKinds.Parameter,
        GqlNodeKind.GROUP => NodeKinds.Group,
        GqlNodeKind.ANNOTATION => NodeKinds.Annotation,
        GqlNodeKind.CLUSTER => NodeKinds.Cluster,
        _ => throw GraphStoreException.InvalidArgument($"Unknown kind '{kind}'."),
    };

    public static GqlNodeKind? ToGqlKind(string? kind) => kind switch
    {
        NodeKinds.Operator => GqlNodeKind.OPERATOR,
        NodeKinds.Parameter => GqlNodeKind.PARAMETER,
        NodeKinds.Group => GqlNodeKind.GROUP,
        NodeKinds.Annotation => GqlNodeKind.ANNOTATION,
        NodeKinds.Cluster => GqlNodeKind.CLUSTER,
        _ => null,
    };

    public static string ToStore(GqlPortDirection direction) => direction switch
    {
        GqlPortDirection.IN => PortDirections.In,
        GqlPortDirection.OUT => PortDirections.Out,
        GqlPortDirection.BOTH => PortDirections.Both,
        _ => throw GraphStoreException.InvalidArgument($"Unknown direction '{direction}'."),
    };

    public static GqlPortDirection? ToGqlDirection(string? direction) => direction switch
    {
        PortDirections.In => GqlPortDirection.IN,
        PortDirections.Out => GqlPortDirection.OUT,
        PortDirections.Both => GqlPortDirection.BOTH,
        _ => null,
    };

    public static StoreEntityKind ToStore(GqlEntityKind kind) => kind switch
    {
        GqlEntityKind.DOCUMENT => StoreEntityKind.Document,
        GqlEntityKind.NODE => StoreEntityKind.Node,
        GqlEntityKind.PORT => StoreEntityKind.Port,
        GqlEntityKind.EDGE => StoreEntityKind.Edge,
        _ => throw GraphStoreException.InvalidArgument($"Unknown entity kind '{kind}'."),
    };

    public static StoreSortDirection ToStore(GqlSortDirection? direction) =>
        direction == GqlSortDirection.DESC ? StoreSortDirection.Desc : StoreSortDirection.Asc;

    public static string ToStore(GqlDocumentSortField field) => field switch
    {
        GqlDocumentSortField.DOCUMENT_ID => "documentId",
        GqlDocumentSortField.VERSION_ID => "versionId",
        GqlDocumentSortField.FILE_NAME => "fileName",
        GqlDocumentSortField.CREATED_AT => "createdAt",
        _ => "documentId",
    };

    public static string ToStore(GqlNodeSortField field) => field switch
    {
        GqlNodeSortField.NODE_ID => "nodeId",
        GqlNodeSortField.VERSION_ID => "versionId",
        GqlNodeSortField.NAME => "name",
        GqlNodeSortField.TYPE_ID => "typeId",
        GqlNodeSortField.KIND => "kind",
        GqlNodeSortField.X => "x",
        GqlNodeSortField.Y => "y",
        _ => "nodeId",
    };

    public static string ToStore(GqlPortSortField field) => field switch
    {
        GqlPortSortField.PORT_ID => "portId",
        GqlPortSortField.VERSION_ID => "versionId",
        GqlPortSortField.NAME => "name",
        GqlPortSortField.DIRECTION => "direction",
        _ => "portId",
    };

    public static string ToStore(GqlEdgeSortField field) => field switch
    {
        GqlEdgeSortField.VERSION_ID => "versionId",
        GqlEdgeSortField.SOURCE_PORT_ID => "sourcePortId",
        GqlEdgeSortField.TARGET_PORT_ID => "targetPortId",
        _ => "versionId",
    };

    public static string ToStore(GqlNodeTypeSortField field) => field switch
    {
        GqlNodeTypeSortField.ORIGIN => "origin",
        GqlNodeTypeSortField.TYPE_ID => "typeId",
        GqlNodeTypeSortField.NAME => "name",
        _ => "typeId",
    };

    public static string ToStore(GqlLibrarySortField field) => field switch
    {
        GqlLibrarySortField.ORIGIN => "origin",
        GqlLibrarySortField.LIBRARY_ID => "libraryId",
        GqlLibrarySortField.NAME => "name",
        _ => "libraryId",
    };
}
