using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using GraphRoots.GraphDb;

namespace GraphRoots.Grasshopper.Parser
{
    /// <summary>
    /// XML representation of a GHX archive
    /// </summary>
    [XmlRoot("Archive")]
    public class GhxArchiveXml
    {
        [XmlAttribute("name")]
        public string? ArchiveName { get; set; }

        [XmlElement("items")]
        public GhxItemsXml? Items { get; set; }

        [XmlElement("chunks")]
        public GhxChunksXml? Chunks { get; set; }
    }

    /// <see cref="IGhxArchive"/>"/>
    public class GhxArchive : IGhxArchive
    {
        public GhxArchive(GhxArchiveXml xml, IGhxArchiveParserContext context)
        {
            ArchiveName = xml.ArchiveName;
            Items = new GhxItems(xml.Items ?? new GhxItemsXml());
            Chunks = new GhxChunks(xml.Chunks ?? new GhxChunksXml());

            ArchiveVersion = context.ParsingCluster ? null : new GhxItemVersion(Items.GetItemByName("ArchiveVersion")!);
            Definition = new GhxChunkDefinition(Chunks.GetChunkByName("Definition")!, context);
        }

        public IGhxItemVersion? ArchiveVersion { get; private set; }

        public string? ArchiveName { get; private set; }

        public IGhxItems Items { get; private set; }

        public IGhxChunks Chunks { get; private set; }

        public IGhxChunkDefinition Definition { get; private set; }
    }

    /// <summary>
    /// XML representation of a container for <items> elements (used at various levels)
    /// </summary>
    public class GhxItemsXml
    {
        [XmlAttribute("count")]
        public int Count { get; set; }

        [XmlElement("item")]
        public List<GhxItemXml>? ItemList { get; set; }
    }

    /// <see cref="IGhxItems">
    public class GhxItems : IGhxItems
    {
        public GhxItems(GhxItemsXml xml)
        {
            Count = xml.Count;
            ItemList = (xml.ItemList ?? new List<GhxItemXml>()).Select(i => new GhxItem(i) as IGhxItem).ToList();
        }

        public int Count { get; private set; }

        public List<IGhxItem> ItemList { get; private set; }

        public IEnumerable<IGhxItem> GetItemsByName(string name)
        {
            return ItemList.Where(i => i.ItemName == name);
        }

        public IGhxItem? GetItemByName(string name, bool throwIfNotFound = true)
        {
            var item = GetItemsByName(name).FirstOrDefault();
            if (throwIfNotFound && item == null)
            {
                throw new ArgumentException($"Item {name} not found");
            }
            return item;
        }
    }

    /// <summary>
    /// XML representation of a generic <item> element. 
    /// This is the “raw” version obtained directly from deserialization.
    /// </summary>
    public class GhxItemXml
    {
        [XmlAttribute("name")]
        public string? ItemName { get; set; }

        [XmlAttribute("type_name")]
        public string? TypeName { get; set; }

        [XmlAttribute("type_code")]
        public int TypeCode { get; set; }

        [XmlAnyElement]
        public XmlElement[]? AnyElements { get; set; }

        [XmlText]
        public string? Value { get; set; }
    }

    /// <summary>
    /// Represents a generic <item> element. 
    /// This is the “raw” version obtained directly from deserialization.
    /// </summary>
    public class GhxItem : IGhxItem
    {
        public GhxItem(GhxItemXml xml)
        {
            ItemName = xml.ItemName;
            TypeName = xml.TypeName;
            TypeCode = xml.TypeCode;
            AnyElements = xml.AnyElements;
            Value = xml.Value;
        }

        public GhxItem(IGhxItem item)
        {
            ItemName = item.ItemName;
            TypeName = item.TypeName;
            TypeCode = item.TypeCode;
            AnyElements = item.AnyElements;
            Value = item.Value;
        }

        public XmlElement? GetElementByName(string name, bool throwIfNotFound = true)
        {
            var element = AnyElements?.Where(e => e.Name == name).FirstOrDefault();
            if (throwIfNotFound && element == null)
            {
                throw new ArgumentException($"Element {name} not found");
            }
            return element;
        }

