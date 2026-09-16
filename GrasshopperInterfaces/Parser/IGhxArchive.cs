using System;
using System.Collections.Generic;
using System.Xml;

namespace GraphRoots.Grasshopper.Parser
{
    /// <summary>
    /// A parsed Grasshopper XML archive.
    /// </summary>
    public interface IGhxArchive
    {
        /// <summary>
        /// The name of the archive.
        /// "Root" for the root element of the XML file.
        /// </summary>
        string? ArchiveName { get; }

        IGhxItemVersion? ArchiveVersion { get; }

        IGhxItems Items { get; }

        IGhxChunks Chunks { get; }

        IGhxChunkDefinition Definition { get; }

    }

    /// <summary>
    /// A list of items in a Grasshopper XML archive.
    /// </summary>
    public interface IGhxItems
    {
        /// <summary>
        /// Count of the items in the list.
        /// </summary>
        int Count { get; }

        /// <summary>
        /// List of items in the archive.
        /// </summary>
        List<IGhxItem> ItemList { get; }

        /// <summary>
        /// Get an item by its name. Returns the first item found with that name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="throwIfNotFound"></param>
        /// <returns></returns>
        IGhxItem? GetItemByName(string name, bool throwIfNotFound = true);

        /// <summary>
        /// Get multiple items by their name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        IEnumerable<IGhxItem> GetItemsByName(string name);
    }

    /// <summary>
    /// An item in a Grasshopper XML archive.
    /// Items can contain further XML elements, but they do not contains <see cref="IGhxChunks"/>.
    /// </summary>
    public interface IGhxItem
    {
        /// <summary>
        /// Name of the item.
        /// </summary>
        string? ItemName { get; }

        /// <summary>
        /// Type name of the item, e.g. "gh_guid" or "gh_string".
        /// </summary>
        string? TypeName { get; }

        /// <summary>
        /// Type code of the item, e.g. 9 for "gh_guid" or 10 for "gh_string".
        /// </summary>
        int TypeCode { get; }

        /// <summary>
        /// Some items have nested elements with their data
        /// </summary>
        XmlElement[]? AnyElements { get; }

        /// <summary>
        /// Some items contain a text value directly
        /// </summary>
        string? Value { get; }

        /// <summary>
        /// Get an element by its name. Returns the first element found with that name.
        /// Throws in case no element was found.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        XmlElement? GetElementByName(string name, bool throwIfNotFound = true);
    }

    /// <summary>
    /// A list of chunks in a Grasshopper XML archive.
    /// </summary>
    public interface IGhxChunks
    {
        int Count { get; }

        List<IGhxChunk> ChunkList { get; }

        IGhxChunk? GetChunkByName(string name, bool throwIfNotFound = true);

        IEnumerable<IGhxChunk> GetChunksByName(string name);
    }

    /// <summary>
    /// A chunk in a Grasshopper XML archive. As an example, chunks are used to represent components. 
    /// A chunk can contain other chunks, and it can also contain items.
    /// </summary>
    public interface IGhxChunk
    {
        /// <summary>
        /// Name of the chunk. For a component this is "Object". 
        /// Component inputs and outputs are represented by chunks named "param_input" and "param_output".
        /// </summary>
        string ChunkName { get; }

        /// <summary>
        /// Some chunk elements have an optional "index" attribute.
        /// </summary>
        int? Index { get; }

        /// <summary>
        /// Items contained in this chunk.
        /// </summary>
        IGhxItems? Items { get; }

        /// <summary>
        /// Chunks contained in this chunk.
        /// </summary>
        IGhxChunks? Chunks { get; }
    }

    public interface IGhxSources
    {
        /// <summary>
        /// The list of source component GUIDs that are connected to this parameter.
        /// There are two possible cases:
        /// - For connected output parameters, this is <see cref="IGhxChunkParamOutput.InstanceGuid"/>.
        /// - For connected floating parameters (components without specific inputs and outputs),
        ///   this is <see cref="IGhxChunkContainer.InstanceGuid"/>.
        /// </summary>
        IList<Guid> Sources { get; }

        /// <summary>
        /// The number of sources. For output parameters this is typically 0.
        /// </summary>
        int SourceCount { get; }
    }

    #region Specific chunks

