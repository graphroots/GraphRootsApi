using System;
using System.IO;
using System.Threading.Tasks;
using GraphRoots.GraphApi;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraphRoots.GraphApiTests;

[TestClass]
public class SchemaSnapshotTest
{
    [TestMethod]
    public async Task PrintedSchema_MatchesContractSdl()
    {
        var printed = await SchemaExport.Print();
        var contractPath = ContractPath();
        Assert.IsTrue(File.Exists(contractPath), $"Missing contract at {contractPath}");
        var contract = await File.ReadAllTextAsync(contractPath);

        var expected = SchemaContract.Canonical(contract);
        var actual = SchemaContract.Canonical(printed);
        Assert.AreEqual(expected, actual, Diff(expected, actual));
    }

    static string Diff(string expected, string actual)
    {
        var e = expected.Split('\n');
        var a = actual.Split('\n');
        var n = Math.Max(e.Length, a.Length);
        var lines = new System.Collections.Generic.List<string>();
        for (var i = 0; i < n && lines.Count < 40; i++)
        {
            var left = i < e.Length ? e[i] : "<missing>";
            var right = i < a.Length ? a[i] : "<missing>";
            if (left != right)
                lines.Add($"L{i + 1}\n  expected: {left}\n  actual:   {right}");
        }
        return string.Join("\n", lines);
    }

    static string ContractPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "docs", "graphql", "schema.graphql");
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "docs", "graphql", "schema.graphql"));
    }
}
