


using System;
using System.Xml.Linq;
using Migration;
using Nest;
using static System.Net.Mime.MediaTypeNames;
using static Migration.Doc;


//Connection Settings

var test = new ElasticsearchMigration();

await test.RunMigration();

