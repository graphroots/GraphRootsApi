using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace GraphRoots.Grasshopper.Parser
{
    public class GhxArchiveParser : IGhxArchiveParser
    {
        public GhxArchiveParser(IGhxArchiveConverter archiveConverter)
        {
            Context = new GhxArchiveParserContext(this);
            ArchiveConverter = archiveConverter ?? throw new ArgumentNullException(nameof(archiveConverter));
        }

        IGhxArchiveParserContext Context { get; }

        IGhxArchiveConverter ArchiveConverter { get; }

        public IGhxArchive ParseArchiveString(string xml, IGhxArchiveParserContext? context = null)
        {
            using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                return ParseArchiveStream(memoryStream, context ?? Context);
            }
        }

        public IGhxArchive ParseArchiveByteArray(byte[] bytes, IGhxArchiveParserContext? context = null)
        {
            string xml = ArchiveConverter.ConvertByteArrayToXml(bytes);
            return ParseArchiveString(xml, context ?? Context);
        }

        public IGhxArchive ParseArchiveFile(string xmlFilePath, IGhxArchiveParserContext? context = null)
        {
            using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Open))
            {
                if (string.Equals(Path.GetExtension(xmlFilePath), ".gh", StringComparison.OrdinalIgnoreCase))
                {
                    using (MemoryStream memoryStream = new MemoryStream())
                    {
                        fileStream.CopyTo(memoryStream);
                        byte[] bytes = memoryStream.ToArray();
                        string xml = ArchiveConverter.ConvertByteArrayToXml(bytes);
                        return ParseArchiveString(xml, context ?? Context);
                    }
                }

                return ParseArchiveStream(fileStream, context ?? Context);
            }
        }

        public IGhxArchive ParseArchiveStream(Stream stream, IGhxArchiveParserContext? context = null)
        {
            using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8))
            {
                using (XmlReader xmlReader = XmlReader.Create(streamReader))
                {
                    return ParseArchiveXmlReader(xmlReader, context ?? Context);
                }
            }
        }

        private IGhxArchive ParseArchiveXmlReader(XmlReader xmlReader, IGhxArchiveParserContext context)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GhxArchiveXml));
            var xml = serializer.Deserialize(xmlReader) as GhxArchiveXml
                ?? throw new InvalidOperationException("Failed to deserialize GHX archive.");
            return new GhxArchive(xml, context);
        }

        public IGhxChunkDefinition ParseChunkString(string xml, IGhxArchiveParserContext? context = null)
        {
            using (MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(xml)))
            {
                return ParseChunkStream(memoryStream, context ?? Context);
            }
        }

        public IGhxChunkDefinition ParseChunkByteArray(byte[] bytes, IGhxArchiveParserContext? context = null)
        {
            string xml = ArchiveConverter.ConvertByteArrayToXml(bytes);
            return ParseChunkString(xml, context ?? Context);
        }

        public IGhxChunkDefinition ParseChunkFile(string xmlFilePath, IGhxArchiveParserContext? context = null)
        {
            using (FileStream fileStream = new FileStream(xmlFilePath, FileMode.Open))
            {
                return ParseChunkStream(fileStream, context ?? Context);
            }
        }

        public IGhxChunkDefinition ParseChunkStream(Stream stream, IGhxArchiveParserContext? context = null)
        {
            using (StreamReader streamReader = new StreamReader(stream, Encoding.UTF8))
            {
                using (XmlReader xmlReader = XmlReader.Create(streamReader))
                {
                    return ParseChunkXmlReader(xmlReader, context ?? Context);
                }
            }
        }

        private IGhxChunkDefinition ParseChunkXmlReader(XmlReader xmlReader, IGhxArchiveParserContext context)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(GhxChunkXml));
            var xml = serializer.Deserialize(xmlReader) as GhxChunkXml
                ?? throw new InvalidOperationException("Failed to deserialize GHX chunk.");
            return new GhxChunkDefinition(xml, context);
        }
    }

    class GhxArchiveParserContext : IGhxArchiveParserContext
    {
        public GhxArchiveParserContext(IGhxArchiveParser parser)
        {
            Parser = parser;
        }

        public IGhxArchiveParser Parser { get; }

        public bool ParsingCluster { get; private set; }

        public IGhxArchiveParserContext CreateClusterContext()
        {
            return new GhxArchiveParserContext(Parser)
            {
                ParsingCluster = true
            };
        }

        public IDictionary<string, IGhxChunkDefinition> ParsedClusters { get; } = new Dictionary<string, IGhxChunkDefinition>();
    }
}
