using Azure.Storage.Blobs;
using Microsoft.VisualBasic.CompilerServices;
using System;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace HPorvenir.Blob
{
    class Program
    {


        static async System.Threading.Tasks.Task Main(string[] args)
        {
            Console.WriteLine(args[0]);
            Console.WriteLine(args[1]);


            BlobManager manager = new BlobManager();
            manager.initIndexBlob("");
            manager.exportIndex();


            //getpasswords("gc3/h0kIvetxD6Se3V+azw==", "<CET>Digix.S.A.Gdl.Jalisco.Mx</CET>");
            //Migrate(args);

            //transformimages();
            //EvaluateFiles(args);         
        }

        static async System.Threading.Tasks.Task Migrate(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine(args[0]);
            Console.WriteLine(args[1]);


            BlobManager manager = new BlobManager(args[0],int.Parse(args[1]));
            await manager.MigrateData();
            Console.WriteLine("container created");

        }


        static void EvaluateFiles(string[] args)
        {
            Console.WriteLine("Hello World!");
            Console.WriteLine(args[0]);
            Console.WriteLine(args[1]);



            ImagesManager manager = new ImagesManager(args[0], args[1], DateTime.Parse(args[2]), DateTime.Parse(args[3]));
            manager.MapData();
            Console.WriteLine("container created");

        }


        static void transformimages() {

            var trans = new TransformImages();
            trans.Execute();

        }


        static void getpasswords(string user, string privatekey) {


            DataSet dataSet = new DataSet();
            int num = (int)dataSet.ReadXml("c:\\dev2\\CET.xml");
            var str = StringType.FromObject(dataSet.Tables[0].Rows[0]["CET"]);

            RSACryptoServiceProvider cryptoServiceProvider;
            
            cryptoServiceProvider = new RSACryptoServiceProvider();
            UnicodeEncoding unicodeEncoding = new UnicodeEncoding();
            cryptoServiceProvider.FromXmlString(string.Format("<RSAKeyValue><Modulus>{0}</Modulus><Exponent>{1}</Exponent></RSAKeyValue>", str, "AQAB"));
            byte[] rgb = Convert.FromBase64String(user);
            byte[] bytes = cryptoServiceProvider.Decrypt(rgb, false);
            Console.WriteLine(unicodeEncoding.GetString(bytes));
            
            cryptoServiceProvider.Clear();
            
        }


    
    }
}
