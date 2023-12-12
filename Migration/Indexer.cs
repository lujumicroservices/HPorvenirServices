using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*namespace Migration
{
    public class Indexer
    {
        ConnectionSettings ConnectionSettings;
        ElasticClient client;
     
        public Indexer() {

            ConnectionSettings = new ConnectionSettings(new Uri("https://40.124.185.84:9200/"))
               .DefaultIndex("hporvenir_1919-1930")
               .DisableDirectStreaming()
               .ApiKeyAuthentication("eeoYHowB5SerKtpAEWzN", "201YWmlbQaC0rYGD8PeS6g");

            ConnectionSettings.ServerCertificateValidationCallback((a, b, c, d) => true);

            client = new ElasticClient(ConnectionSettings);

        }

        public async Task Index()
        {

            var Search  = new Searcher();

            var documentstoindex = await Search.Search();
            


            var bulkDescriptor = new BulkDescriptor();

            foreach (var document in documentstoindex)
            {
                var d1 = new Doc
                {
                    Id = document.Id,
                    Name = document.Name,
                    Coords = document.Coords,
                    Date = document.Date,
                    Content = document.Content
                };
                d1.Id = document.Id;

                // Assuming document has an ID property
                bulkDescriptor.Index<Doc>(op => op
                    .Document(d1)
                    .Id((Id)d1.Id) // Specify the document ID
                );
            }



            var indexManyResponse = client.Bulk(bulkDescriptor);


        }


    }
}
*/