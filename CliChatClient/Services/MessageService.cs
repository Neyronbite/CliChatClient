﻿using CliChatClient.Data;
using CliChatClient.Helpers;
using CliChatClient.Models;
using CliChatClient.Utils;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
            await _dataAccess.UserKeys.Update(_userKey.Username, _userKey);
            await _dataAccess.DisposeAsync();
            await _connection.StopAsync();
        }

        public async Task Init(Action<MessageModel> displayMessage, Action<string> displayError, List<MessageModel> queued)
        {
            _displayError = displayError;
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
                // Handling group message part
                // Group messages come with specific user format
                // groupid:username
                if (Regex.IsMatch(user, @"^(\d{1,19}):(.+)$"))
                {
                    var groupId = user.Substring(0, user.IndexOf(':'));
                    var fromUsername = user.Substring(user.IndexOf(":") + 1);

                    if (!_userKey.UsersSymetricKeys.ContainsKey(groupId))
                    {
                        await RequireForgotenGroupKey(fromUsername, groupId);

                        _queuedToBeDecryptedMessages.Add(new MessageModel() { From = fromUsername, To = groupId, Message = message });
                    }
                    else
                    {
                        var resultMessage = DecryptMessage(groupId, message);
                        resultMessage.From = fromUsername;
                        resultMessage.To = groupId;

                        _displayMessage(resultMessage);
                    }
                }
                else if (!_userKey.UsersSymetricKeys.ContainsKey(user))
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

            // Handler that just creates new aes key for group
            _connection.On<GroupKeyExchangeModel>("CreateGroup", (exchObj) =>
            {
                if (exchObj.ServerRequest)
                {
                    var key = AESEncryptionHelper.GenerateKey();

                    if (_userKey.UsersSymetricKeys.ContainsKey(exchObj.GroupId))
                    {
                        _userKey.UsersSymetricKeys[exchObj.GroupId] = key;
                    }
                    else
                    {
                        _userKey.UsersSymetricKeys.Add(exchObj.GroupId, key);
                    }

                    _displayMessage(new MessageModel() { IsNotification = true, Message = "Group Created with group id -> " + exchObj.GroupId });
                }
            });

            // Handler that handles group members key exchange
            _connection.On<GroupKeyExchangeModel>("GroupKeyExchange", async (exchObj) =>
            {
                // if server says us to exchange key with group owner
                if (exchObj.ServerRequest)
                {
                    var exchReqObj = new GroupKeyExchangeModel()
                    {
                        From = _context.LoggedUsername,
                        To = exchObj.OwnerUsername,
                        GroupId = exchObj.GroupId,
                        ExchangeRequest = true
                    };

                    await _connection.InvokeAsync("GroupKeyExchange", exchReqObj);
                    _displayMessage(new MessageModel() 
                    { 
                        From = exchObj.OwnerUsername, 
                        IsNotification = true, 
                        To = _context.LoggedUsername, 
                        Message = "You are invited to a group with ID " + exchObj.GroupId 
                    });
                }
                // If user wants group key, we send group key
                if (exchObj.ExchangeRequest)
                {
                    if (_userKey.UsersSymetricKeys.ContainsKey(exchObj.GroupId))
                    {
                        await SendGroupPrivateKey(exchObj.From, _userKey.UsersSymetricKeys[exchObj.GroupId], exchObj.GroupId);

                        // displaying message about key exchange
                        _displayMessage(new MessageModel() { From = exchObj.GroupId, To = exchObj.From, Message = "Key exchange successfull", IsNotification = true });
                    }
                    else
                    {
                        // displaying error message when we don't found a key
                        // why we don't use DisplayError function?
                        // good question
                        // because I want from and to fields to be displayed
                        _displayMessage(new MessageModel() { From = exchObj.GroupId, To = exchObj.From, Message = "Key exchange failure, key not found", HasError = true });
                    }
                }
                // If user sent new key, we add new key to _userKey object
                else if (exchObj.ExchangeResponse)
                {
                    var keyDecoded = exchObj.PrivateKey.Decode();
                    var keyDecrypted = _RSAEncryptionHelper.Decrypt(keyDecoded);

                    if (_userKey.UsersSymetricKeys.ContainsKey(exchObj.GroupId))
                    {
                        _userKey.UsersSymetricKeys[exchObj.GroupId] = keyDecrypted;
                    }
                    else
                    {
                        _userKey.UsersSymetricKeys.Add(exchObj.GroupId, keyDecrypted);
                    }

                    // displaying message about key exchange
                    _displayMessage(new MessageModel() { From = exchObj.GroupId, To = _context.LoggedUsername, Message = "Key exchange successfull", IsNotification = true });
                }
                // If user want old key
                else if (exchObj.ForgotKeyRequest)
                {
                    if (_userKey.UsersSymetricKeys.ContainsKey(exchObj.GroupId))
                    {
                        // sending key
                        await SendGroupPrivateKey(exchObj.From, _userKey.UsersSymetricKeys[exchObj.GroupId], exchObj.GroupId);
                        // displaying message about key exchange
                        _displayMessage(new MessageModel() { From = exchObj.GroupId, To = exchObj.From, Message = "Key exchange successfull", IsNotification = true });
                    }
                    else
                    {
                        // displaying error as we did before
                        _displayMessage(new MessageModel() { From = exchObj.GroupId, To = exchObj.From, Message = "Key exchange failure, key not found", HasError = true });
                    }

                }

                // Checking queued messages, if they exists, we send them
                var groupQueuedMessages = _queuedToSendMessages.Where(m => m.To == exchObj.GroupId);

                foreach (var message in groupQueuedMessages)
                {
                    await SendMessage(exchObj.GroupId, message.Message);
                    _queuedToSendMessages.Remove(message);
                }

                var groupMessagesToBeDecrypted = _queuedToBeDecryptedMessages.Where(m => m.To == exchObj.GroupId);

                foreach (var message in groupMessagesToBeDecrypted)
                {
                    var decryptedMessageModel = DecryptMessage(exchObj.GroupId, message.Message);
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
            if (_dataAccess.UserKeys[_context.LoggedUsername] == null)
            {
                throw new Exception($"there are no data for {_context.LoggedUsername}, try importing them manually");
            }

            var oldUserKey = _dataAccess.UserKeys[_context.LoggedUsername];

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
                UsersPublicKeys = new Dictionary<string, string>()
            };

            _RSAEncryptionHelper = new RSAEncryptionHelper(_userKey.PublicKey, _userKey.PrivateKey);

            // we need to await main UI init task, to display messages
            await GetMessages(oldUserKey.UsersSymetricKeys, queued);

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
        /// Sending create group to server
        /// </summary>
        /// <param name="users"></param>
        /// <returns></returns>
        public async Task CreateGroup(string[] users)
        {
            await _connection.InvokeAsync("CreateGroup", users);
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
        /// sending request to group member that sends message to send old AES key
        /// </summary>
        /// <param name="user"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private async Task RequireForgotenGroupKey(string user, string groupId)
        {
            var exchObj = new GroupKeyExchangeModel()
            {
                From = _context.LoggedUsername,
                To = user,
                GroupId = groupId,
                ForgotKeyRequest = true
            };

            await _connection.InvokeAsync("GroupKeyExchange", exchObj);
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
        /// encrypting, encoding and sending
        /// </summary>
        /// <param name="user"></param>
        /// <param name="key"></param>
        /// <param name="groupId"></param>
        /// <returns></returns>
        private async Task SendGroupPrivateKey(string user, string key, string groupId)
        {
            var keyEncoded = await EncryptEncodeKey(user, key);

            var exchObj = new GroupKeyExchangeModel()
            {
                ExchangeResponse = true,
                PrivateKey = keyEncoded,
                From = _context.LoggedUsername,
                To = user,
                GroupId = groupId
            };

            await _connection.InvokeAsync("GroupKeyExchange", exchObj);
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

        /// <summary>
        /// Getting queued messages, decrypting, displaying
        /// </summary>
        /// <param name="oldKeys"></param>
        /// <returns></returns>
        private async Task GetMessages(Dictionary<string, string> oldKeys, List<MessageModel> list)
        {
            var messages = await _httpService.GetQueuedMessages();

            foreach (var item in messages)
            {
                if (oldKeys.ContainsKey(item.From) || oldKeys.ContainsKey(item.To))
                {
                    var key = oldKeys.ContainsKey(item.From) ? oldKeys[item.From] : oldKeys[item.To];
                    var aes = new AESEncryptionHelper(key);

                    var decoded = item.Message.Decode();

                    try
                    {
                        var decrypted = aes.Decrypt(decoded);
                        item.Message = decrypted;
                    }
                    catch (Exception e)
                    {
                        item.Message = "Exception while decrypting";
                        item.HasError = true;
                    }

                    list.Add(item);
                }
                else
                {
                    item.Message = "Key not found";
                    item.HasError = true;
                }
            }
        }
    }
}

// TODO obshi ste kareli er ujex architecture gcel,
// vor hnaravorutyun tar apaga avel huberi kpnely hesht arver
// bayc
// zahla chka