        public string? ItemName { get; private set; }

        public string? TypeName { get; private set; }

        public int TypeCode { get; private set; }

        public XmlElement[]? AnyElements { get; private set; }

        public string? Value { get; private set; }
    }

    /// <summary>
    /// XML representation of a container for <chunks> elements (used at various levels)
    /// </summary>
    public class GhxChunksXml
    {
        [XmlAttribute("count")]
        public int Count { get; set; }

        [XmlElement("chunk")]
        public List<GhxChunkXml>? ChunkList { get; set; }
    }

    /// <see cref="IGhxChunks">
    public class GhxChunks : IGhxChunks
    {
        public GhxChunks(GhxChunksXml xml)
        {
            Count = xml.Count;
            ChunkList = (xml.ChunkList ?? new List<GhxChunkXml>()).Select(c => new GhxChunk(c) as IGhxChunk).ToList();
        }

        public int Count { get; private set; }

        public List<IGhxChunk> ChunkList { get; private set; }

        public IEnumerable<IGhxChunk> GetChunksByName(string name)
        {
            return ChunkList.Where(i => i.ChunkName == name);
        }

        public IGhxChunk? GetChunkByName(string name, bool throwIfNotFound = true)
        {
            var chunk = GetChunksByName(name).FirstOrDefault();
            if (throwIfNotFound && chunk == null)
            {
                throw new ArgumentException($"Chunk {name} not found");
            }
            return chunk;
        }
    }

    /// <summary>
    /// XML representation of a <chunk> element
    /// </summary>
    [XmlRoot("Archive")]
    public class GhxChunkXml
    {
        [XmlAttribute("name")]
        public string? ChunkName { get; set; }

        [XmlAttribute("index")]
        public string? Index { get; set; }

        [XmlElement("items")]
        public GhxItemsXml? Items { get; set; }

        [XmlElement("chunks")]
        public GhxChunksXml? Chunks { get; set; }
    }


    /// <summary>
    /// Represents a <chunk> element
    /// </summary>
    public class GhxChunk : IGhxChunk
    {
        public GhxChunk(GhxChunkXml xml)
        {
            ChunkName = xml.ChunkName ?? "";
            Index = !String.IsNullOrEmpty(xml.Index) ? GhxParse.Int(xml.Index) : (int?)null;
            Items = xml.Items != null ? new GhxItems(xml.Items) : null;
            Chunks = xml.Chunks != null ? new GhxChunks(xml.Chunks) : null;
        }

        public GhxChunk(IGhxChunk chunk)
        {
            ChunkName = chunk.ChunkName;
            Index = chunk.Index;
            Items = chunk.Items;
            Chunks = chunk.Chunks;
        }

        public string ChunkName { get; private set; }

        public int? Index { get; private set; }

        public IGhxItems? Items { get; private set; }

        public IGhxChunks? Chunks { get; private set; }
    }

    #region Specific chunks

    public class GhxChunkAttributes : GhxChunk, IGhxChunkAttributes
    {
        public GhxChunkAttributes(IGhxChunk chunk) : base(chunk)
        {
            var pivot = Items?.GetItemByName("Pivot", false);
            if (pivot != null)
            {
                Pivot = new GhxItemDrawingPointf(pivot);
            }
            var bounds = Items?.GetItemByName("Bounds", false);
            if (bounds != null)
            {
                Bounds = new GhxItemDrawingRectanglef(bounds);
            }
        }
        public IGhxItemDrawingPointf? Pivot { get; }
        public IGhxItemDrawingRectanglef? Bounds { get; }
    }

