using Serilog;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace sandbox
{
    public abstract class ProcessDocument
    {




        public abstract  Task<bool> ExecuteAsync(string fileName);


        protected string CalculateBlobName(string path)
        {

            var pathParts = path.Split('/');

            if (pathParts.Length == 2 && pathParts[0].Length == 8)
            {
                var name = @$"{pathParts[0].Substring(0,4)}\{pathParts[0].Substring(4, 2)}\{pathParts[0].Substring(6, 2)}\{pathParts[0].Substring(0, 4)}_{pathParts[0].Substring(4, 2)}_{pathParts[0].Substring(6, 2)}_{pathParts[1]}";
                return name;
            }

            Log.Error("invalid path structure {path}", path);
            throw new Exception($"invalid path structure {path}");


            
            
        }

        protected string CalculateThumbBlobName(string path, string ext)
        {

            var pathParts = path.Split('/');

            if (pathParts.Length == 2 && pathParts[0].Length == 8)
            {
                var name = @$"{pathParts[0].Substring(0, 4)}\{pathParts[0].Substring(4, 2)}\{pathParts[0].Substring(6, 2)}\thumb\{pathParts[0].Substring(0, 4)}_{pathParts[0].Substring(4, 2)}_{pathParts[0].Substring(6, 2)}_{pathParts[1].Replace(ext,".jpg")}";
                return name;
            }

            Log.Error("invalid path structure {path}", path);
            throw new Exception($"invalid path structure {path}");

        }
    }
}
