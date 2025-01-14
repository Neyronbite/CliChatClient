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
        List<MessageModel> _queuedToBeDecryptedMessages;

        Action<MessageModel> _displayMessage;
        Action<string> _displayError;
        Action<string> _displayWarning;

        public MessageService(DataAccess dataAccess, Context context, HTTPService httpService)
        {
            _context = context;
            _dataAccess = dataAccess;
            _httpService = httpService;
            _queuedToSendMessages = new List<MessageModel>();
            _queuedToBeDecryptedMessages = new List<MessageModel>();
        }

        public async ValueTask DisposeAsync()
        {
            await _dataAccess.UserKey.Update(_userKey.Username, _userKey);
            await _dataAccess.DisposeAsync();
            await _connection.StopAsync();
        }

        public async void Init(Action<MessageModel> displayMessage, Action<string> displayError, Action<string> displayWarning)
        {
            _displayError = displayError;
            _displayWarning = displayWarning;
            _displayMessage = displayMessage;

            // SignalR connection options
            _connection = new HubConnectionBuilder()
                .WithUrl(_context.BaseUrl + "/chat",
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_context.Token);
                    if (_context.IgnoreSSL)
                    {
                        options.HttpMessageHandlerFactory = (message) =>
                        {
                            if (message is HttpClientHandler clientHandler)
                            {
                                // always verify the SSL certificate
                                clientHandler.ServerCertificateCustomValidationCallback +=
                                    (sender, certificate, chain, sslPolicyErrors) => { return true; };
                            }
                            return message;
                        };
                    }
                })
                .Build();

            // Handlers

            // Define the handler for receiving messages
            _connection.On<string, string>("ReceiveMessage", async (user, message) =>
            {
                if (!_userKey.UsersSymetricKeys.ContainsKey(user)) 
                {
                    await RequireForgotenPrivateKey(user);

                    _queuedToBeDecryptedMessages.Add(new MessageModel() { From = user, To = _context.LoggedUsername, Message = message });
                }
                else
                {
                    var resultMessage = DecryptMessage(user, message);

                    _displayMessage(resultMessage);
                }
            });

            // Define handeler for key exchange process
            _connection.On<string, KeyExchangeModel>("KeyExchange", async (user, keyExchange) =>
            {
                // If user wants new key, we send new key
                if (keyExchange.NewKeyRequest)
                {
                    await SendNewPrivateKey(user);

                    // displaying message about key exchange
                    _displayMessage(new MessageModel() { From = user, To = _context.LoggedUsername, Message = "Key exchange successfull", IsNotification = true });
                }
                // If user sent new key, we add new key to _userKey object
                else if (keyExchange.NewKeyResponse)
                {
                    var keyDecoded = keyExchange.PrivateKey.Decode();
                    var keyDecrypted = _RSAEncryptionHelper.Decrypt(keyDecoded);

                    if (_userKey.UsersSymetricKeys.ContainsKey(user))
                    {
                        _userKey.UsersSymetricKeys[user] = keyDecrypted;
                    }
                    else 
                    {
                        _userKey.UsersSymetricKeys.Add(user, keyDecrypted);
                    }

                    // displaying message about key exchange
                    _displayMessage(new MessageModel() { From = user, To = _context.LoggedUsername, Message = "Key exchange successfull", IsNotification = true });
                }
                // If user want old key
                else if (keyExchange.ForgotKeyRequest)
                {
                    if (_userKey.UsersSymetricKeys.ContainsKey(user))
                    {
                        // sending key
                        await SendPrivateKey(user, _userKey.UsersSymetricKeys[user]);
                    }
                    else
                    {
                        // sending new key
                        await SendNewPrivateKey(user);
                    }

                    // displaying message about key exchange
                    _displayMessage(new MessageModel() { From = user, To = _context.LoggedUsername, Message = "Key exchange successfull", IsNotification = true });
                }

                // Checking queued messages, if they exists, we send them
                var userQueuedMessages = _queuedToSendMessages.Where(m => m.To == user);

                foreach (var message in userQueuedMessages) 
                {
                    await SendMessage(user, message.Message);
                    _queuedToSendMessages.Remove(message);
                }

                var userMessagesToBeDecrypted = _queuedToBeDecryptedMessages.Where(m => m.From == user);

                foreach (var message in userMessagesToBeDecrypted)
                {
                    var decryptedMessageModel = DecryptMessage(user, message.Message);
                    displayMessage(decryptedMessageModel);
                    _queuedToBeDecryptedMessages.Remove(message);
                }
            });

            // Define handeler for server side error handling
            _connection.On<string, string>("ReceiveError", (u, e) =>
            {
                _displayError(e);
            });

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

            //TODO after implementing message queueing on server, implement getting messages, and displaying

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
        }

        /// <summary>
        /// main message sending logic
        /// </summary>
        /// <param name="user"></param>
        /// <param name="message"></param>
        /// <returns></returns>
        public async Task SendMessage(string user, string message)
        {
            try
            {
                if (!_userKey.UsersSymetricKeys.ContainsKey(user))
                {
                    // if we dont have exchanged key on message sending, we require new key from user whum we want to send message
                    await RequirePrivateKey(user);
                    // and adding message to queue
                    // we will check queue after key exchange
                    _queuedToSendMessages.Add(new MessageModel { From = _context.LoggedUsername, Message = message, To = user });
                }
                else
                {
                    // if we already have key, we send message
                    await EncryptMessageAndSend(user, message);

                    _displayMessage(new MessageModel()
                    {
                        From = _context.LoggedUsername,
                        To = user,
                        Message = message
                    });
                }
            }
            catch (Exception e)
            {
                _displayError(e.Message);
            }
        }

        /// <summary>
        /// Before using this function, check _userKey.UsersSymetricKeys[user]
        /// 
        /// decrypting user's message using exchanged private key
        /// if something goes wrong in process
        /// it marks message as error, setting HasError to true, and adding error to message
        /// in UI we can just print it, and it will not be presented as normal message
        /// </summary>
        /// <param name="user">user who sent message</param>
        /// <param name="message">message text</param>
        /// <returns></returns>
        private MessageModel DecryptMessage(string user, string message)
        {
            var resultMessage = new MessageModel();

            try
            {
                var symetricKey = _userKey.UsersSymetricKeys[user];
                var aesHelper = new AESEncryptionHelper(symetricKey);

                var decodedMessage = message.Decode();
                var decryptedMessage = aesHelper.Decrypt(decodedMessage);

                //TODO message validation

                resultMessage.Message = decryptedMessage;
                resultMessage.From = user;
                resultMessage.To = _context.LoggedUsername;
            }
            catch (Exception e)
            {
                resultMessage.Message = e.Message;
                resultMessage.HasError = true;
                resultMessage.From = user;
                resultMessage.To = _context.LoggedUsername;
            }

            return resultMessage;
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

            var encryptedMessage = aesHelper.Encrypt(message);
            var encodedMessage = encryptedMessage.Encode();

            await _connection.InvokeAsync("SendMessage", encodedMessage, user);
        }

        #region Key Exchange
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
        /// sending to user request to send old AES key
        /// </summary>
        /// <param name="user">user's username</param>
        /// <returns></returns>
        private async Task RequireForgotenPrivateKey(string user)
        {
            var keyExchange = new KeyExchangeModel()
            {
                ForgotKeyRequest = true,
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
            var key = AESEncryptionHelper.GenerateKey();
            
            await SendPrivateKey(user, key);

            if (_userKey.UsersSymetricKeys.ContainsKey(user))
            {
                _userKey.UsersSymetricKeys[user] = key;
            }
            else
            {
                _userKey.UsersSymetricKeys.Add(user, key);
            }
        }

        /// <summary>
        /// encrypting, encoding private key, and sending to user
        /// </summary>
        /// <param name="user">username</param>
        /// <param name="key">AES key</param>
        /// <returns></returns>
        private async Task SendPrivateKey(string user, string key)
        {
            var keyEncoded = await EncryptEncodeKey(user, key);

            var keyExchange = new KeyExchangeModel()
            {
                NewKeyResponse = true,
                PrivateKey = keyEncoded,
                Username = _context.LoggedUsername
            };

            await _connection.InvokeAsync("KeyExchange", keyExchange, user);
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

        /// <summary>
        /// getting user's public key, and encrypting (then encoding) new key
        /// </summary>
        /// <param name="user">username</param>
        /// <param name="key">AES key</param>
        /// <returns></returns>
        private async Task<string> EncryptEncodeKey(string user, string key)
        {
            string pubKey = await GetUsersPubKey(user);

            var rsaHelper = new RSAEncryptionHelper(pubKey, "");
            var keyEncrypted = rsaHelper.Encrypt(key);
            var keyEncoded = keyEncrypted.Encode();

            return keyEncoded;
        }
        #endregion
    }
}


// TODO obshi ste kareli er ujex architecture gcel,
// vor hnaravorutyun tar apaga avel huberi kpnely hesht arver
// bayc
// zahla chka