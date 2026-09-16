using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GraphRoots.GraphDb;

namespace GraphRoots.GraphDbCli
{
    sealed class GraphQlImportClient
    {
        const string Mutation = """
            mutation ImportSnapshot($input: ImportSnapshotInput!) {
              importSnapshot(input: $input) {
                document { documentId versionId origin committed }
                nestedDocumentCount
                nodeCount
                portCount
                edgeCount
              }
            }
            """;

        static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
        };

        readonly HttpClient _http;
        readonly Uri _endpoint;

        public GraphQlImportClient(HttpClient http, Uri endpoint)
        {
            _http = http;
            _endpoint = endpoint;
        }

        public async Task<ImportSnapshotResult> ImportSnapshot(ImportSnapshot snapshot, CancellationToken cancellationToken)
        {
            var body = new Dictionary<string, object?>
            {
                ["query"] = Mutation,
                ["operationName"] = "ImportSnapshot",
                ["variables"] = new Dictionary<string, object?>
                {
                    ["input"] = GraphQlSnapshotEncoder.Encode(snapshot),
                },
            };
            var json = JsonSerializer.Serialize(body, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException($"GraphApi is not reachable at {_endpoint}.", ex);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException($"GraphApi request to {_endpoint} timed out.", ex);
            }

            var payload = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"GraphApi returned {(int)response.StatusCode} from {_endpoint}: {payload}");

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                var first = errors[0];
                var message = first.TryGetProperty("message", out var msg) ? msg.GetString() : payload;
                var code = first.TryGetProperty("extensions", out var ext) && ext.TryGetProperty("code", out var codeEl)
                    ? codeEl.GetString()
                    : null;
                throw new InvalidOperationException(string.IsNullOrEmpty(code) ? message : $"{code}: {message}");
            }

            var data = root.GetProperty("data").GetProperty("importSnapshot");
            var doc = data.GetProperty("document");
            return new ImportSnapshotResult
            {
                Document = new Document
                {
                    DocumentId = doc.GetProperty("documentId").GetString() ?? "",
                    VersionId = Guid.Parse(doc.GetProperty("versionId").GetString() ?? Guid.Empty.ToString()),
                    Origin = OriginFromGql(doc.GetProperty("origin").GetString()),
                    Committed = doc.GetProperty("committed").GetBoolean(),
                },
                NestedDocumentCount = data.GetProperty("nestedDocumentCount").GetInt32(),
                NodeCount = data.GetProperty("nodeCount").GetInt32(),
                PortCount = data.GetProperty("portCount").GetInt32(),
                EdgeCount = data.GetProperty("edgeCount").GetInt32(),
            };
        }

        static string OriginFromGql(string? origin) => origin switch
        {
            "GRASSHOPPER" => GraphOrigins.Grasshopper,
            "DYNAMO" => GraphOrigins.Dynamo,
            "GRAPHROOTS" => GraphOrigins.GraphRoots,
            _ => origin ?? "",
        };
    }
}
