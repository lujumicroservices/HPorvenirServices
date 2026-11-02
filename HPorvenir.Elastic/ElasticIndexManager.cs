using HPorvenir.Parser;
using Nest;
using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HPorvenir.Elastic
{
    public class ElasticIndexManager
    {
        private readonly ElasticClient _client;
        private readonly ElasticOptions _options;

        public ElasticIndexManager(ElasticClient client, ElasticOptions options)
        {
            _client = client;
            _options = options;
        }

        public async Task EnsureIndexAsync(string indexName, CancellationToken cancellationToken = default)
        {
            var exists = await _client.Indices.ExistsAsync(indexName, ct: cancellationToken);
            if (exists.Exists)
            {
                Log.Information("Elasticsearch index {IndexName} already exists", indexName);
                return;
            }

            var analyzer = string.IsNullOrWhiteSpace(_options.ContentAnalyzer)
                ? "spanish"
                : _options.ContentAnalyzer;

            var create = new CreateIndexDescriptor(indexName)
                .Settings(s => s
                    .NumberOfShards(_options.NumberOfShards)
                    .NumberOfReplicas(_options.NumberOfReplicas));

            if (string.Equals(analyzer, "spanish_folded", StringComparison.OrdinalIgnoreCase))
            {
                create = create.Settings(s => s
                    .NumberOfShards(_options.NumberOfShards)
                    .NumberOfReplicas(_options.NumberOfReplicas)
                    .Analysis(a => a
                        .Analyzers(an => an
                            .Custom(
                                "spanish_folded",
                                ca => ca
                                    .Tokenizer("standard")
                                    .Filters("lowercase", "asciifolding", "spanish_stemmer")))));
            }

            create = create.Map<Paragraph>(m => m
                .Properties(p => p
                    .Number(n => n.Name(nn => nn.Date).Type(NumberType.Integer))
                    .Keyword(k => k.Name(n => n.Id))
                    .Keyword(k => k.Name(n => n.Name))
                    .Keyword(k => k.Name(n => n.Coords))
                    .Text(t => t
                        .Name(n => n.Content)
                        .Analyzer(analyzer))));

            var response = await _client.Indices.CreateAsync(create, cancellationToken);

            if (!response.IsValid)
            {
                throw new Exception(
                    $"Failed to create index {indexName}: {response.ServerError?.Error?.Reason ?? response.OriginalException?.Message}");
            }

            Log.Information(
                "Created Elasticsearch index {IndexName} with analyzer {Analyzer}",
                indexName,
                _options.ContentAnalyzer);
        }

        public async Task SwapSearchAliasAsync(string aliasName, string newIndexName, CancellationToken cancellationToken = default)
        {
            var aliasResponse = await _client.Indices.GetAliasAsync(
                Indices.All,
                a => a.Name(aliasName),
                cancellationToken);

            var bulk = new BulkAliasDescriptor();

            if (aliasResponse.IsValid)
            {
                foreach (var indexName in aliasResponse.Indices.Keys)
                {
                    bulk.Remove(r => r.Index(indexName.Name).Alias(aliasName));
                }
            }

            bulk.Add(a => a.Index(newIndexName).Alias(aliasName));

            var response = await _client.Indices.BulkAliasAsync(bulk, cancellationToken);
            if (!response.IsValid)
            {
                throw new Exception(
                    $"Failed to swap alias {aliasName} to {newIndexName}: {response.ServerError?.Error?.Reason ?? response.OriginalException?.Message}");
            }

            Log.Information("Alias {AliasName} now points to {IndexName}", aliasName, newIndexName);
        }

        public async Task PublishYearToSearchAliasAsync(
            string aliasName,
            string indexName,
            string indexNamePrefix,
            int year,
            CancellationToken cancellationToken = default)
        {
            var legacyRangeIndex = IndexerSettingsLoader.BuildLegacyRangeIndexName(indexNamePrefix, year);
            var currentTargets = await GetAliasTargetsAsync(aliasName, cancellationToken);
            var bulk = new BulkAliasDescriptor();
            var changed = false;

            if (currentTargets.Contains(legacyRangeIndex, StringComparer.OrdinalIgnoreCase))
            {
                bulk.Remove(r => r.Index(legacyRangeIndex).Alias(aliasName));
                changed = true;
                Log.Information(
                    "Removing legacy range index {LegacyIndex} from alias {AliasName}",
                    legacyRangeIndex,
                    aliasName);
            }

            if (!currentTargets.Contains(indexName, StringComparer.OrdinalIgnoreCase))
            {
                bulk.Add(a => a.Index(indexName).Alias(aliasName));
                changed = true;
            }

            if (!changed)
            {
                Log.Information(
                    "Alias {AliasName} already includes {IndexName}",
                    aliasName,
                    indexName);
                return;
            }

            var response = await _client.Indices.BulkAliasAsync(bulk, cancellationToken);
            if (!response.IsValid)
            {
                throw new Exception(
                    $"Failed to publish {indexName} to alias {aliasName}: {response.ServerError?.Error?.Reason ?? response.OriginalException?.Message}");
            }

            Log.Information(
                "Alias {AliasName} now includes year index {IndexName}",
                aliasName,
                indexName);
        }

        public async Task<IReadOnlyCollection<string>> GetAliasTargetsAsync(string aliasName, CancellationToken cancellationToken = default)
        {
            var aliasResponse = await _client.Indices.GetAliasAsync(
                Indices.All,
                a => a.Name(aliasName),
                cancellationToken);

            if (!aliasResponse.IsValid)
            {
                return Array.Empty<string>();
            }

            return aliasResponse.Indices.Keys.Select(k => k.Name).ToList();
        }
    }
}
