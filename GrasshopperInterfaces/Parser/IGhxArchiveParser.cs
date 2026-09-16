using System.Collections.Generic;
using System.IO;

namespace GraphRoots.Grasshopper.Parser
{
    /// <summary>
    /// Grasshopper archive parsing functionality.
    /// </summary>
    public interface IGhxArchiveParser
    {
        IGhxArchive ParseArchiveStream(Stream xml, IGhxArchiveParserContext? Context = null);

        IGhxArchive ParseArchiveByteArray(byte[] bytes, IGhxArchiveParserContext? Context = null);

        IGhxArchive ParseArchiveString(string xml, IGhxArchiveParserContext? Context = null);

        IGhxArchive ParseArchiveFile(string xmlFilePath, IGhxArchiveParserContext? Context = null);

        IGhxChunkDefinition ParseChunkStream(Stream xml, IGhxArchiveParserContext? Context = null);

        IGhxChunkDefinition ParseChunkByteArray(byte[] bytes, IGhxArchiveParserContext? Context = null);

        IGhxChunkDefinition ParseChunkString(string xml, IGhxArchiveParserContext? Context = null);

        IGhxChunkDefinition ParseChunkFile(string xmlFilePath, IGhxArchiveParserContext? Context = null);
    }

    /// <summary>
    /// Conversion of binary Grasshopper archives to XML.
    /// </summary>
    public interface IGhxArchiveConverter
    {
        string ConvertByteArrayToXml(byte[] bytes);
    }

    /// <summary>
    /// Context information for the Grasshopper archive parser.
    /// </summary>
    public interface IGhxArchiveParserContext
    {
        IGhxArchiveParser Parser { get; }

        /// <summary>
        /// True if we are parsing a cluster definition.
        /// </summary>
        bool ParsingCluster { get; }

        /// <summary>
        /// Create a context for parsing a cluster definition.
        /// </summary>
        /// <returns></returns>
        IGhxArchiveParserContext CreateClusterContext();

        /// <summary>
        /// Parsed cluster definitions indexed by their unique identifier.
        /// </summary>
        IDictionary<string, IGhxChunkDefinition> ParsedClusters { get; }
    }
}
