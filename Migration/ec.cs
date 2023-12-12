using Nest;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Migration.Doc;
/*
namespace Migration
{
    public class ec
    {

        public static List<Doc> Get(string Id, long date, string Coords, string Name, string Content)
        {


            var client = new ElasticClient(settings);
            double searchaftervalue = 0;

            var responseCount = 1;

            while (responseCount > 0)
            {

                var request = new SearchRequest<Doc>("migration_1919-1930")
                {
                    Size = 1,
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

                if (response.IsValid)
                {




                    var doc = response.Documents.LastOrDefault() as Doc;
                    responseCount = response.Documents.Count();

                    Console.WriteLine(responseCount.ToString());

                    searchaftervalue = Convert.ToDouble(doc.Id);
                    Console.WriteLine(searchaftervalue);

                }

            }


        }
    }
}

*/