using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nest;
using Migration;

namespace Migration

{
    public class ElasticsearchMigration
    {
        private readonly ElasticClient _sourceClient;
        private readonly ElasticClient _destinationClient;
        private string _searchAfterValue;
        private int _responseCount;
        private int _cycleNum;
        private int _dotCounter;
        private int _cycleId;
        private double _lap;

        public ElasticsearchMigration()
        {
            // Connection Settings for source client
            var sourceSettings = new ConnectionSettings(new Uri("https://aa19934ba78e42a5a2677efb2f3f5612.westus2.azure.elastic-cloud.com"))
                .DefaultIndex("hporvenirv2v2_2005-2021")
                .ApiKeyAuthentication("H_UN0IsB_ovt8jdITJ9l", "21c_a3hCQmSikGaHC7TRHw");

            _sourceClient = new ElasticClient(sourceSettings);

            // Connection Settings for destination client
            var destinationSettings = new ConnectionSettings(new Uri("https://40.124.185.84:9200/"))
                .DefaultIndex("hporvenir_2005-2021")
                .DisableDirectStreaming()
                .ApiKeyAuthentication("eeoYHowB5SerKtpAEWzN", "201YWmlbQaC0rYGD8PeS6g");

            destinationSettings.ServerCertificateValidationCallback((a, b, c, d) => true);

            _destinationClient = new ElasticClient(destinationSettings);

            _searchAfterValue = "0";
            _responseCount = 0;
            _cycleNum = 0;
            _dotCounter = 0;
            _cycleId = 0;
            _lap = 0.0;
        }

        public async Task RunMigration()
        {
            Console.WriteLine("Index Started");
            Console.WriteLine();

            while (_responseCount < 5073)
            {
                var response = await SearchDocumentsAsync();

                if (response.IsValid)
                {
                    
                    var restul2 =  MigrateDocumentsAsync((List<Doc>)response.Documents).Result;
                    _searchAfterValue = response.Documents.Last().Id;
                }

                _responseCount++;
                _cycleNum++;
                _dotCounter++;
                _cycleId++;

                // Progress tracking
                if (_cycleNum >= 17)
                {
                    _lap++;
                    _cycleNum = 0;
                }

                Console.Clear();
                Console.Write(_lap + "%");
                Console.Write("  ///  ");
                Console.Write(_cycleId);
            }

            Console.WriteLine("All Finished");
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }

        private async Task<ISearchResponse<Doc>> SearchDocumentsAsync()
        {
            var request = new SearchRequest<Doc>("hporvenirv2v2_2005-2021")
            {
                Size = 1000,
                Query = new WildcardQuery { 
                Field = new Field("name"),
                Value = "*.pdf"
                
                },
                SearchAfter = new List<object> { _searchAfterValue },

                Sort = new List<ISort>
                {
                    new FieldSort
                    {
                        Field = "id.keyword",
                        Order = SortOrder.Ascending
                    }
                }
            };

            return await _sourceClient.SearchAsync<Doc>(request);
        }

        private Task<BulkResponse> MigrateDocumentsAsync(List<Doc> documents)
        {
            var bulkDescriptor = new BulkDescriptor();

            foreach (var document in documents)
            {
                var docToIndex = new Doc
                {
                    Id = document.Id,
                    Name = document.Name,
                    Coords = document.Coords,
                    Date = document.Date,
                    Content = document.Content
                };

                docToIndex.Id = document.Id;

                bulkDescriptor.Index<Doc>(op => op
                    .Document(docToIndex)
                    .Id(docToIndex.Id)
                );
            }

             return _destinationClient.BulkAsync(bulkDescriptor);
        }
    }
}