    /// <summary>
    /// A chunk that contains attributes for a component or a parameter.
    /// </summary>
    public interface IGhxChunkAttributes : IGhxChunk
    {
        IGhxItemDrawingPointf? Pivot { get; }
        IGhxItemDrawingRectanglef? Bounds { get; }
    }

    /// <summary>
    /// A chunk that represents a parameter input of a component.
    /// </summary>
    public interface IGhxChunkParamInput : IGhxChunk, IGhxSources
    {
        /// <summary>
        /// Index of the parameter input.
        /// </summary>
        new int Index { get; }

        IGhxChunkAttributes Attributes { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.GH_ParamAccess"/>
        /// 0 - item
        /// 1 - list
        /// 2 - tree
        /// </summary>
        int? Access { get; }

        string Description { get; }

        Guid InstanceGuid { get; }

        bool? Mutable { get; }

        string ItemName { get; }

        string ItemNickName { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.IGH_Param.Optional"/>
        /// Gets or sets whether or not this parameter is considered optional by the owner
        /// component. Empty, non-optional parameters prevent the component from being solved.
        /// </summary>
        bool Optional { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.GH_DataMapping"/>
        /// 0 - item
        /// 1 - flatten
        /// 2 - graft
        /// </summary>
        int? Mapping { get; }

    }

    /// <summary>
    /// A chunk that represents a parameter output of a component.
    /// </summary>
    public interface IGhxChunkParamOutput : IGhxChunk, IGhxSources
    {
        /// <summary>
        /// Index of the parameter output.
        /// </summary>
        new int Index { get; }

        IGhxChunkAttributes Attributes { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.GH_ParamAccess"/>
        /// 0 - item
        /// 1 - list
        /// 2 - tree
        /// </summary>
        int? Access { get; }

        string Description { get; }

        Guid InstanceGuid { get; }

        string ItemName { get; }

        string ItemNickName { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.IGH_Param.Optional"/>
        /// Gets or sets whether or not this parameter is considered optional by the owner
        /// component. Empty, non-optional parameters prevent the component from being solved.
        /// </summary>
        bool Optional { get; }
    }

    /// <summary>
    /// A chunk that represents the container of a component <see cref="IGhxChunkObject"/>.
    /// </summary>
    public interface IGhxChunkContainer : IGhxChunk, IGhxSources
    {
        IGhxChunkAttributes Attributes { get; }

        /// <summary>
        /// The input parameters of the component.
        /// </summary>
        IList<IGhxChunkParamInput> ParamInputs { get; }

        /// <summary>
        /// Get an input parameter by its name. Returns the first parameter found with that name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="throwIfNotFound"></param>
        /// <returns></returns>
        IGhxChunkParamInput? GetParamInputByName(string name, bool throwIfNotFound = true);

        /// <summary>
        /// The output parameters of the component.
        /// </summary>
        IList<IGhxChunkParamOutput> ParamOutputs { get; }

        /// <summary>
        /// Get an output parameter by its name. Returns the first parameter found with that name.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="throwIfNotFound"></param>
        /// <returns></returns>
        IGhxChunkParamOutput? GetParamOutputByName(string name, bool throwIfNotFound = true);

        /// <summary>
        /// Description of the component instance.
        /// </summary>
        string Description { get; }

        bool? Hidden { get; }

        /// <summary>
        /// The instance GUID of the component, i.e. a unique identifier for this specific component instance.
        /// </summary>
        Guid InstanceGuid { get; }

        /// <summary>
        /// Name of the component instance.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Nickname of the component instance.
        /// </summary>
        string NickName { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.IGH_Param.Optional"/>
        /// Gets or sets whether or not this parameter is considered optional by the owner
        /// component. Empty, non-optional parameters prevent the component from being solved.
        /// </summary>
        bool? Optional { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.IGH_Param.Reverse"/>
        /// </summary>
        bool? ReverseData { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.GH_DataMapping"/>
        /// 0 - item
        /// 1 - flatten
        /// 2 - graft
        /// </summary>
        int? Mapping { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.IGH_Param.Simplify"/>
        /// </summary>
        bool? SimplifyData { get; }

        /// <summary>
        /// <see cref="Grasshopper.Kernel.IGH_ActiveObject.Locked"/>"/>
        /// </summary>
        bool? Locked { get; }

        /// <summary>
        /// For cluster components:
        /// Map of input and output parameter GUIDs to corresponding component GUIDs of the cluster document.
        /// Null for non-cluster components.
        /// </summary>
        IDictionary<Guid, Guid>? ParamMap { get; }

        /// <summary>
        /// For cluster components: Parsed cluster definition. Null otherwise.
        /// Note: In case a cluster appears multiple times in a Grasshopper XML archive,
        /// the same instance of the cluster definition will be reused.
        /// </summary>
        IGhxChunkDefinition? ClusterDefinition { get; }

        /// <summary>
        /// A hash that uniquely identifies the cluster definition.
        /// Null if this is not a cluster definition.
        /// </summary>
        Guid? ClusterHash { get; }

        /// <summary>
        /// For cluster inputs and outputs: Custom description.
        /// </summary>
        string? CustomDescription { get; }

        /// <summary>
        /// x inputs and outputs: Custom name.
        /// </summary>
        string? CustomName { get; }

        /// <summary>
        /// For cluster inputs and outputs: Custom nickname.
        /// </summary>
        string? CustomNickName { get; }
    }

    /// <summary>
    /// A chunk that represents a library in the Grasshopper XML archive.
    /// </summary>
    public interface IGhxChunkLibrary : IGhxChunk
    {
        new int Index { get; }

        string? AssemblyFullName { get; }

        string? AssemblyVersion { get; }

        string Author { get; }


        /// <summary>
        /// Library id.
        /// </summary>
        Guid Id { get; }

        string Name { get; }

        string Version { get; }
    }

    /// <summary>
    /// A chunk that contains the libraries in the Grasshopper XML archive.
    /// </summary>
    public interface IGhxChunkGhaLibraries : IGhxChunk
    {

        IList<IGhxChunkLibrary> Libraries { get; }

        int LibraryCount { get; }
    }

    /// <summary>
    /// A chunk that represents a component in the Grasshopper XML archive.
    /// </summary>
    public interface IGhxChunkObject : IGhxChunk
    {
        new int Index { get; }

        IGhxChunkContainer Container { get; }

        /// <summary>
        /// Component GUID, i.e. a unique identifier for the component type.
        /// </summary>
        Guid Guid { get; }

        /// <summary>
        /// Optional library GUID, i.e. a unique identifier for the library that contains the component.
        /// </summary>
        Guid? Lib { get; }

        /// <summary>
        /// Name of the component type.
        /// </summary>
        string DefinitionName { get; }
    }

    /// <summary>
    /// A chunk that contains the components in the Grasshopper XML archive.
    /// </summary>
    public interface IGhxChunkDefinitionObjects : IGhxChunk
    {
        /// <summary>
        /// The list of components in the definition.
        /// </summary>
        IList<IGhxChunkObject> Objects { get; }

        int ObjectCount { get; }
    }

    /// <summary>
    /// A chunk that contains the document header in the Grasshopper XML archive.
    /// </summary>
    public interface IGhxChunkDocumentHeader : IGhxChunk
    {
        Guid DocumentID { get; }
    }

    /// <summary>
    /// A chunk that contains the definition properties in the Grasshopper XML archive.
    /// </summary>
    public interface IGhxChunkDefinitionProperties : IGhxChunk
    {
        DateTime Date { get; }

        string Description { get; }

        string Name { get; }
    }

    /// <summary>
    /// A chunk that contains the definition of a Grasshopper XML archive.
    /// </summary>
    public interface IGhxChunkDefinition : IGhxChunk
    {
        IGhxItemVersion PluginVersion { get; }

        /// <summary>
        /// Chunk that contains the components in the Grasshopper XML archive
        /// </summary>
        IGhxChunkDefinitionObjects DefinitionObjects { get; }

        IGhxChunkGhaLibraries? Libraries { get; }

        IGhxChunkDocumentHeader DocumentHeader { get; }

        IGhxChunkDefinitionProperties DefinitionProperties { get; }

    }

    #endregion

    #region Specific items
    public interface IGhxItemDrawingPointf : IGhxItem
    {
        float PivotX { get; }
        float PivotY { get; }
    }

    public interface IGhxItemDrawingRectanglef : IGhxItem
    {
        float X { get; }
        float Y { get; }
        float W { get; }
        float H { get; }
    }

    public interface IGhxItemVersion : IGhxItem
    {
        Version Version { get; }
    }

    #endregion

}
