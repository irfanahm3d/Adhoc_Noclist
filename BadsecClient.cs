using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Adhoc_Noclist
{
    class BadsecClient
    {
        private static readonly HttpClient client = new HttpClient();
        private const string AuthTokenHeader = "Badsec-Authentication-Token";
        private const string ChecksumRequestHeader = "X-Request-Checksum";
        private const string BaseUri = "http://localhost:8888/";
        private const string UsersUriPath = "users";
        private const string AuthUriPath = "auth";

        public BadsecClient()
        {
            client.BaseAddress = new Uri(BaseUri);
        }

        public async Task GetUsers()
        {
            HttpResponseMessage authResponse = await GetAuth();
            string authToken = RetrieveTokenHeader(authResponse);
            string checksum = ComputeChecksumFromToken(authToken);
            HttpResponseMessage userListResponse = await GetUserList(checksum);
            if (userListResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                string userList = await userListResponse.Content.ReadAsStringAsync();
                string jsonUserList = JsonConvert.SerializeObject(userList, Formatting.Indented);
            }
        }

        async Task<HttpResponseMessage> GetAuth()
        {
            return await client.GetAsync(AuthUriPath, HttpCompletionOption.ResponseHeadersRead);
        }

        async Task<HttpResponseMessage> GetUserList(string checksum)
        {
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, UsersUriPath);
            request.Headers.Add(ChecksumRequestHeader, checksum);
            return await client.SendAsync(request);            
        }

        string RetrieveTokenHeader(HttpResponseMessage response)
        {
            string authToken = String.Empty;
            IEnumerable<string> tokenList = null;
            if (response.Headers.TryGetValues(AuthTokenHeader, out tokenList))
            {
                authToken = tokenList.First();
            }

            return authToken;
        }

        string ComputeChecksumFromToken(string authToken)
        {
            using (SHA256 crypto = SHA256.Create())
            {
                byte[] hash = crypto.ComputeHash(Encoding.UTF8.GetBytes(String.Concat(authToken, "/", UsersUriPath)));

                StringBuilder checksum = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    checksum.Append(hash[i].ToString("x2"));
                }

                return checksum.ToString();
            }
        }
    }
}
