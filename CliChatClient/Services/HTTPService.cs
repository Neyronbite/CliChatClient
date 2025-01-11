using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Services
{
    public class HTTPService
    {
        HttpClient _httpClient;

        string _baseUrl;

        public HTTPService(string baseUrl)
        {
            _baseUrl = baseUrl;

            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(baseUrl)
            };
        }

        public async Task<string> Authenticate(string username, string password)
        {
            using HttpResponseMessage response = await _httpClient
                .PostAsync("api/auth/login", JsonContent.Create(
                new
                {
                    id = 0,
                    username = username,
                    password = password
                }));

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(jsonResponse);
            return json.token;

            //TODO ste arji user model avelcnel, bayc de vren
        }

        public async Task<string> Register(string username, string password, string pubKey)
        {
            using HttpResponseMessage response = await _httpClient
                .PostAsync("api/auth/register", JsonContent.Create(
                new
                {
                    id = 0,
                    username = username,
                    password = password,
                    pubKey = pubKey
                }));

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();
            dynamic json = JsonConvert.DeserializeObject(jsonResponse);
            return json.token;

            //TODO ste arji user model avelcnel, bayc de vren
        }
    }
}
