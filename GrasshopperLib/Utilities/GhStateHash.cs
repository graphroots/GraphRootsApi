using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace GraphRoots.Grasshopper.Parser
{
    /// <summary>
    /// Computes <see cref="GhExtensionKeys.StateHash"/> from a component's <c>Object</c> chunk.
    /// Items the graph already stores elsewhere (names, layout, wiring) are left out, at every depth, and so is
    /// <c>Selected</c>, the canvas selection flag Grasshopper writes into <c>Attributes</c>: selecting a component
    /// and saving does not change the definition.
    /// </summary>
    /// <remarks>
    /// Origin: GrasshopperDigestor (GraphHop, https://github.com/graphhop/GrasshopperDigestor). The
    /// "Settings/state" comparison this replaces (<c>SharedRhino/GhVersionDiff.cs</c>) is Konrad Zaremba's
    /// modification in his copy of that project (https://github.com/KonradZaremba/GrasshopperDigestor) and
    /// compares raw object XML. GraphRoots does not keep that XML, so the normalised state is hashed at
    /// import instead, and wiring (<c>Source</c>) is excluded because <c>EDGE</c> already records it.
    /// </remarks>
    internal static class GhStateHash
    {
        static readonly HashSet<string> IgnoredItems = ["Name", "NickName", "Pivot", "Bounds", "Selected", "Source", "SourceCount"];

        public static string Compute(IGhxChunk objectChunk)
        {
            var sb = new StringBuilder();
            // The Object chunk's own index is its position in the object list, which shifts whenever an
            // earlier object is added or removed; nested indices (param slots) are meaningful.
            Append(sb, objectChunk, includeIndex: false);
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
        }

        static void Append(StringBuilder sb, IGhxChunk chunk, bool includeIndex)
        {
            sb.Append('[').Append(chunk.ChunkName);
            if (includeIndex && chunk.Index is int index)
                sb.Append('#').Append(index);
            foreach (var item in chunk.Items?.ItemList ?? [])
            {
                if (item.ItemName != null && IgnoredItems.Contains(item.ItemName))
                    continue;
                sb.Append('{').Append(item.ItemName).Append(':').Append(item.TypeName).Append('=').Append(item.Value);
                foreach (var element in item.AnyElements ?? [])
                    sb.Append(element.OuterXml);
                sb.Append('}');
            }
            foreach (var child in chunk.Chunks?.ChunkList ?? [])
                Append(sb, child, includeIndex: true);
            sb.Append(']');
        }
    }
}
