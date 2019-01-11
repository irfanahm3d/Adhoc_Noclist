using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Adhoc.Noclist
{
    class BadsecClient
    {
        internal const string AuthFailureMessage = "Auth failed";
        internal const string UserListRetrievalFailureMessage = "User list retrieval failed";

        internal const string AuthTokenHeader = "Badsec-Authentication-Token";
        internal const string ChecksumRequestHeader = "X-Request-Checksum";
        internal const string BadsecUri = "http://localhost:8888/";
        internal const string UsersEndpoint = "users";
        internal const string AuthEndpoint = "auth";
        internal const int RetryLimit = 3;

        private readonly HttpClient Client;

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
        public BadsecClient(HttpClient client)
        {
            this.Client = client;
            this.Client.BaseAddress = new Uri(BadsecUri);
        }

        /// <summary>
        /// Gets the list of users.
        /// </summary>
        /// <returns>A string containing a list of users</returns>
        public async Task<string> GetUsersList()
        {
            string userList = String.Empty;

            HttpResponseMessage authResponse = await GetAuth();
            if (authResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception(AuthFailureMessage);
            }

            string authToken = RetrieveTokenHeader(authResponse);
            string checksum = ComputeSHA256ChecksumFromToken(authToken);

            HttpResponseMessage userListResponse = await GetUsers(checksum);
            if (userListResponse.StatusCode != System.Net.HttpStatusCode.OK)
            {
                throw new Exception(UserListRetrievalFailureMessage);
            }

            userList = await userListResponse.Content.ReadAsStringAsync();
            return ConvertStringToJson(userList);
        }
        
        /// <summary>
        /// Get call to the /auth endpoint.
        /// </summary>
        /// <returns>
        /// An HttpResponse with only headers that contains the authorization token
        /// </returns>
        internal async Task<HttpResponseMessage> GetAuth()
        {
            return await GetHttpWithRetry(
                AuthEndpoint,
                HttpCompletionOption.ResponseHeadersRead);
        }

        /// <summary>
        /// Get call to the /users endpoint.
        /// </summary>
        /// <param name="checksum">The checksum required for authorization</param>
        /// <returns>A complete HttpResponse containing the user list</returns>
        internal async Task<HttpResponseMessage> GetUsers(string checksum)
        {
            return await GetHttpWithRetry(
                UsersEndpoint,
                HttpCompletionOption.ResponseContentRead,
                checksum);
        }

        /// <summary>
        /// Retrieves the token from the response header.
        /// </summary>
        /// <param name="response">The HttpResponse</param>
        /// <returns>The authorization token</returns>
        internal string RetrieveTokenHeader(HttpResponseMessage response)
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
        internal string ComputeSHA256ChecksumFromToken(string authToken)
        {
            using (SHA256 crypto = SHA256.Create())
            {
                byte[] hash = crypto.ComputeHash(
                    Encoding.UTF8.GetBytes(String.Concat(authToken, "/", UsersEndpoint)));

                StringBuilder checksum = new StringBuilder();
                for (int i = 0; i < hash.Length; i++)
                {
                    checksum.Append(hash[i].ToString("x2"));
                }

                return checksum.ToString();
            }
        }

        /// <summary>
        /// Converts the newline delimited string into a json format.
        /// </summary>
        /// <param name="list">A newline delimited list of user ids</param>
        /// <returns>A json format user id list</returns>
        internal string ConvertStringToJson(string stringList)
        {
            string[] userIds = 
                stringList.Split(new []{ '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            StringBuilder jsonString = new StringBuilder();
            jsonString.Append("[");
            for (int idx = 0; idx < userIds.Length; idx++)
            {
                if (idx == userIds.Length - 1)
                {
                    // don't add the comma and space for the last entry
                    jsonString.Append(String.Concat("\"", userIds[idx], "\"")); 
                }
                else
                {
                    jsonString.Append(String.Concat("\"", userIds[idx], "\"", ", "));
                }                
            }
            jsonString.Append("]");

            return jsonString.ToString();
        }

        internal async Task<HttpResponseMessage> GetHttpWithRetry(
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
                        break;
                    }
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }

                retry++;
            } while (retry < RetryLimit);

            if (response == null &&
                exceptions.Count != 0)
            {
                throw new AggregateException(exceptions);
            }

            return response;
        }

        internal async Task<HttpResponseMessage> HttpRequest(
            HttpRequestMessage request,
            HttpCompletionOption completionOption)
        {
            try
            {
                return await this.Client.SendAsync(request, completionOption);
            }
            catch (TaskCanceledException taskException)
            {
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
