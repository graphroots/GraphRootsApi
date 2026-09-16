using System;

namespace GraphRoots.Grasshopper.Parser
{
    public class GhxArchiveConverter : IGhxArchiveConverter
    {
        public GhxArchiveConverter()
        {
            GdiPlusNative.Ensure();
        }

        public string ConvertByteArrayToXml(byte[] bytes)
        {
            GdiPlusNative.Ensure();
            try
            {
                var ghArchive = new GH_IO.Serialization.GH_Archive();
                if (!ghArchive.Deserialize_Binary(bytes))
                {
                    throw new InvalidOperationException("Failed to deserialize GH archive from byte array.");
                }
                return ghArchive.Serialize_Xml();
            }
            catch (Exception ex) when (IsMissingGdiPlus(ex))
            {
                throw new InvalidOperationException(
                    "Failed to load libgdiplus, which is required to read bitmaps in binary Grasshopper files. On macOS install it with 'brew install mono-libgdiplus'. On Debian/Ubuntu install 'libgdiplus'.",
                    ex);
            }
        }

        static bool IsMissingGdiPlus(Exception ex)
        {
            for (var current = ex; current != null; current = current.InnerException)
            {
                if (current is DllNotFoundException)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