    public class GhxChunkParamInput : GhxChunk, IGhxChunkParamInput
    {
        public GhxChunkParamInput(IGhxChunk chunk) : base(chunk)
        {
            Index = base.Index!.Value;
            Attributes = new GhxChunkAttributes(Chunks!.GetChunkByName("Attributes")!);
            var access = Items!.GetItemByName("Access", false);
            if (access != null)
            {
                Access = GhxParse.Int(access.Value!);
            }
            Description = Items.GetItemByName("Description")!.Value!;
            InstanceGuid = Guid.Parse(Items.GetItemByName("InstanceGuid")!.Value!);
            var mutable = Items.GetItemByName("Mutable", false);
            Mutable = mutable != null ? bool.Parse(mutable.Value!) : null;
            ItemName = Items.GetItemByName("Name")!.Value!;
            ItemNickName = Items.GetItemByName("NickName")!.Value!;
            Optional = bool.Parse(Items.GetItemByName("Optional")!.Value!);
            Sources = Items.GetItemsByName("Source").Select(i => Guid.Parse(i.Value!)).ToList();
            SourceCount = GhxParse.Int(Items.GetItemByName("SourceCount")!.Value!);
            var mapping = Items.GetItemByName("Mapping", false);
            if (mapping != null)
            {
                Mapping = GhxParse.Int(mapping.Value!);
            }
        }

        public IGhxChunkAttributes Attributes { get; }

        public int? Access { get; }

        public string Description { get; }

        public Guid InstanceGuid { get; }

        public bool? Mutable { get; }

        public string ItemName { get; }

        public string ItemNickName { get; }

        public bool Optional { get; }

        public int? Mapping { get; }

        public IList<Guid> Sources { get; }

        public int SourceCount { get; }

        public new int Index { get; }
    }

    public class GhxChunkParamOutput : GhxChunk, IGhxChunkParamOutput
    {
        public GhxChunkParamOutput(IGhxChunk chunk) : base(chunk)
        {
            Index = base.Index!.Value;
            Attributes = new GhxChunkAttributes(Chunks!.GetChunkByName("Attributes")!);
            Description = Items!.GetItemByName("Description")!.Value!;
            InstanceGuid = Guid.Parse(Items.GetItemByName("InstanceGuid")!.Value!);
            ItemName = Items.GetItemByName("Name")!.Value!;
            ItemNickName = Items.GetItemByName("NickName")!.Value!;
            Optional = bool.Parse(Items.GetItemByName("Optional")!.Value!);
            Sources = Items.GetItemsByName("Source").Select(i => Guid.Parse(i.Value!)).ToList();
            SourceCount = GhxParse.Int(Items.GetItemByName("SourceCount")!.Value!);
            Access = Items.GetItemByName("Access", false) != null ? GhxParse.Int(Items.GetItemByName("Access")!.Value!) : null;
        }

        public IGhxChunkAttributes Attributes { get; }

        public string Description { get; }

        public Guid InstanceGuid { get; }

        public string ItemName { get; }

        public string ItemNickName { get; }

        public bool Optional { get; }

        public IList<Guid> Sources { get; }

        public int SourceCount { get; }

        public new int Index { get; }

        public int? Access { get; }
    }

