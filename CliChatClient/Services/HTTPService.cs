using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CliChatClient.Models;
using static System.Collections.Specialized.BitVector32;
using CliChatClient.Utils;

namespace CliChatClient.Services
{
    public class HTTPService
    {
        HttpClient _httpClient;

        Context _context;

        public HTTPService(Context context)
        {
            _context = context;

            _httpClient = new HttpClient()
            {
                BaseAddress = new Uri(_context.BaseUrl)
            };
        }

        /// <summary>
        /// gets user's public RSA key
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public async Task<string> GetUsersPubKey(string username)
        {
            var resp = await Get<dynamic>($"api/user/{username}");
            return resp.publicKey;
        }

        /// <summary>
        /// if user is connected to hub, returns true
        /// </summary>
        /// <param name="username"></param>
        /// <returns></returns>
        public async Task<string> GetUserIsOnline(string username)
        {
            var resp = await Get<dynamic>($"api/user/{username}");
            return resp.isOnline;
        }

        /// <summary>
        /// sending login request, returning jwt token
        /// </summary>
        /// <param name="username">username to login</param>
        /// <param name="password">password to login</param>
        /// <returns></returns>
        public async Task<string> Authenticate(string username, string password)
        {
            var resp = await Post<dynamic>("api/auth/login",
                new
                {
                    id = 0,
                    username = username,
                    password = password
                });

            // TODO ste arji register model avelcnel, bayc de vren
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer " + resp.token);
            return resp.token;
        }

        /// <summary>
        /// sending register request, returning jwt token
        /// </summary>
        /// <param name="username">username to register</param>
        /// <param name="password">password to register</param>
        /// <param name="pubKey">user's generated public key</param>
        /// <returns></returns>
        public async Task<string> Register(string username, string password, string pubKey)
        {
            var resp =  await Put<dynamic>("api/auth/register",
                new
                {
                    username = username,
                    password = password,
                    publicKey = pubKey.Encode()
                });

            // TODO ste arji user model avelcnel, bayc de vren
            _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer " + resp.token);
            return resp.token;
        }

        /// <summary>
        /// http post request
        /// </summary>
        /// <typeparam name="TReturn">return type</typeparam>
        /// <param name="action">api action</param>
        /// <param name="parameters">body parameters</param>
        /// <returns></returns>
        private async Task<TReturn> Post<TReturn>(string action, object parameters)
        {
            using HttpResponseMessage response = await _httpClient
                .PostAsync(action, JsonContent.Create(parameters));

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();

            var tReturn = JsonConvert.DeserializeObject<TReturn>(jsonResponse);

            return tReturn;
        }

        /// <summary>
        /// http put request
        /// </summary>
        /// <typeparam name="TReturn">return type</typeparam>
        /// <param name="action">api action</param>
        /// <param name="parameters">body parameters</param>
        /// <returns></returns>
        private async Task<TReturn> Put<TReturn>(string action, object parameters)
        {
            using HttpResponseMessage response = await _httpClient
                .PutAsync(action, JsonContent.Create(parameters));

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();

            var tReturn = JsonConvert.DeserializeObject<TReturn>(jsonResponse);

            return tReturn;
        }

        /// <summary>
        /// http get request
        /// send uri parameters inside action string
        /// </summary>
        /// <typeparam name="TReturn">return type</typeparam>
        /// <param name="action">api action including parameters</param>
        /// <returns></returns>
        private async Task<TReturn> Get<TReturn>(string action)
        {
            using HttpResponseMessage response = await _httpClient
               .GetAsync(action);

            response.EnsureSuccessStatusCode();

            var jsonResponse = await response.Content.ReadAsStringAsync();

            var tReturn = JsonConvert.DeserializeObject<TReturn>(jsonResponse);

            return tReturn;
        }
    }
}
