using Hporvenir.Indexer;
using HPorvenir.Parser;
using Nest;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using HPorvenir.Model;
using System.Globalization;

namespace HPorvenir.Elastic
{
    public class Searcher
    {

        ElasticClient _client;
        string _indexName;

        public Searcher(string indexName)
        {
            _indexName = indexName;

            var settings = new ConnectionSettings(new Uri("https://aa19934ba78e42a5a2677efb2f3f5612.westus2.azure.elastic-cloud.com:9243")).DefaultIndex(_indexName).ApiKeyAuthentication("TAvQfXoBALKbRWliRmnL", "SoEZ9e7HQZO8gOXF_qHbZg");
            settings.DisableDirectStreaming();
            _client = new ElasticClient(settings);
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


        public List<AggResult> Search(string[] terms,bool phrase, DateTime? startDate = null, DateTime? endDate = null) {

            for (int i = 0; i < terms.Length; i++) {
                terms[i] = RemoveDiacritics(terms[i].ToLower());
            }


            List<QueryContainer> cont = new List<QueryContainer>();
            QueryContainerDescriptor<Paragraph> mustQry = new QueryContainerDescriptor<Paragraph>();
            
            //add time range
            if (startDate.HasValue && endDate.HasValue)
            {
                cont.Add(mustQry.Range(r => r
                        .Field(f => f.Date)
                        .GreaterThan(double.Parse($"{startDate.Value.Year}{startDate.Value.Month.ToString("00")}{startDate.Value.Day.ToString("00")}"))
                        .LessThan(double.Parse($"{endDate.Value.Year}{endDate.Value.Month.ToString("00")}{endDate.Value.Day.ToString("00")}"))
                    ));
            }

            //is phrase
            if (phrase)
            {
                cont.Add(mustQry.MatchPhrase(mp =>
                                    mp.Field("content")
                                    .Query(string.Join(' ', terms))
                                ));
            }
            else
            { //any term
                List<QueryContainer> termsCont = new List<QueryContainer>();
                foreach (var term in terms)
                {
                    termsCont.Add(mustQry.Match(ma => ma
                         .Field(f => f.Content)
                         .Query(term)
                      ));
                }

                cont.Add(mustQry.Bool(b =>
                    b.Should(termsCont.ToArray())));
            }

            var results = _client.Search<Paragraph>(s => s
                .Query(q =>
                    q.Bool(b => b.Must(cont.ToArray()))
                 ).Aggregations(ag =>
                        ag.Terms("fileagg", term => term.Field("name").Size(500))
                ).Size(0));

            List<AggResult> searchResults = new List<AggResult>();

            if (results.Aggregations.Count > 0) {
                var aggresult = results.Aggregations.Values.First() as BucketAggregate;
                foreach (var item in aggresult.Items) {
                    searchResults.Add(new AggResult { Name = (item as KeyedBucket<object>).Key.ToString() });
                }                
            }

            return searchResults;
        }


        public List<Paragraph> FileDetails(string filename, string[] terms, bool phrase, DateTime? startDate = null, DateTime? endDate = null) {


            List<QueryContainer> cont = new  List<QueryContainer>();
            QueryContainerDescriptor<Paragraph> mustQry = new QueryContainerDescriptor<Paragraph>();

            //add filename
            cont.Add(mustQry.Match(m => m
                                   .Field(f => f.Name)
                                   .Query(filename)
                                ));

            //add time range
            if (startDate.HasValue && endDate.HasValue)
            {
                cont.Add(mustQry.Range(r => r
                        .Field(f => f.Date)
                        .GreaterThan(double.Parse($"{startDate.Value.Year}{startDate.Value.Month.ToString("00")}{startDate.Value.Day.ToString("00")}"))
                        .LessThan(double.Parse($"{endDate.Value.Year}{endDate.Value.Month.ToString("00")}{endDate.Value.Day.ToString("00")}"))
                    ));
            }

            //is phrase
            if (phrase)
            {
                cont.Add(mustQry.MatchPhrase(mp =>
                                    mp.Field("content")
                                    .Query(string.Join(' ', terms))
                                ));                                
            }
            else
            { //any term
                List<QueryContainer> termsCont = new List<QueryContainer>();
                foreach (var term in terms)
                {
                    termsCont.Add(mustQry.Match(ma => ma
                         .Field(f => f.Content)
                         .Query(term)
                      ));
                }

                cont.Add(mustQry.Bool(b => 
                    b.Should(termsCont.ToArray())));
            }
            
            var results = _client.Search<Paragraph>(s => s
                .Query(q => 
                    q.Bool(b => b.Must(cont.ToArray()))
                 ).Size(10));

            return results.Hits.Select(x=> x.Source).ToList();
        }                
    }
}
