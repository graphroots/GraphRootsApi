using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using GraphRoots.GraphDb;
using GraphRoots.Grasshopper.Parser;

namespace GraphRoots.Grasshopper
{
    public class GhxSnapshotBuilder : IGhxSnapshotBuilder
    {
        static string Id(Guid guid) => guid.ToString();

        static string? MapAccess(int? access) => access switch
        {
            0 => "item",
            1 => "list",
            2 => "tree",
            _ => access?.ToString(),
        };

        static IDictionary<string, object?>? Extensions(params (string key, object? value)[] pairs)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var (key, value) in pairs)
            {
                if (value == null)
                    continue;
                if (value is string s && s.Length == 0)
                    continue;
                dict[key] = value;
            }
            return dict.Count == 0 ? null : dict;
        }

        static string DetermineKind(IGhxChunkObject chunk)
        {
            if (chunk.Guid == GhComponentIds.Group)
                return NodeKinds.Group;
            if (chunk.Guid == GhComponentIds.Scribble)
                return NodeKinds.Annotation;
            if (chunk.Container.ClusterDefinition != null || chunk.Guid == GhComponentIds.GRASSHOPPER_CLUSTER_COMPONENT_ID)
                return NodeKinds.Cluster;
            if ((chunk.Container.ParamInputs == null || chunk.Container.ParamInputs.Count == 0)
                && (chunk.Container.ParamOutputs == null || chunk.Container.ParamOutputs.Count == 0))
                return NodeKinds.Parameter;
            return NodeKinds.Operator;
        }

        public ImportSnapshot Build(IGhxLoaderContext context, CancellationToken cancellationToken = default)
        {
            var acc = new Accumulator();
            var document = MapRootDocument(context);
            acc.AddDocument(document);
            MapDefinition(context.GhxArchive.Definition, document, acc, cancellationToken);
            return acc.ToSnapshot();
        }

        static ImportDocument MapRootDocument(IGhxLoaderContext context) => new()
        {
            DocumentId = Id(context.GhxArchive.Definition.DocumentHeader.DocumentID),
            VersionId = context.VersionId,
            Origin = GraphOrigins.Grasshopper,
            FileName = context.FileName,
            FilePath = context.FilePath,
            FileCreationTimeUtc = context.FileCreationTimeUtc,
            FileLastWriteTimeUtc = context.FileLastWriteTimeUtc,
            IsNested = false,
        };

        static ImportDocument MapClusterDocument(IGhxChunkContainer clusterChunk)
        {
            if (!clusterChunk.ClusterHash.HasValue)
                throw new ArgumentException($"Chunk {clusterChunk.InstanceGuid} is not a cluster.");
            return new ImportDocument
            {
                DocumentId = Id(clusterChunk.ClusterDefinition!.DocumentHeader.DocumentID),
                VersionId = clusterChunk.ClusterHash.Value,
                Origin = GraphOrigins.Grasshopper,
                FileName = clusterChunk.ClusterDefinition.DefinitionProperties.Name,
                IsNested = true,
            };
        }

        static void MapDefinition(IGhxChunkDefinition definition, ImportDocument document, Accumulator acc, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var versionId = document.VersionId;
            var portById = new Dictionary<string, Port>(StringComparer.OrdinalIgnoreCase);
            var ownerByPort = IndexPortOwners(definition);
            var referencedSources = CollectReferencedSourceGuids(definition);

            var nodes = new List<ImportNode>();
            var ports = new List<ImportPort>();

            foreach (var chunk in definition.DefinitionObjects.Objects)
            {
                var node = MapNode(chunk, document.DocumentId, versionId);
                nodes.Add(node);
                foreach (var port in MapPorts(chunk, versionId, portById, referencedSources))
                    ports.Add(ToImportPort(port, document.DocumentId, node.NodeId));
            }

            var nodeById = nodes.ToDictionary(n => n.NodeId, StringComparer.OrdinalIgnoreCase);
            var mappedEdges = MapEdges(definition, versionId, portById, ownerByPort).ToList();
            AssignSyntheticPortDirections(ports, nodes, mappedEdges);

            acc.Nodes.AddRange(nodes);
            acc.Ports.AddRange(ports);
            foreach (var edge in mappedEdges)
            {
                acc.Edges.Add(new ImportEdge
                {
                    DocumentId = document.DocumentId,
                    VersionId = edge.Item3.VersionId,
                    SourcePortId = edge.Item3.SourcePortId,
                    TargetPortId = edge.Item3.TargetPortId,
                    SourceName = edge.Item3.SourceName,
                    TargetName = edge.Item3.TargetName,
                    Extensions = edge.Item3.Extensions,
                });
            }

            foreach (var chunk in definition.DefinitionObjects.Objects.Where(o => o.Guid == GhComponentIds.Group))
            {
                if (!nodeById.TryGetValue(Id(chunk.Container.InstanceGuid), out var group))
                    continue;
                var ids = chunk.Container.Items?.GetItemsByName("ID") ?? Enumerable.Empty<IGhxItem>();
                foreach (var item in ids)
                {
                    if (item.Value == null)
                        continue;
                    var memberId = Guid.Parse(item.Value).ToString();
                    if (nodeById.ContainsKey(memberId))
                    {
                        acc.GroupMemberships.Add(new ImportGroupMembership
                        {
                            VersionId = versionId,
                            MemberNodeId = memberId,
                            GroupNodeId = group.NodeId,
                        });
                    }
                }
            }

            foreach (var library in MapLibraries(definition))
                acc.AddLibrary(library);
            var definitionLibraryVersions = MapLibraryVersions(definition).ToList();
            var libraryVersionById = definitionLibraryVersions
                .GroupBy(lv => lv.LibraryId, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
            foreach (var version in definitionLibraryVersions)
            {
                acc.AddLibraryVersion(version);
                acc.UsedBy.Add(new ImportUsedBy
                {
                    Origin = version.Origin,
                    LibraryId = version.LibraryId,
                    Version = version.Version,
                    AssemblyVersion = version.AssemblyVersion,
                    DocumentId = document.DocumentId,
                    VersionId = document.VersionId,
                });
            }

            foreach (var o in definition.DefinitionObjects.Objects)
            {
                var typeId = Id(o.Guid);
                string? libraryId = o.Lib is Guid lib ? Id(lib) : null;
                string? version = null;
                string? assemblyVersion = null;
                if (libraryId != null && libraryVersionById.TryGetValue(libraryId, out var libraryVersion))
                {
                    version = libraryVersion.Version;
                    assemblyVersion = libraryVersion.AssemblyVersion;
                }
                acc.AddNodeType(new ImportNodeType
                {
                    Origin = GraphOrigins.Grasshopper,
                    TypeId = typeId,
                    Name = o.DefinitionName,
                    LibraryId = libraryId != null && version != null ? libraryId : null,
                    Version = version,
                    AssemblyVersion = assemblyVersion,
                });
            }

            foreach (var cluster in definition.DefinitionObjects.Objects.Where(o => o.Container.ClusterDefinition != null))
            {
                var clusterDoc = MapClusterDocument(cluster.Container);
                var added = acc.AddDocument(clusterDoc);
                acc.AddNest(new ImportNest
                {
                    ParentDocumentId = document.DocumentId,
                    ParentVersionId = document.VersionId,
                    ChildDocumentId = clusterDoc.DocumentId,
                    ChildVersionId = clusterDoc.VersionId,
                });
                if (added)
                    MapDefinition(cluster.Container.ClusterDefinition!, clusterDoc, acc, cancellationToken);
            }
        }

        static ImportNode MapNode(IGhxChunkObject chunk, string documentId, Guid versionId)
        {
            var items = chunk.Container.Items;
            var kind = DetermineKind(chunk);

            string? source = null;
            string? language = null;
            string? scriptText = null;
            if (GhComponentIds.IsRhinoCodeScriptComponent(chunk.Guid))
            {
                var raw = chunk.Container.Chunks?.GetChunkByName("Script", false)?.Items?.GetItemByName("Text", false)?.Value;
                if (raw != null)
                {
                    scriptText = Encoding.UTF8.GetString(Convert.FromBase64String(raw));
                    source = scriptText;
                }
                language = GhComponentIds.GetRhinoCodeScriptComponentLanguage(chunk.Guid);
            }
            else if (GhComponentIds.IsLegacyScriptComponent(chunk.Guid))
            {
                language = GhComponentIds.GetLegacyScriptComponentLanguage(chunk.Guid);
            }

            var text = chunk.Guid == GhComponentIds.Scribble
                ? items?.GetItemByName("Text", false)?.Value
                : null;

            return new ImportNode
            {
                DocumentId = documentId,
                VersionId = versionId,
                NodeId = Id(chunk.Container.InstanceGuid),
                Origin = GraphOrigins.Grasshopper,
                TypeId = Id(chunk.Guid),
                Name = chunk.Container.Name,
                NickName = chunk.Container.NickName,
                Kind = kind,
                Locked = chunk.Container.Locked,
                X = chunk.Container.Attributes.Pivot?.PivotX,
                Y = chunk.Container.Attributes.Pivot?.PivotY,
                Source = source,
                Language = language,
                Text = text,
                Extensions = Extensions(
                    (GhExtensionKeys.ComponentGuid, Id(chunk.Guid)),
                    (GhExtensionKeys.ScriptSource, items?.GetItemByName("ScriptSource", false)?.Value),
                    (GhExtensionKeys.UsingSource, items?.GetItemByName("UsingSource", false)?.Value),
                    (GhExtensionKeys.AdditionalSource, items?.GetItemByName("AdditionalSource", false)?.Value),
                    (GhExtensionKeys.ScriptText, scriptText),
                    (GhExtensionKeys.ClusterVersionId, chunk.Container.ClusterHash is Guid hash ? hash.ToString() : null),
                    (GhExtensionKeys.ClusterParamMap, FormatParamMap(chunk.Container.ParamMap))
                ),
            };
        }

        static string? FormatParamMap(IDictionary<Guid, Guid>? paramMap)
        {
            if (paramMap == null || paramMap.Count == 0)
                return null;
            return string.Join(";", paramMap.Select(kvp => $"{kvp.Key}={kvp.Value}"));
        }

        static IEnumerable<Port> MapPorts(IGhxChunkObject chunk, Guid versionId, Dictionary<string, Port> portById, HashSet<Guid> referencedSources)
        {
            var ports = new List<Port>();

            foreach (var input in chunk.Container.ParamInputs ?? Enumerable.Empty<IGhxChunkParamInput>())
            {
                var port = new Port
                {
                    VersionId = versionId,
                    PortId = Id(input.InstanceGuid),
                    Name = input.ItemName,
                    Direction = PortDirections.In,
                    Access = MapAccess(input.Access),
                };
                ports.Add(port);
                portById[port.PortId] = port;
            }

            foreach (var output in chunk.Container.ParamOutputs ?? Enumerable.Empty<IGhxChunkParamOutput>())
            {
                var port = new Port
                {
                    VersionId = versionId,
                    PortId = Id(output.InstanceGuid),
                    Name = output.ItemName,
                    Direction = PortDirections.Out,
                    Access = MapAccess(output.Access),
                };
                ports.Add(port);
                portById[port.PortId] = port;
            }

            var instanceId = Id(chunk.Container.InstanceGuid);
            if (ports.Count > 0 || portById.ContainsKey(instanceId))
                return ports;

            var hasContainerSources = chunk.Container.Sources != null && chunk.Container.Sources.Count > 0;
            var kind = DetermineKind(chunk);
            var isGroupOrAnnotation = kind == NodeKinds.Group || kind == NodeKinds.Annotation;
            var referencedAsSource = referencedSources.Contains(chunk.Container.InstanceGuid);
            var needsSynthetic = hasContainerSources
                || (referencedAsSource && !isGroupOrAnnotation);
            if (!needsSynthetic)
                return ports;

            var synthetic = new Port
            {
                VersionId = versionId,
                PortId = instanceId,
                Name = chunk.Container.Name,
            };
            ports.Add(synthetic);
            portById[instanceId] = synthetic;
            return ports;
        }

        static ImportPort ToImportPort(Port port, string documentId, string nodeId) => new()
        {
            DocumentId = documentId,
            VersionId = port.VersionId,
            NodeId = nodeId,
            PortId = port.PortId,
            Name = port.Name,
            Direction = port.Direction,
            Access = port.Access,
            Extensions = port.Extensions,
        };

        static HashSet<Guid> CollectReferencedSourceGuids(IGhxChunkDefinition definition)
        {
            var ids = new HashSet<Guid>();
            foreach (var chunk in definition.DefinitionObjects.Objects)
            {
                if (chunk.Container.Sources != null)
                {
                    foreach (var source in chunk.Container.Sources)
                        ids.Add(source);
                }

                foreach (var input in chunk.Container.ParamInputs ?? Enumerable.Empty<IGhxChunkParamInput>())
                {
                    if (input.Sources == null)
                        continue;
                    foreach (var source in input.Sources)
                        ids.Add(source);
                }
            }
            return ids;
        }

        static void AssignSyntheticPortDirections(IEnumerable<ImportPort> ports, IReadOnlyCollection<ImportNode> nodes, IEnumerable<Tuple<Port, Port, Edge>> edges)
        {
            var nodeIds = new HashSet<string>(nodes.Select(n => n.NodeId), StringComparer.OrdinalIgnoreCase);
            var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var edge in edges)
            {
                sources.Add(edge.Item3.SourcePortId);
                targets.Add(edge.Item3.TargetPortId);
            }

            foreach (var port in ports)
            {
                if (!nodeIds.Contains(port.PortId))
                    continue;
                var usedAsSource = sources.Contains(port.PortId);
                var usedAsTarget = targets.Contains(port.PortId);
                if (usedAsSource && usedAsTarget)
                    port.Direction = PortDirections.Both;
                else if (usedAsTarget)
                    port.Direction = PortDirections.In;
                else if (usedAsSource)
                    port.Direction = PortDirections.Out;
            }
        }

        static Dictionary<Guid, IGhxChunkObject> IndexPortOwners(IGhxChunkDefinition definition)
        {
            var ownerByPort = new Dictionary<Guid, IGhxChunkObject>();
            foreach (var chunk in definition.DefinitionObjects.Objects)
            {
                ownerByPort[chunk.Container.InstanceGuid] = chunk;
                foreach (var output in chunk.Container.ParamOutputs ?? Enumerable.Empty<IGhxChunkParamOutput>())
                    ownerByPort[output.InstanceGuid] = chunk;
                foreach (var input in chunk.Container.ParamInputs ?? Enumerable.Empty<IGhxChunkParamInput>())
                    ownerByPort[input.InstanceGuid] = chunk;
            }
            return ownerByPort;
        }

        static IEnumerable<Tuple<Port, Port, Edge>> MapEdges(
            IGhxChunkDefinition definition,
            Guid versionId,
            Dictionary<string, Port> portById,
            Dictionary<Guid, IGhxChunkObject> ownerByPort)
        {
            foreach (var chunk in definition.DefinitionObjects.Objects)
            {
                if (chunk.Container.Sources != null && chunk.Container.Sources.Count > 0)
                {
                    foreach (var edge in MapEdgesToTarget(chunk, null, chunk.Container.InstanceGuid, versionId, portById, ownerByPort))
                        yield return edge;
                }

                foreach (var input in chunk.Container.ParamInputs ?? Enumerable.Empty<IGhxChunkParamInput>())
                {
                    if (input.Sources == null || input.Sources.Count == 0)
                        continue;
                    foreach (var edge in MapEdgesToTarget(chunk, input, input.InstanceGuid, versionId, portById, ownerByPort))
                        yield return edge;
                }
            }
        }

        static IEnumerable<Tuple<Port, Port, Edge>> MapEdgesToTarget(
            IGhxChunkObject targetObject,
            IGhxChunkParamInput? targetInput,
            Guid targetPortGuid,
            Guid versionId,
            Dictionary<string, Port> portById,
            Dictionary<Guid, IGhxChunkObject> ownerByPort)
        {
            IGhxSources sources = targetInput != null ? targetInput : targetObject.Container;
            var targetPortId = Id(targetPortGuid);
            if (!portById.TryGetValue(targetPortId, out var targetPort))
                throw new InvalidOperationException($"No target port found for {targetPortId}");

            foreach (var source in sources.Sources)
            {
                var sourcePortId = Id(source);
                if (!portById.TryGetValue(sourcePortId, out var sourcePort))
                    throw new InvalidOperationException($"No source found for {source}");

                string? clusterSource = null;
                if (ownerByPort.TryGetValue(source, out var sourceOwner)
                    && sourceOwner.Container.ParamMap != null
                    && sourceOwner.Container.ParamMap.TryGetValue(source, out var mappedSource))
                    clusterSource = mappedSource.ToString();

                string? clusterTarget = null;
                if (targetInput != null
                    && targetObject.Container.ParamMap != null
                    && targetObject.Container.ParamMap.TryGetValue(targetInput.InstanceGuid, out var mappedTarget))
                    clusterTarget = mappedTarget.ToString();

                yield return Tuple.Create(sourcePort, targetPort, new Edge
                {
                    VersionId = versionId,
                    SourcePortId = sourcePortId,
                    TargetPortId = targetPortId,
                    SourceName = sourcePort.Name,
                    TargetName = targetPort.Name,
                    Extensions = Extensions(
                        (GhExtensionKeys.ClusterSourcePortId, clusterSource),
                        (GhExtensionKeys.ClusterTargetPortId, clusterTarget)
                    ),
                });
            }
        }

        static IEnumerable<ImportLibrary> MapLibraries(IGhxChunkDefinition definition)
        {
            return definition.Libraries?.Libraries?.Select(lib => new ImportLibrary
            {
                Origin = GraphOrigins.Grasshopper,
                LibraryId = Id(lib.Id),
                Name = lib.Name,
                Author = lib.Author,
            }) ?? Enumerable.Empty<ImportLibrary>();
        }

        static IEnumerable<ImportLibraryVersion> MapLibraryVersions(IGhxChunkDefinition definition)
        {
            return definition.Libraries?.Libraries?.Select(lib => new ImportLibraryVersion
            {
                Origin = GraphOrigins.Grasshopper,
                LibraryId = Id(lib.Id),
                Version = lib.Version ?? "unknown",
                AssemblyVersion = lib.AssemblyVersion ?? "unknown",
                Name = lib.Name,
                Author = lib.Author,
                Extensions = Extensions((GhExtensionKeys.AssemblyName, lib.AssemblyFullName)),
            }) ?? Enumerable.Empty<ImportLibraryVersion>();
        }

        sealed class Accumulator
        {
            public List<ImportDocument> Documents { get; } = [];
            public List<ImportNode> Nodes { get; } = [];
            public List<ImportPort> Ports { get; } = [];
            public List<ImportEdge> Edges { get; } = [];
            public List<ImportGroupMembership> GroupMemberships { get; } = [];
            public List<ImportNest> Nests { get; } = [];
            public List<ImportLibrary> Libraries { get; } = [];
            public List<ImportLibraryVersion> LibraryVersions { get; } = [];
            public List<ImportNodeType> NodeTypes { get; } = [];
            public List<ImportUsedBy> UsedBy { get; } = [];

            readonly HashSet<string> _documentKeys = new(StringComparer.Ordinal);
            readonly HashSet<string> _nestKeys = new(StringComparer.Ordinal);
            readonly HashSet<string> _libraryKeys = new(StringComparer.Ordinal);
            readonly HashSet<string> _libraryVersionKeys = new(StringComparer.Ordinal);
            readonly HashSet<string> _nodeTypeKeys = new(StringComparer.Ordinal);

            public bool AddDocument(ImportDocument document)
            {
                var key = $"{document.DocumentId}\u001f{document.VersionId:D}";
                if (!_documentKeys.Add(key))
                    return false;
                Documents.Add(document);
                return true;
            }

            public void AddNest(ImportNest nest)
            {
                var key = $"{nest.ParentDocumentId}\u001f{nest.ParentVersionId:D}\u001f{nest.ChildDocumentId}\u001f{nest.ChildVersionId:D}";
                if (_nestKeys.Add(key))
                    Nests.Add(nest);
            }

            public void AddLibrary(ImportLibrary library)
            {
                var key = $"{library.Origin}\u001f{library.LibraryId}";
                if (_libraryKeys.Add(key))
                    Libraries.Add(library);
            }

            public void AddLibraryVersion(ImportLibraryVersion version)
            {
                var key = $"{version.Origin}\u001f{version.LibraryId}\u001f{version.Version}\u001f{version.AssemblyVersion ?? ""}";
                if (_libraryVersionKeys.Add(key))
                    LibraryVersions.Add(version);
            }

            public void AddNodeType(ImportNodeType nodeType)
            {
                var key = $"{nodeType.Origin}\u001f{nodeType.TypeId}";
                if (_nodeTypeKeys.Add(key))
                    NodeTypes.Add(nodeType);
            }

            public ImportSnapshot ToSnapshot() => new()
            {
                Documents = Documents,
                Nodes = Nodes,
                Ports = Ports,
                Edges = Edges,
                GroupMemberships = GroupMemberships,
                Nests = Nests,
                Libraries = Libraries,
                LibraryVersions = LibraryVersions,
                NodeTypes = NodeTypes,
                UsedBy = UsedBy,
            };
        }
    }
}
