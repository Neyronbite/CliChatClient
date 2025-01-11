using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Helpers
{
    using System;
    using System.Security.Cryptography;
    using System.Text;

    public class RSAEncryptionHelper
    {
        private readonly string _publicKey;
        private readonly string _privateKey;

        // Constructor without arguments: creates public and private keys
        public RSAEncryptionHelper()
        {
            (string pubKey, string privKey) = GenerateKeys();
            _publicKey = pubKey;
            _privateKey = privKey;
        }

        // Constructor with arguments: accepts public and private keys as strings
        public RSAEncryptionHelper(string pubKey, string privKey)
        {
            _publicKey = pubKey;
            _privateKey = privKey;
        }

        // Static method to generate a pair of public and private keys
        public static (string publicKey, string privateKey) GenerateKeys()
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.KeySize = 2048;

                // Export the public and private keys
                string publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());
                string privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());

                return (publicKey, privateKey);
            }
        }

        // Encrypt method: Encrypts plain text using the public key
        public string Encrypt(string plainText)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPublicKey(Convert.FromBase64String(_publicKey), out _);

                byte[] data = Encoding.UTF8.GetBytes(plainText);
                byte[] encryptedData = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
                return Convert.ToBase64String(encryptedData);
            }
        }

        // Decrypt method: Decrypts encrypted text using the private key
        public string Decrypt(string encryptedText)
        {
            using (RSA rsa = RSA.Create())
            {
                rsa.ImportRSAPrivateKey(Convert.FromBase64String(_privateKey), out _);

                byte[] encryptedData = Convert.FromBase64String(encryptedText);
                byte[] decryptedData = rsa.Decrypt(encryptedData, RSAEncryptionPadding.OaepSHA256);
                return Encoding.UTF8.GetString(decryptedData);
            }
        }
    }

}
