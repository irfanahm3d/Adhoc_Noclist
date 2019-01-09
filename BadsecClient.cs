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
        private const int RetryLimit = 3;

        /// <summary>
        /// A list of time delays to be used for the retry methodology.
        /// As retries increase the delay for the retry also increases.
        /// </summary>
        private IReadOnlyList<int> TimeDelays = new List<int>
        {
            0, //ms
            3000, //ms
            7000 //ms
        };

        /// <summary>
        /// Constructor
        /// </summary>
        public BadsecClient()
        {
            client.BaseAddress = new Uri(BaseUri);
        }

        /// <summary>
        /// Gets the list of users.
        /// </summary>
        /// <returns>A string containing a list of users</returns>
        public async Task<string> GetUsers()
        {
            string userList = String.Empty;
            HttpResponseMessage authResponse = await GetAuth();
            string authToken = RetrieveTokenHeader(authResponse);
            string checksum = ComputeSHA256ChecksumFromToken(authToken);
            HttpResponseMessage userListResponse = await GetUserList(checksum);
            if (userListResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                userList = await userListResponse.Content.ReadAsStringAsync();
            }

            return JsonConvert.SerializeObject(userList, Formatting.Indented);
        }
        
        /// <summary>
        /// Get call to the /auth endpoint.
        /// </summary>
        /// <returns>
        /// An HttpResponse with only headers that contains the authorization token
        /// </returns>
        async Task<HttpResponseMessage> GetAuth()
        {
            return await GetHttpWithRetry(
                AuthUriPath,
                HttpCompletionOption.ResponseHeadersRead);
        }

        /// <summary>
        /// Get call to the /user endpoint.
        /// </summary>
        /// <param name="checksum">The checksum required for authorization</param>
        /// <returns>A complete HttpResponse containing the user list</returns>
        async Task<HttpResponseMessage> GetUserList(string checksum)
        {
            return await GetHttpWithRetry(
                UsersUriPath,
                HttpCompletionOption.ResponseContentRead,
                checksum);
        }

        /// <summary>
        /// Retrieves the token from the response header.
        /// </summary>
        /// <param name="response">The HttpResponse</param>
        /// <returns>The authorization token</returns>
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

        /// <summary>
        /// Computes a checksum using the SHA256 algorithm from the authorization token.
        /// </summary>
        /// <param name="authToken">The authorization token</param>
        /// <returns>A string containing the checksum</returns>
        string ComputeSHA256ChecksumFromToken(string authToken)
        {
            using (SHA256 crypto = SHA256.Create())
            {
                byte[] hash = crypto.ComputeHash(
                    Encoding.UTF8.GetBytes(String.Concat(authToken, "/", UsersUriPath)));

                StringBuilder checksum = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    checksum.Append(hash[i].ToString("x2"));
                }

                return checksum.ToString();
            }
        }

        async Task<HttpResponseMessage> GetHttpWithRetry(
            string uriPath,
            HttpCompletionOption completionOption,
            string checksum = "")
        {
            int retry = 0;
            HttpResponseMessage response = null;
            IList<Exception> exceptions = new List<Exception>();

            do
            {
                await Task.Delay(TimeDelays[retry]);
                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, uriPath);
                if (!String.IsNullOrWhiteSpace(checksum))
                {
                    request.Headers.Add(ChecksumRequestHeader, checksum);
                }

                try
                {
                    response = await HttpRequest(request, completionOption);
                    if (response.StatusCode == System.Net.HttpStatusCode.OK)
                    {
                        return response;
                    }
                    else
                    {
                        throw new Exception();
                    }
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }

                retry++;
            } while (retry < RetryLimit);

            throw new AggregateException(exceptions);
        }

        async Task<HttpResponseMessage> HttpRequest(
            HttpRequestMessage request,
            HttpCompletionOption completionOption)
        {
            try
            {
                return await client.SendAsync(request, completionOption);
            }
            catch (TaskCanceledException taskException)
            {
                if (!taskException.CancellationToken.IsCancellationReques‌​ted)
                {
                    throw new Exception("Timeout expired trying to reach the ButterCMS service.");
                }
                throw taskException;
            }
            catch (HttpRequestException httpException)
            {
                throw httpException;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
