using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using GraphRoots.GraphDb;
using GraphRoots.Grasshopper;
using GraphRoots.Grasshopper.Parser;
using Microsoft.Extensions.DependencyInjection;

namespace GraphRoots.GraphDbCli
{
    /// <summary>
    /// Importing one or several Grasshopper models.
    /// </summary>
    [Verb("import", isDefault: false, HelpText = "Import one or several Grasshopper models.")]
    class ImportCommand : BaseCommand, ICommand
    {
        public const string DefaultGraphQlUrl = "http://127.0.0.1:5088/graphql";

        [Option("path", Required = true, HelpText = "Path to the Grasshopper model or directory containing models.")]
        public string ImportPath { get; set; } = "";

        [Option("purge", HelpText = "Purge existing data before importing. Requires --yes.")]
        public bool Purge { get; set; }

        [Option("yes", HelpText = "Confirm a destructive --purge.")]
        public bool Yes { get; set; }

        [Option("graphql-url", HelpText = "GraphQL endpoint (GRAPHQL_URL if not specified, default: http://127.0.0.1:5088/graphql).")]
        public string? GraphQlUrl { get; set; }

        public async Task<int> Execute(CancellationToken cancellationToken = default)
        {
            await using var services = CliServices.CreateProvider();
            return await Execute(services.GetRequiredService<IGhxArchiveParser>(), cancellationToken);
        }

        internal async Task<int> Execute(IGhxArchiveParser parser, CancellationToken cancellationToken = default)
        {
            var files = GetFilesToImport(ImportPath)?.ToList();
            if (files == null || files.Count == 0)
            {
                Console.WriteLine($"No valid files found at path (specify a directory or a .gh or .ghx file): {ImportPath}");
                return 1;
            }

            if (Purge)
            {
                if (!Yes)
                {
                    Console.WriteLine($"Refusing to purge {ResolveNeo4jTarget()} without --yes.");
                    return 1;
                }
                Console.WriteLine($"Purging all data at {ResolveNeo4jTarget()}.");
                await using var driver = GetNeo4jDriver();
                var dbOperations = new DbOperations(driver, ResolveNeo4jDatabase());
                await dbOperations.PurgeDatabase(cancellationToken);
            }

            var endpoint = ResolveGraphQlUrl();
            Console.WriteLine($"Importing via GraphQL at {endpoint}.");
            var builder = new GhxSnapshotBuilder();
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(130) };
            var client = new GraphQlImportClient(http, endpoint);

            var totalFiles = files.Count;
            var fileIndex = 0;
            var failed = 0;
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                fileIndex++;
                Console.WriteLine($"Loading file ({fileIndex}/{totalFiles}): {file}");
                try
                {
                    var context = LoaderContext.FromFile(parser, file);
                    var snapshot = builder.Build(context, cancellationToken);
                    var result = await client.ImportSnapshot(snapshot, cancellationToken);
                    Console.WriteLine(
                        $"Imported {result.Document.DocumentId} version {result.Document.VersionId} ({result.NodeCount} nodes, {result.PortCount} ports, {result.EdgeCount} edges, {result.NestedDocumentCount} nested).");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    failed++;
                    Console.WriteLine($"Failed to import file {fileIndex} of {totalFiles}: {file}");
                    var previousColor = Console.ForegroundColor;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.ToString());
                    Console.ForegroundColor = previousColor;
                }
            }

            return failed == 0 ? 0 : 2;
        }

        Uri ResolveGraphQlUrl()
        {
            var raw = FirstNonEmpty(GraphQlUrl, CliServices.Configuration["GRAPHQL_URL"]) ?? DefaultGraphQlUrl;
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
                throw new InvalidOperationException($"GRAPHQL_URL is not a valid absolute URI: {raw}");
            return uri;
        }

        static string? FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }
            return null;
        }

        IEnumerable<string>? GetFilesToImport(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }

            if (File.Exists(path))
            {
                var extension = Path.GetExtension(path);
                return string.Equals(extension, ".gh", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(extension, ".ghx", StringComparison.OrdinalIgnoreCase)
                    ? new[] { path }
                    : null;
            }

            if (Directory.Exists(path))
            {
                return Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories)
                    .Where(file =>
                    {
                        var extension = Path.GetExtension(file);
                        return string.Equals(extension, ".gh", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(extension, ".ghx", StringComparison.OrdinalIgnoreCase);
                    });
            }

            return null;
        }
    }
}
