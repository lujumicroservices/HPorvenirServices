using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/*namespace Migration
{
    public class Searcher
    {
        ConnectionSettings connectionSettings;
        ElasticClient client;
        public string stringvalue;
        public double searchaftervalue = 0;


        public Searcher()
        {

            var settings = new ConnectionSettings(new Uri("https://aa19934ba78e42a5a2677efb2f3f5612.westus2.azure.elastic-cloud.com"))
             .DefaultIndex("hporvenirv2v2_1919-1930") // Replace with your actual index name
             .ApiKeyAuthentication("H_UN0IsB_ovt8jdITJ9l", "21c_a3hCQmSikGaHC7TRHw");

            var client = new ElasticClient(settings);
        }


        public async Task<List<Doc>> Search()
        {
            searchaftervalue = 0;

            var request = new SearchRequest<Doc>("migration_1919-1930")
            {
                Size = 5000,
                Query = new MatchAllQuery(),
                SearchAfter = new List<object>() { searchaftervalue },

                Sort = new List<ISort>
            {
                new FieldSort
                {
                    Field = "id", // Replace with your actual field name
                    Order = SortOrder.Ascending // Replace with your sorting order
                }
            }
            };

                 
            var response = await client.SearchAsync<Doc>(request);

            Console.WriteLine(response.Documents.Count);

              stringvalue = response.Documents.Last().Id.ToString();

            searchaftervalue = response.Documents.Last().Id;
               
            return response.Documents.ToList();
        }
    }

}
*/