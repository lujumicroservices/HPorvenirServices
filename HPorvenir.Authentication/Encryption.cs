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

    }
}