    public class GhxChunkContainer : GhxChunk, IGhxChunkContainer
    {
        public GhxChunkContainer(IGhxChunk chunk, IGhxArchiveParserContext context) : base(chunk)
        {
            Attributes = new GhxChunkAttributes(Chunks!.GetChunkByName("Attributes")!);

            // Components with a variable number of inputs and outputs, as well as clusters, 
            // have a "ParameterData" chunk. This chunk contains the input and output parameters.
            var parameterData = Chunks.GetChunkByName("ParameterData", false);

            var inputParamChunks = parameterData != null ? (parameterData.Chunks != null ? parameterData.Chunks.GetChunksByName("InputParam") : Enumerable.Empty<IGhxChunk>()) : Chunks.GetChunksByName("param_input");
            ParamInputs = inputParamChunks.Select(c => new GhxChunkParamInput(c) as IGhxChunkParamInput).ToList();

            var outputParamChunks = parameterData != null ? (parameterData.Chunks != null ? parameterData.Chunks.GetChunksByName("OutputParam") : Enumerable.Empty<IGhxChunk>()) : Chunks.GetChunksByName("param_output");
            ParamOutputs = outputParamChunks.Select(c => new GhxChunkParamOutput(c) as IGhxChunkParamOutput).ToList();

            var paramMapItems = Chunks.GetChunkByName("ParamMap", false)?.Items;
            if (paramMapItems != null)
            {
                ParamMap = new Dictionary<Guid, Guid>();
                var keys = paramMapItems.GetItemsByName("Key").ToList();
                var values = paramMapItems.GetItemsByName("Value").ToList();
                if (keys.Count == values.Count)
                {
                    for (int i = 0; i < keys.Count; i++)
                    {
                        ParamMap.Add(Guid.Parse(keys[i].Value!), Guid.Parse(values[i].Value!));
                    }
                }
            }

            Description = Items!.GetItemByName("Description")!.Value!;
            var hidden = Items.GetItemByName("Hidden", false);
            if (hidden != null)
            {
                Hidden = bool.Parse(hidden.Value!);
            }
            InstanceGuid = Guid.Parse(Items.GetItemByName("InstanceGuid")!.Value!);
            Name = Items.GetItemByName("Name")!.Value!;
            NickName = Items.GetItemByName("NickName")!.Value!;
            var optional = Items.GetItemByName("Optional", false);
            if (optional != null)
            {
                Optional = bool.Parse(optional.Value!);
            }

            // Floating parameter components have a "Source" item.
            Sources = Items.GetItemsByName("Source").Select(i => Guid.Parse(i.Value!)).ToList();
            var sourceCount = Items.GetItemByName("SourceCount", false);
            if (sourceCount != null)
            {
                SourceCount = GhxParse.Int(sourceCount.Value!);
            }

            var reverseData = Items.GetItemByName("ReverseData", false);
            if (reverseData != null)
            {
                ReverseData = bool.Parse(reverseData.Value!);
            }

            var mapping = Items.GetItemByName("Mapping", false);
            if (mapping != null)
            {
                Mapping = GhxParse.Int(mapping.Value!);
            }

            var simplifyData = Items.GetItemByName("SimplifyData", false);
            if (simplifyData != null)
            {
                SimplifyData = bool.Parse(simplifyData.Value!);
            }

            var locked = Items.GetItemByName("Locked", false);
            if (locked != null)
            {
                Locked = bool.Parse(locked.Value!);
            }

            var clusterDocument = Items.GetItemByName("ClusterDocument", false);
            if (clusterDocument != null)
            {
                var binaryArchiveBase64 = clusterDocument.GetElementByName("stream", true)!.InnerText;
                var uuid5 = Uuid5.Generate(clusterHashGuid, binaryArchiveBase64);
                ClusterHash = uuid5;
                var uuid5str = uuid5.ToString();
                if (context.ParsedClusters.ContainsKey(uuid5str))
                {
                    ClusterDefinition = context.ParsedClusters[uuid5str];
                    return;
                }
                var bytes = Convert.FromBase64String(binaryArchiveBase64);
                var definition = context.Parser.ParseChunkByteArray(bytes, context.CreateClusterContext());
                context.ParsedClusters[uuid5str] = definition;
                ClusterDefinition = definition;
            }

            CustomName = Items.GetItemByName("CustomName", false)?.Value;
            CustomNickName = Items.GetItemByName("CustomNickName", false)?.Value;
            CustomDescription = Items.GetItemByName("CustomDescription", false)?.Value;
        }

        private static readonly Guid clusterHashGuid = Guid.Parse("E375A37E-75CD-4384-9506-BA3FF670A199");

        public IGhxChunkAttributes Attributes { get; }

        public IList<IGhxChunkParamInput> ParamInputs { get; }

        public IList<IGhxChunkParamOutput> ParamOutputs { get; }

        public IGhxChunkParamInput? GetParamInputByName(string name, bool throwIfNotFound = true)
        {
            var paramInput = ParamInputs.Where(i => i.ItemName == name).FirstOrDefault();
            if (throwIfNotFound && paramInput == null)
            {
                throw new ArgumentException($"ParamInput {name} not found");
            }
            return paramInput;
        }

        public IGhxChunkParamOutput? GetParamOutputByName(string name, bool throwIfNotFound = true)
        {
            var paramOutput = ParamOutputs.Where(i => i.ItemName == name).FirstOrDefault();
            if (throwIfNotFound && paramOutput == null)
            {
                throw new ArgumentException($"ParamOutput {name} not found");
            }
            return paramOutput;
        }

