using Elasticsearch.Net;
using Nest;
using System;
using System.Collections.Specialized;

namespace HPorvenir.Elastic
{
    public static class ElasticClientFactory
    {
        public static ElasticClient Create(ElasticOptions options, string indexName = null)
        {
            ConnectionSettings settings;
            var apiKey = new ApiKeyAuthenticationCredentials(options.ApiKey);

            if (options.UseElasticCloud && !string.IsNullOrWhiteSpace(options.Url))
            {
                settings = new ConnectionSettings(new Uri(NormalizeUrl(options.Url)))
                    .ApiKeyAuthentication(apiKey);
            }
            else if (options.UseElasticCloud && !string.IsNullOrWhiteSpace(options.CloudId))
            {
                settings = new ConnectionSettings(options.CloudId, apiKey);
            }
            else if (!string.IsNullOrWhiteSpace(options.ApiKey) && options.ApiKey.Contains("="))
            {
                settings = new ConnectionSettings(new Uri(NormalizeUrl(options.Url)))
                    .ApiKeyAuthentication(apiKey);
            }
            else
            {
                settings = new ConnectionSettings(new Uri(NormalizeUrl(options.Url)))
                    .ApiKeyAuthentication(options.ApiKeyId, options.ApiKey);
            }

            // ES 9 rejects compatible-with=7 (HTTP 400). compatible-with=8 works on 8.x and 9.x.
            var compatibilityHeaders = new NameValueCollection
            {
                { "Accept", "application/vnd.elasticsearch+json;compatible-with=8" },
                { "Content-Type", "application/vnd.elasticsearch+json;compatible-with=8" }
            };

            settings
                .DefaultIndex(indexName ?? options.DefaultIndex)
                .DisableDirectStreaming()
                .GlobalHeaders(compatibilityHeaders)
                .RequestTimeout(TimeSpan.FromSeconds(options.RequestTimeoutSeconds))
                .ServerCertificateValidationCallback((sender, certificate, chain, errors) => true);

            return new ElasticClient(settings);
        }

        private static string NormalizeUrl(string url) =>
            url.EndsWith("/") ? url : url + "/";
    }
}
