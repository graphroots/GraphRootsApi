using System;
using System.IO;
using System.Text;
using GraphRoots.GraphDb;
using GraphRoots.Grasshopper.Parser;

namespace GraphRoots.Grasshopper
{
    /// <summary>
    /// Grasshopper import context: file artifact plus parsed GHX archive.
    /// </summary>
    public class LoaderContext : IGhxLoaderContext
    {
        public LoaderContext(IGhxArchive ghxArchive, string filePath)
            : this(ghxArchive, filePath, File.ReadAllBytes(filePath))
        {
        }

        public LoaderContext(IGhxArchive ghxArchive, string filePath, byte[] content)
        {
            GhxArchive = ghxArchive;
            FileCreationTimeUtc = File.GetCreationTimeUtc(filePath);
            FileLastWriteTimeUtc = File.GetLastWriteTimeUtc(filePath);
            FileName = Path.GetFileName(filePath);
            FilePath = filePath;
            VersionId = Uuid5.Generate(VersionIdNamespace, content);
        }

        /// <summary>
        /// Read the file once, parse it, and hash the same bytes for <see cref="VersionId"/>.
        /// </summary>
        public static LoaderContext FromFile(IGhxArchiveParser parser, string path)
        {
            var bytes = File.ReadAllBytes(path);
            var archive = string.Equals(Path.GetExtension(path), ".gh", StringComparison.OrdinalIgnoreCase)
                ? parser.ParseArchiveByteArray(bytes)
                : parser.ParseArchiveString(Encoding.UTF8.GetString(bytes));
            return new LoaderContext(archive, path, bytes);
        }

        static Guid VersionIdNamespace = Guid.Parse("65BA840B-4FC0-4EC0-86D4-CF9226B23895");

        public IGhxArchive GhxArchive { get; }

        public Guid VersionId { get; }

        public DateTime FileCreationTimeUtc { get; }

        public DateTime FileLastWriteTimeUtc { get; }

        public string FileName { get; }

        public string FilePath { get; }
    }
}
