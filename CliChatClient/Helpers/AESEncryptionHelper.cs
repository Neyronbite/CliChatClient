﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.IO;
using System.Security.Cryptography;

namespace CliChatClient.Helpers
{
    public class AESEncryptionHelper
    {
        private readonly string _privateKey;

        // Constructor without arguments: generates a private key
        public AESEncryptionHelper()
        {
            _privateKey = GenerateKey();
        }

        // Constructor with a private key argument
        public AESEncryptionHelper(string privateKey)
        {
            _privateKey = privateKey;
        }

        // Static method to generate a new AES private key
        public static string GenerateKey()
        {
            using (Aes aes = Aes.Create())
            {
                aes.GenerateKey();
                return Convert.ToBase64String(aes.Key);
            }
        }

        // Encrypt method: Encrypts plain text using AES and the private key
        public string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(_privateKey);
                aes.GenerateIV();

                ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);

                using (MemoryStream ms = new MemoryStream())
                {
                    ms.Write(aes.IV, 0, aes.IV.Length);
                    using (CryptoStream cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                    using (StreamWriter sw = new StreamWriter(cs))
                    {
                        sw.Write(plainText);
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        // Decrypt method: Decrypts encrypted text using AES and the private key
        public string Decrypt(string encryptedText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(_privateKey);

                byte[] cipherText = Convert.FromBase64String(encryptedText);

                using (MemoryStream ms = new MemoryStream(cipherText))
                {
                    byte[] iv = new byte[aes.IV.Length];
                    ms.Read(iv, 0, iv.Length);
                    aes.IV = iv;

                    ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);

                    using (CryptoStream cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                    using (StreamReader sr = new StreamReader(cs))
                    {
                        return sr.ReadToEnd();
                    }
                }
            }
        }
    }

}