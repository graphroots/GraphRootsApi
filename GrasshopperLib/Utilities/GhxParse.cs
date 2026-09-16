using System.Globalization;

namespace GraphRoots.Grasshopper.Parser
{
    internal static class GhxParse
    {
        public static int Int(string value) => int.Parse(value, CultureInfo.InvariantCulture);

        public static long Long(string value) => long.Parse(value, CultureInfo.InvariantCulture);

        public static float Float(string value) => float.Parse(value, CultureInfo.InvariantCulture);
    }
}
