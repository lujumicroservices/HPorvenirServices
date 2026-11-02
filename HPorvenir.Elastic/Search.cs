using HPorvenir.Model;
using HPorvenir.Parser;
using Nest;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace HPorvenir.Elastic
{
    public class Searcher
    {
        private readonly ElasticClient _client;
        private readonly string _indexName;

        public Searcher()
            : this(null, null)
        {
        }

        public Searcher(string indexName)
            : this(null, indexName)
        {
        }

        public Searcher(ElasticOptions options, string indexName = null)
        {
            var settings = options ?? IndexerSettingsLoader.Load().Elastic;
            _indexName = indexName ?? settings.SearchAlias;
            _client = ElasticClientFactory.Create(settings, _indexName);
        }

        static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        public List<AggResult> Search(string[] terms, bool phrase, DateTime? startDate = null, DateTime? endDate = null)
        {
            for (int i = 0; i < terms.Length; i++)
            {
                terms[i] = RemoveDiacritics(terms[i].ToLower());
            }

            List<QueryContainer> cont = new List<QueryContainer>();
            QueryContainerDescriptor<Paragraph> mustQry = new QueryContainerDescriptor<Paragraph>();

            if (startDate.HasValue && endDate.HasValue)
            {
                cont.Add(mustQry.Range(r => r
                    .Field(f => f.Date)
                    .GreaterThan(double.Parse($"{startDate.Value.Year}{startDate.Value.Month:00}{startDate.Value.Day:00}"))
                    .LessThan(double.Parse($"{endDate.Value.Year}{endDate.Value.Month:00}{endDate.Value.Day:00}"))));
            }

            if (phrase)
            {
                cont.Add(mustQry.MatchPhrase(mp =>
                    mp.Field(f => f.Content)
                        .Query(string.Join(' ', terms))));
            }
            else
            {
                List<QueryContainer> termsCont = new List<QueryContainer>();
                foreach (var term in terms)
                {
                    termsCont.Add(mustQry.Match(ma => ma
                        .Field(f => f.Content)
                        .Query(term)));
                }

                cont.Add(mustQry.Bool(b => b.Should(termsCont.ToArray())));
            }

            var results = _client.Search<Paragraph>(s => s
                .Index(_indexName)
                .Query(q => q.Bool(b => b.Must(cont.ToArray())))
                .Aggregations(ag => ag.Terms("fileagg", term => term.Field(f => f.Name).Size(500)))
                .Size(0));

            EnsureValid(results, "search");

            List<AggResult> searchResults = new List<AggResult>();

            if (results.Aggregations.Count > 0)
            {
                var aggresult = results.Aggregations.Values.First() as BucketAggregate;
                foreach (var item in aggresult.Items)
                {
                    searchResults.Add(new AggResult { Name = (item as KeyedBucket<object>).Key.ToString() });
                }
            }

            return searchResults;
        }

        public List<Paragraph> FileDetails(string filename, string[] terms, bool phrase, DateTime? startDate = null, DateTime? endDate = null)
        {
            List<QueryContainer> cont = new List<QueryContainer>();
            QueryContainerDescriptor<Paragraph> mustQry = new QueryContainerDescriptor<Paragraph>();

            cont.Add(mustQry.Term(t => t.Field(f => f.Name).Value(filename)));

            if (startDate.HasValue && endDate.HasValue)
            {
                cont.Add(mustQry.Range(r => r
                    .Field(f => f.Date)
                    .GreaterThan(double.Parse($"{startDate.Value.Year}{startDate.Value.Month:00}{startDate.Value.Day:00}"))
                    .LessThan(double.Parse($"{endDate.Value.Year}{endDate.Value.Month:00}{endDate.Value.Day:00}"))));
            }

            if (phrase)
            {
                cont.Add(mustQry.MatchPhrase(mp =>
                    mp.Field(f => f.Content)
                        .Query(string.Join(' ', terms))));
            }
            else
            {
                List<QueryContainer> termsCont = new List<QueryContainer>();
                foreach (var term in terms)
                {
                    termsCont.Add(mustQry.Match(ma => ma
                        .Field(f => f.Content)
                        .Query(term)));
                }

                cont.Add(mustQry.Bool(b => b.Should(termsCont.ToArray())));
            }

            var results = _client.Search<Paragraph>(s => s
                .Index(_indexName)
                .Query(q => q.Bool(b => b.Must(cont.ToArray())))
                .Size(10));

            EnsureValid(results, "file details");

            return results.Hits.Select(x => x.Source).ToList();
        }

        private void EnsureValid(ISearchResponse<Paragraph> response, string operation)
        {
            if (response.IsValid)
            {
                return;
            }

            var reason = response.ServerError?.Error?.Reason
                ?? response.OriginalException?.Message
                ?? "unknown Elasticsearch error";

            throw new InvalidOperationException(
                $"Elasticsearch {operation} on '{_indexName}' failed: {reason}",
                response.OriginalException);
        }
    }
}
