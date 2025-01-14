using CliChatClient.Data;
using CliChatClient.Helpers;
using CliChatClient.Models;
using CliChatClient.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Services
{
    public class MessageService : IAsyncDisposable
    {
        HubConnection _connection;

        DataAccess _dataAccess;
        HTTPService _httpService;
        Context _context;
        UserKey _userKey;

        RSAEncryptionHelper _RSAEncryptionHelper;

        // Messages that need to be sent after key exchange
        List<MessageModel> _queuedToSendMessages;

        public MessageService(DataAccess dataAccess, Context context, HTTPService httpService)
        {
            _context = context;
            _dataAccess = dataAccess;
            _httpService = httpService;
            _queuedToSendMessages = new List<MessageModel>();
        }

        public async ValueTask DisposeAsync()
        {
            await _dataAccess.UserKey.Update(_userKey.Username, _userKey);
            await _dataAccess.DisposeAsync();
            await _connection.StopAsync();
        }

        //TODO add handleErrorDisplay and handleWarningDisplay actions on init
        public async void Init(Action<string, string> handleMessageReceive)
        {
            _connection = new HubConnectionBuilder()
                .WithUrl(_context.BaseUrl + "/chat",
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_context.Token);
                })
                .Build();

            // Handlers

            // Define the handler for receiving messages
            _connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                //Console.WriteLine($"ReceiveMessage - {user}: encoded: {message}"); //TODO delete this
                //TODO message validation, encryption

                var decoded = message.Decode();
                //Console.WriteLine($"Encrypted: {decoded}"); //TODO delete this


                //Console.WriteLine($"!!!!!!!!!!!!Printing all symetric keys"); //TODO delete this
                //foreach (var item in _userKey.UsersSymetricKeys)
                //{
                //    //Console.WriteLine($"{item.Key}: {item.Value}");
                //}
                //TODO not found case
                //Console.WriteLine($"Do we have symetric key: {_userKey.UsersSymetricKeys.ContainsKey(user)}");
                var symetricKey = _userKey.UsersSymetricKeys[user];
                //Console.WriteLine($"Symetric Key: {symetricKey}"); //TODO delete this

                var aesHelper = new AESEncryptionHelper(symetricKey);
                var decrypted = aesHelper.Decrypt(decoded);
                //Console.WriteLine($"Decrypted: {decrypted}"); //TODO delete this

                handleMessageReceive(user, decrypted);
            });

            // Define handeler for key exchange process
            _connection.On<string, KeyExchangeModel>("KeyExchange", async (user, keyExchange) =>
            {
                // If user wants new key, we send new key
                if (keyExchange.NewKeyRequest)
                {
                    //Console.WriteLine($"KeyExchange NewKeyRequest"); //TODO delete this
                    await SendNewPrivateKey(user);
                }
                // If user sent new key, we add new key to _userKey object
                else if (keyExchange.NewKeyResponse)
                {
                    //Console.WriteLine($"KeyExchange NewKeyResponse: encoded: {keyExchange.PrivateKey}"); //TODO delete this
                    var keyDecoded = keyExchange.PrivateKey.Decode();
                    //Console.WriteLine($"Decoded: {keyDecoded}"); //TODO delete this
                    var keyDecrypted = _RSAEncryptionHelper.Decrypt(keyDecoded);
                    //Console.WriteLine($"Decrypted: {keyDecrypted}"); //TODO delete this
                    if (_userKey.UsersSymetricKeys.ContainsKey(user))
                    {
                        _userKey.UsersSymetricKeys[user] = keyDecrypted;
                    }
                    else 
                    {
                        _userKey.UsersSymetricKeys.Add(user, keyDecrypted);
                    }
                }

                // Checking queued messages, if they exists, we send them
                var userQueuedMessages = _queuedToSendMessages.Where(m => m.To == user);

                foreach (var message in userQueuedMessages) 
                {
                    await SendMessage(user, message.Message);
                }
            });

            // TODO Error handler

            // Key validations
            if (_dataAccess.UserKey[_context.LoggedUsername] == null) 
            {
                throw new Exception($"there are no data for {_context.LoggedUsername}, try importing them manually");
            }

            var oldUserKey = _dataAccess.UserKey[_context.LoggedUsername];

            if (string.IsNullOrEmpty(oldUserKey.PrivateKey))
            {
                throw new Exception($"there are no private key for {_context.LoggedUsername}, try importing it manually");
            }

            // creating new UserKey instance without old exchanged keys
            // it need to exchange keys with users again
            _userKey = new UserKey()
            {
                Username = oldUserKey.Username,
                PrivateKey = oldUserKey.PrivateKey,
                PublicKey = oldUserKey.PublicKey,
                UsersSymetricKeys = new Dictionary<string, string>(),
                UsersPublicKeys = oldUserKey.UsersPublicKeys
            };

            _RSAEncryptionHelper = new RSAEncryptionHelper(_userKey.PublicKey, _userKey.PrivateKey);

            // Starting connection
            await _connection.StartAsync();

            //TODO update old keys
        }

        /// <summary>
        /// main message sending logic
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendMessage(string user, string message)
        {
            // TODO errors
            // TODO encryption
            // TODO key exchange

            if (!_userKey.UsersSymetricKeys.ContainsKey(user))
            {
                await RequirePrivateKey(user);
                _queuedToSendMessages.Add(new MessageModel { From = _context.LoggedUsername, Message = message, To = user });
            }
            else
            {
                await EncryptMessageAndSend(user, message);
            }
        }

        /// <summary>
        /// encrypting message using exchanged AES key
        /// and sending it
        /// </summary>
        /// <param name="user">user to send message</param>
        /// <param name="message">message to be sent</param>
        /// <returns></returns>
        private async Task EncryptMessageAndSend(string user, string message) 
        {
            var aesHelper = new AESEncryptionHelper(_userKey.UsersSymetricKeys[user]);
            //Console.WriteLine($"Message To Send: {message}"); //TODO delete this
            var encryptedMessage = aesHelper.Encrypt(message);
            //Console.WriteLine($"Message Encrypting: {encryptedMessage}"); //TODO delete this
            var encoded = encryptedMessage.Encode();
            //Console.WriteLine($"Message Encoding and sending: {encoded}"); //TODO delete this

            await _connection.InvokeAsync("SendMessage", encoded, user);
        }

        /// <summary>
        /// sending to user request to generate and send back new AES key
        /// </summary>
        /// <param name="user">user's username</param>
        /// <returns></returns>
        private async Task RequirePrivateKey(string user)
        {
            //TODO check user existance and is online
            var keyExchange = new KeyExchangeModel()
            {
                NewKeyRequest = true,
                Username = _context.LoggedUsername
            };

            await _connection.InvokeAsync("KeyExchange", keyExchange, user);
        }

        /// <summary>
        /// generating new AES key
        /// encrypting it using user's public RSA key
        /// sending it
        /// </summary>
        /// <param name="user">user's username</param>
        /// <returns></returns>
        private async Task SendNewPrivateKey(string user)
        {
            string pubKey = await GetUsersPubKey(user);
            //Console.WriteLine($"Getting users public key from server: {pubKey}"); //TODO delete this

            var key = AESEncryptionHelper.GenerateKey();
            //Console.WriteLine($"Generating AES key: {key}"); //TODO delete this

            var rsaHelper = new RSAEncryptionHelper(pubKey, "");
            var keyEncrypted = rsaHelper.Encrypt(key);
            //Console.WriteLine($"Encrypting Key: {keyEncrypted}"); //TODO delete this
            var keyEncoded = keyEncrypted.Encode();
            //Console.WriteLine($"Encoding Key and sending: {keyEncoded}"); //TODO delete this

            var keyExchange = new KeyExchangeModel()
            {
                NewKeyResponse = true,
                PrivateKey = keyEncoded,
                Username = _context.LoggedUsername
            };

            await _connection.InvokeAsync("KeyExchange", keyExchange, user);

            _userKey.UsersSymetricKeys.Add(user, key);
        }

        /// <summary>
        /// gets user's public rsa key, if it is not in _userKey object
        /// </summary>
        /// <param name="user">user's username</param>
        /// <returns></returns>
        private async Task<string> GetUsersPubKey(string user)
        {
            if (_userKey.UsersPublicKeys.ContainsKey(user))
            {
                return _userKey.UsersPublicKeys[user];
            }
            else
            {
                //TODO handle user not found error
                var pubKey = await _httpService.GetUsersPubKey(user);
                var pubkeyDecoded = pubKey.Decode();
                _userKey.UsersPublicKeys.Add(user, pubkeyDecoded);

                return pubkeyDecoded;
            }
        }
    }
}


// TODO obshi ste kareli er ujex architecture gcel,
// vor hnaravorutyun tar apaga avel huberi kpnely hesht arver
// bayc
// zahla chka