        public IDictionary<Guid, Guid>? ParamMap { get; }

        public IGhxChunkDefinition? ClusterDefinition { get; }

        public Guid? ClusterHash { get; }

        public string Description { get; }

        public bool? Hidden { get; }

        public Guid InstanceGuid { get; }

        public string Name { get; }

        public string NickName { get; }

        public bool? Optional { get; }

        public IList<Guid> Sources { get; }

        public int SourceCount { get; }

        public bool? ReverseData { get; }

        public int? Mapping { get; }

        public bool? SimplifyData { get; }

        public bool? Locked { get; }

        public string? CustomDescription { get; }

        public string? CustomName { get; }

        public string? CustomNickName { get; }
    }

    public class GhxChunkLibrary : GhxChunk, IGhxChunkLibrary
    {
        public GhxChunkLibrary(IGhxChunk chunk) : base(chunk)
        {
            Index = base.Index!.Value;
            AssemblyFullName = Items!.GetItemByName("AssemblyFullName", false)?.Value;
            AssemblyVersion = Items.GetItemByName("AssemblyVersion", false)?.Value;
            Author = Items.GetItemByName("Author")!.Value!;
            Id = Guid.Parse(Items.GetItemByName("Id")!.Value!);
            Name = Items.GetItemByName("Name")!.Value!;
            Version = Items.GetItemByName("Version")!.Value!;
        }

        public string? AssemblyFullName { get; }

        public string? AssemblyVersion { get; }

        public string Author { get; }

        public Guid Id { get; }

        public string Name { get; }

        public string Version { get; }

        public new int Index { get; }
    }

    public class GhxChunkGhaLibraries : GhxChunk, IGhxChunkGhaLibraries
    {
        public GhxChunkGhaLibraries(IGhxChunk chunk) : base(chunk)
        {
            LibraryCount = GhxParse.Int(Items!.GetItemByName("Count")!.Value!);
            Libraries = LibraryCount == 0 ? new List<IGhxChunkLibrary>() : Chunks!.GetChunksByName("Library").Select(c => new GhxChunkLibrary(c) as IGhxChunkLibrary).ToList();
        }
        public IList<IGhxChunkLibrary> Libraries { get; }
        public int LibraryCount { get; }
    }

    public class GhxChunkObject : GhxChunk, IGhxChunkObject
    {
        public GhxChunkObject(IGhxChunk chunk, IGhxArchiveParserContext context) : base(chunk)
        {
            Index = base.Index!.Value;
            Container = new GhxChunkContainer(Chunks!.GetChunkByName("Container")!, context);
            DefinitionName = Items!.GetItemByName("Name")!.Value!;
            Guid = Guid.Parse(Items.GetItemByName("GUID")!.Value!);
            var lib = Items.GetItemByName("Lib", false);
            if (lib != null)
            {
                Lib = Guid.Parse(lib.Value!);
            }
        }

        public IGhxChunkContainer Container { get; }

        public string DefinitionName { get; }

        public Guid Guid { get; }

        public Guid? Lib { get; }

        public new int Index { get; }
    }

    public class GhxChunkDefinitionObjects : GhxChunk, IGhxChunkDefinitionObjects
    {
        public GhxChunkDefinitionObjects(IGhxChunk chunk, IGhxArchiveParserContext context) : base(chunk)
        {
            Objects = Chunks!.GetChunksByName("Object").Select(c => new GhxChunkObject(c, context) as IGhxChunkObject).ToList();
            ObjectCount = GhxParse.Int(Items!.GetItemByName("ObjectCount")!.Value!);
        }

        public IList<IGhxChunkObject> Objects { get; }

        public int ObjectCount { get; }
    }

    public class GhxChunkDocumentHeader : GhxChunk, IGhxChunkDocumentHeader
    {
        public GhxChunkDocumentHeader(IGhxChunk chunk) : base(chunk)
        {
            DocumentID = Guid.Parse(Items!.GetItemByName("DocumentID")!.Value!);
        }

        public Guid DocumentID { get; }
    }

