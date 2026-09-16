using GraphRoots.Grasshopper.Parser;

namespace GraphRoots.GraphDbTests
{
    static class TestParserFactory
    {
        public static IGhxArchiveParser Create()
        {
            return new GhxArchiveParser(new GhxArchiveConverter());
        }
    }
}
