using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using GraphRoots.Grasshopper.Parser;
using Microsoft.Extensions.DependencyInjection;

namespace GraphRoots.GraphDbCli
{
    internal class Program
    {
        static async Task<int> Main(string[] args)
        {
            using var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };

            var types = Assembly.GetAssembly(typeof(Program))!.GetTypes()
               .Where(t => typeof(ICommand).IsAssignableFrom(t) && !t.IsInterface)
               .OrderBy(t => t.Name)
               .ToArray();

            try
            {
                await using var services = CliServices.CreateProvider();
                var parser = services.GetRequiredService<IGhxArchiveParser>();
                var result = Parser.Default.ParseArguments(args, types);
                if (result.Errors.Any())
                    return 1;

                if (result.Value is ICommand command)
                {
                    if (command is ImportCommand import)
                        return await import.Execute(parser, cts.Token);
                    return await command.Execute(cts.Token);
                }

                return 1;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Cancelled.");
                return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine($"{Environment.NewLine}Error: {e.Message}");
                Console.WriteLine($"{Environment.NewLine}Complete exception info: {e}");
                return 1;
            }
        }
    }
}