    public class GhxChunkDefinitionProperties : GhxChunk, IGhxChunkDefinitionProperties
    {
        public GhxChunkDefinitionProperties(IGhxChunk chunk) : base(chunk)
        {
            Date = new DateTime(GhxParse.Long(Items!.GetItemByName("Date")!.Value!));
            Description = Items.GetItemByName("Description")!.Value!;
            Name = Items.GetItemByName("Name")!.Value!;
        }

        public DateTime Date { get; }

        public string Description { get; }

        public string Name { get; }
    }

    public class GhxChunkDefinition : GhxChunk, IGhxChunkDefinition
    {
        public GhxChunkDefinition(IGhxChunk chunk, IGhxArchiveParserContext context) : base(chunk)
        {
            PluginVersion = new GhxItemVersion(Items!.GetItemByName("plugin_version")!);
            DefinitionObjects = new GhxChunkDefinitionObjects(Chunks!.GetChunkByName("DefinitionObjects")!, context);
            DocumentHeader = new GhxChunkDocumentHeader(Chunks.GetChunkByName("DocumentHeader")!);
            DefinitionProperties = new GhxChunkDefinitionProperties(Chunks.GetChunkByName("DefinitionProperties")!);
            var libraryChunk = Chunks.GetChunkByName("GHALibraries", false);
            Libraries = libraryChunk != null ? new GhxChunkGhaLibraries(libraryChunk) : null;
        }

        public GhxChunkDefinition(GhxChunkXml xml, IGhxArchiveParserContext context) : base(xml)
        {
            PluginVersion = new GhxItemVersion(Items!.GetItemByName("plugin_version")!);
            DefinitionObjects = new GhxChunkDefinitionObjects(Chunks!.GetChunkByName("DefinitionObjects")!, context);
            DocumentHeader = new GhxChunkDocumentHeader(Chunks.GetChunkByName("DocumentHeader")!);
            DefinitionProperties = new GhxChunkDefinitionProperties(Chunks.GetChunkByName("DefinitionProperties")!);
            var libraryChunk = Chunks.GetChunkByName("GHALibraries", !context.ParsingCluster);
            Libraries = libraryChunk != null ? new GhxChunkGhaLibraries(libraryChunk) : null;
        }

        public IGhxItemVersion PluginVersion { get; }

        public IGhxChunkDefinitionObjects DefinitionObjects { get; }

        public IGhxChunkGhaLibraries? Libraries { get; }

        public IGhxChunkDocumentHeader DocumentHeader { get; }

        public IGhxChunkDefinitionProperties DefinitionProperties { get; }
    }

    #endregion

    #region Specific items

    public class GhxItemDrawingPointf : GhxItem, IGhxItemDrawingPointf
    {
        public GhxItemDrawingPointf(IGhxItem item) : base(item)
        {
            PivotX = GhxParse.Float(item.GetElementByName("X")!.InnerText);
            PivotY = GhxParse.Float(item.GetElementByName("Y")!.InnerText);
        }

        public float PivotX { get; }
        public float PivotY { get; }
    }

    public class GhxItemDrawingRectanglef : GhxItem, IGhxItemDrawingRectanglef
    {
        public GhxItemDrawingRectanglef(IGhxItem item) : base(item)
        {
            X = GhxParse.Float(item.GetElementByName("X")!.InnerText);
            Y = GhxParse.Float(item.GetElementByName("Y")!.InnerText);
            W = GhxParse.Float(item.GetElementByName("W")!.InnerText);
            H = GhxParse.Float(item.GetElementByName("H")!.InnerText);
        }

        public float X { get; }
        public float Y { get; }
        public float W { get; }
        public float H { get; }
    }


    public class GhxItemVersion : GhxItem, IGhxItemVersion
    {
        public GhxItemVersion(IGhxItem item) : base(item)
        {
            Version = new Version(GhxParse.Int(item.GetElementByName("Major")!.InnerText), GhxParse.Int(item.GetElementByName("Minor")!.InnerText), GhxParse.Int(item.GetElementByName("Revision")!.InnerText));
        }

        public Version Version { get; }
    }

    #endregion
}
