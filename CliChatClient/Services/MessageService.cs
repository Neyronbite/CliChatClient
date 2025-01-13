using CliChatClient.Data;
using CliChatClient.Models;
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

        string _baseUrl;

        DataAccess _dataAccess;

        public MessageService(DataAccess dataAccess)
        {
            _dataAccess = dataAccess;
        }

        public async ValueTask DisposeAsync()
        {
            await _connection.StopAsync();
        }

        public async void Init(string baseUrl, string jwtToken, Action<string, string> handleMessageReceive)
        {
            _baseUrl = baseUrl;

            _connection = new HubConnectionBuilder()
                .WithUrl(_baseUrl + "/chat",
                options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(jwtToken);
                })
                .Build();

            // Handlers

            // Define the handler for receiving messages
            _connection.On<string, string>("ReceiveMessage", (user, message) =>
            {
                //TODO message validation, encryption
                handleMessageReceive(user, message);
            });
            _connection.On<string, KeyExchangeModel>("KeyExchange", (user, keyExchange) =>
            {
                //TODO implement
            });
            //TODO Error handler
            //TODO KeyExchangeHandler

            await _connection.StartAsync();
        }

        public async Task SendMessage(string user, string message)
        {
            //TODO errors
            //TODO encryption
            //TODO key exchange
            await _connection.InvokeAsync("SendMessage", message, user);
        }
    }
}


//TODO obshi ste kareli er ujex architecture gcel,
//vor hnaravorutyun tar apaga avel huberi kpnely hesht arver
//bayc
//zahla chka