using Lucene.Net.Analysis.Standard;
using Lucene.Net.Index;
using Lucene.Net.Store;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HPorvenir.Search
{
    public class Search
    {

        public void test()
        {


            var indexPath = @"D:\DEV\INDICES\Porvenir_2019";

            

            var analyzer = new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_CURRENT);



            //var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);

            var dir = FSDirectory.Open(indexPath);
            // Create an index writer
            
            using var writer = new IndexWriter(dir,analyzer,IndexWriter.MaxFieldLength.LIMITED);
            //writer.Optimize();
            writer.Flush(true, true, true);
        }
    }
}
