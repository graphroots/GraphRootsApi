using System.Threading.Tasks;
using HotChocolate.Execution;
using Microsoft.Extensions.DependencyInjection;

namespace GraphRoots.GraphApi;

public static class SchemaExport
{
    public static async Task<string> Print()
    {
        var services = new ServiceCollection();
        services.AddGraphRootsGraphQL();
        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequiredService<IRequestExecutorResolver>().GetRequestExecutorAsync();
        return executor.Schema.Print();
    }
}
