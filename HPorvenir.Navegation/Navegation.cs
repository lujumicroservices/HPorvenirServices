using HPorvenir.Model;
using HPorvenir.Storage;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace HPorvenir.Navegation
{
    public class Navegation
    {
        IStorage _storage;
        public Navegation(IStorage storage) {
            _storage = storage;
        }

        public MissingDataModel LoadNavigation() {

            var metadataStream = _storage.GetMetadata();
            MissingDataModel missing = null;
            var stream = _storage.GetMetadata();
            stream.Position = 0;                            
            using (StreamReader reader = new StreamReader(stream))
            {
                var content = reader.ReadToEnd();
                missing = JsonConvert.DeserializeObject<MissingDataModel>(content);
            }
            
            return missing;
        }        
    }
}
