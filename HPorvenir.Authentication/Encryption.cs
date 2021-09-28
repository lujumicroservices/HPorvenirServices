using CS_AES_CTR;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace HPorvenir.Authentication
{
    public static class Encryption
    {

        public static string Encode(string data, string key) {

            TripleDESCryptoServiceProvider cryptoServiceProvider = new TripleDESCryptoServiceProvider();
            UnicodeEncoding unicodeEncoding = new UnicodeEncoding();
            ASCIIEncoding asciiEncoding = new ASCIIEncoding();
            byte[] bytes1 = unicodeEncoding.GetBytes(data);
            MemoryStream memoryStream = new MemoryStream();
            byte[] rgbSalt = new byte[1];
            PasswordDeriveBytes passwordDeriveBytes = new PasswordDeriveBytes(key, rgbSalt);
            byte[] bytes2 = passwordDeriveBytes.GetBytes(24);
            cryptoServiceProvider.Key = bytes2;
            cryptoServiceProvider.IV = passwordDeriveBytes.GetBytes(8);
            CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream, cryptoServiceProvider.CreateEncryptor(), CryptoStreamMode.Write);
            cryptoStream.Write(bytes1, 0, bytes1.Length);
            cryptoStream.FlushFinalBlock();
            var x = Encoding.ASCII.GetString(memoryStream.ToArray());
            return Convert.ToBase64String(memoryStream.ToArray());
            
        }

        public static string Decode(string data, string key)
        {
            TripleDESCryptoServiceProvider cryptoServiceProvider = new TripleDESCryptoServiceProvider();
            UnicodeEncoding unicodeEncoding = new UnicodeEncoding();
            ASCIIEncoding asciiEncoding = new ASCIIEncoding();
            byte[] buffer = Convert.FromBase64String(data);
            MemoryStream memoryStream1 = new MemoryStream();
            MemoryStream memoryStream2 = new MemoryStream(buffer);
            byte[] rgbSalt = new byte[1];
            PasswordDeriveBytes passwordDeriveBytes = new PasswordDeriveBytes(key, rgbSalt);
            byte[] bytes = passwordDeriveBytes.GetBytes(24);
            cryptoServiceProvider.Key = bytes;
            cryptoServiceProvider.IV = passwordDeriveBytes.GetBytes(8);
            CryptoStream cryptoStream = new CryptoStream((Stream)memoryStream2, cryptoServiceProvider.CreateDecryptor(), CryptoStreamMode.Read);
            StreamWriter streamWriter = new StreamWriter((Stream)memoryStream1);
            StreamReader streamReader = new StreamReader((Stream)cryptoStream);
            streamWriter.Write(streamReader.ReadToEnd());
            streamWriter.Flush();
            cryptoStream.Clear();
            cryptoServiceProvider.Clear();
            return unicodeEncoding.GetString(memoryStream1.ToArray());            
        }


        public static string DecriptCRT(string keys, string iv,string content) {

            
            // In real world, generate these with cryptographically secure pseudo-random number generator (CSPRNG)
            byte[] key = Encoding.UTF8.GetBytes(keys);
            byte[] initialCounter = StringToByteArrayFastest(iv);

            // Decrypt
            var d = StringToByteArrayFastest(content);
            AES_CTR forDecrypting = new AES_CTR(key, initialCounter);
            byte[] decryptedContent = new byte[d.Length];
            forDecrypting.DecryptBytes(decryptedContent, d);

            // Print 3

            var text = Encoding.Default.GetString(decryptedContent);
            Console.WriteLine($"Decrypted bytes \t\t{text}");
            return text;

        }

        public static byte[] StringToByteArrayFastest(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        public static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            //return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

    }
}
