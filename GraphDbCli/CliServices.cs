using GraphRoots.Grasshopper.Parser;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraphRoots.GraphDbCli
{
    static class CliServices
    {
        public static IConfiguration Configuration { get; } = new ConfigurationBuilder()
            .AddUserSecrets(typeof(Program).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        public static ServiceProvider CreateProvider()
        {
            var services = new ServiceCollection();
            services.AddSingleton<IGhxArchiveConverter, GhxArchiveConverter>();
            services.AddSingleton<IGhxArchiveParser, GhxArchiveParser>();
            return services.BuildServiceProvider();
        }
    }
}
