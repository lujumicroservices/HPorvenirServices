using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Search.Spans;
using Lucene.Net.Store.Azure;
using Lucene.Net.Util;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Auth;
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections;
using System.IO;
using Lucene.Net.Store;
using static Lucene.Net.Index.MergePolicy;
using HPorvenir.Parser;

namespace Hporvenir.Indexer
{
    public class IndexClient
    {

        string _indexName;
        string _indexPath;

        IndexWriter _writer;

        public IndexClient(string indexPath, string indexName) {
            _indexName = indexName;
            _indexPath = indexPath;
        }



        public IndexWriter IndexWriter{

            get {

                if (_writer == null) {
                    _writer = GetWriter();
                }
                return _writer;            
            }
        }


        private void CreateIndex() {


            StorageCredentials credentials = new StorageCredentials("hporvenirindex", "NXMwdOr6MM8r0DtdnuDZVtbxVWuJhtRyNNYI4nJaAlbxNPPOjUTl9ZGiU33P4yg9peMR8RDbIvZE5DHDqVe0Fw==");

            CloudStorageAccount storage = new CloudStorageAccount(credentials,true);

            AzureDirectory dir = new AzureDirectory(storage, "indextest");

            // Create an analyzer to process the text
            const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
            var analyzer = new StandardAnalyzer(AppLuceneVersion);

            // Create an index writer
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);

            //IndexWriter indexWriter = new IndexWriter(dir, new StandardAnalyzer(), true);
            var writer = new IndexWriter(dir, indexConfig);

            var source = new
            {
                Name = "Kermit the Frog",
                FavoritePhrase = "The quick brown fox jumps over the lazy dog"
            };

            var doc = new Document
            {
                // StringField indexes but doesn't tokenize
                new StringField("name",
                    source.Name,
                    Field.Store.YES),
                new TextField("favoritePhrase",
                    source.FavoritePhrase,
                    Field.Store.YES)
            };
            writer.AddDocument(doc);
            writer.Flush(true, true);

        }

        private IndexWriter GetWriter() {

            
            // Create an analyzer to process the text
            const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
            var analyzer = new StandardAnalyzer(AppLuceneVersion);

            // Create an index writer
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);

            //IndexWriter indexWriter = new IndexWriter(dir, new StandardAnalyzer(), true);

            System.IO.Directory.CreateDirectory(Path.Combine(_indexPath, _indexName));

            var dir = FSDirectory.Open(Path.Combine(_indexPath, _indexName));
            var writer = new IndexWriter(dir, indexConfig);
           
            return writer;
        }


        public bool IndexMany(List<Paragraph> documents) {

            foreach (var p in documents) {

                var doc = new Document
                {                 
                new Int32Field("date",p.Date,Field.Store.YES),
               
                new StringField("coords",p.Coords,Field.Store.YES),
                new StringField("name",p.Name,Field.Store.YES),
                new TextField("content",p.Content,Field.Store.NO),
                };

                IndexWriter.AddDocument(doc);
            }
                     
            return true;
        }

        public void Search() {

            StorageCredentials credentials = new StorageCredentials("hporvenirindex", "NXMwdOr6MM8r0DtdnuDZVtbxVWuJhtRyNNYI4nJaAlbxNPPOjUTl9ZGiU33P4yg9peMR8RDbIvZE5DHDqVe0Fw==");
            CloudStorageAccount storage = new CloudStorageAccount(credentials, true);

            // Create an analyzer to process the text
            const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
            var analyzer = new StandardAnalyzer(AppLuceneVersion);

            // Create an index writer
            //

            var workingReader = new List<IndexReader>();
            
            for (int i = 1919; i < 1930; i++) {
                AzureDirectory dir = new AzureDirectory(storage, i.ToString());
                workingReader.Add(IndexReader.Open(dir));
            }

            var multiReader = new MultiReader(workingReader.ToArray());
            var search = new IndexSearcher(multiReader);

            var phrase = new MultiPhraseQuery
            {
                
                new Term("contenido", "exacta"),
                new Term("contenido", "ALEGORÍA".ToLower())

            };

            var phrase2 = new PhraseQuery();
            //create a term to search file name
            

            SpanQuery[] clauses = new SpanQuery[2];
            clauses[0] = new SpanMultiTermQueryWrapper<FuzzyQuery>(new FuzzyQuery(new Term("contenido", "exacta")));
            clauses[1] = new SpanMultiTermQueryWrapper<FuzzyQuery>(new FuzzyQuery(new Term("contenido", "alegoria")));            
            SpanNearQuery query = new SpanNearQuery(clauses, 0, true);

            //var parser = QueryParser("Title", new StandardAnalyzer());
            //var query = parser.Parse("Title:(Dog AND Cat)");

            var hits = search.Search(query, 10).ScoreDocs;

            foreach (var hit in hits)
            {
                var foundDoc = search.Doc(hit.Doc);
                var w = 0;
            }            
        }

        public void MergeIndex(int start, int end, string mergeName) {


            string mergePath = @"E:\MergeIndex";
            string sourcePath = @"E:\Indices";
            
            // Create an analyzer to process the text
            const LuceneVersion AppLuceneVersion = LuceneVersion.LUCENE_48;
            var analyzer = new StandardAnalyzer(AppLuceneVersion);

            // Create an index writer
            var indexConfig = new IndexWriterConfig(AppLuceneVersion, analyzer);
            var dir = FSDirectory.Open(Path.Combine(mergePath, mergeName));

            Console.WriteLine("Loading source indixces");
            var sourceIndexes = new List<Lucene.Net.Store.FSDirectory>();
            for (int i = start; i <= end; i++) {
                Console.WriteLine("open index " + i.ToString());
                sourceIndexes.Add(FSDirectory.Open(Path.Combine(sourcePath, "Porvenir_" + i.ToString())));
            }



            var writer = new IndexWriter(dir, indexConfig);


            Console.WriteLine("apending indexes");

            writer.AddIndexes(sourceIndexes.ToArray());

            Console.WriteLine("Optimizing index...");            
            Console.WriteLine("Closing Readers...");

            

            //writer.Merge();
            writer.Flush(true, true);            
            writer.Commit();
            writer.Dispose();

            Console.WriteLine("done");

        }    
    
    
    
    }
}
