using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;

namespace Adhoc.Noclist.Tests
{
    [TestClass]
    public class BadsecClientUnitTests
    {
        [TestMethod]
        public void ComputeSHA256ChecksumFromTokenTest_ChecksumExpected_Success()
        {
            Mock<HttpClient> moqHttpClient = new Mock<HttpClient>(MockBehavior.Strict);
            BadsecClient client = new BadsecClient(moqHttpClient.Object);
            const string AuthToken = "12A63255-1388-AB5E-071C-FA35D27C4098";
            const string ExpectedResult = "782fbde2c6619f69b4280e14c9ff09fa1a82506eb8d6f79e6843f97f0de3d43a";
               
            string checksum = client.ComputeSHA256ChecksumFromToken(AuthToken);
            Assert.AreEqual(
                expected: ExpectedResult,
                actual: checksum,
                message: "The checksum is not the same as expected.");
        }

        [TestMethod]
        public void ComputeSHA256ChecksumFromTokenTest_ChecksumNotExpected_Success()
        {
            Mock<HttpClient> moqHttpClient = new Mock<HttpClient>(MockBehavior.Strict);
            BadsecClient client = new BadsecClient(moqHttpClient.Object);
            const string AuthToken = "9D11C948-ED41-277F-C5F6-C76FFCE7CD86";
            const string NotExpectedResult = "782fbde2c6619f69b4280e14c9ff09fa1a82506eb8d6f79e6843f97f0de3d43a";


            string checksum = client.ComputeSHA256ChecksumFromToken(AuthToken);
            Assert.AreNotEqual(
                notExpected: NotExpectedResult,
                actual: checksum,
                message: "The checksum is the same as expected.");
        }

        [TestMethod]
        public void RetrieveTokenHeaderTests_Success()
        {
            Mock<HttpClient> moqHttpClient = new Mock<HttpClient>(MockBehavior.Strict);
            BadsecClient client = new BadsecClient(moqHttpClient.Object);

            var testData = new[]
            {
                new
                {
                    Scenario = "Non-empty auth token header",
                    HeaderValue = "thisisatest",
                    Expected = "thisisatest"
                },
                new
                {
                    Scenario = "Empty auth token header",
                    HeaderValue = String.Empty,
                    Expected = String.Empty
                }

            };

            foreach (var data in testData)
            {
                Console.WriteLine("Running the scenario: " + data.Scenario);

                HttpResponseMessage response = new HttpResponseMessage();
                if (!String.IsNullOrWhiteSpace(data.HeaderValue))
                {
                    response.Headers.Add(BadsecClient.AuthTokenHeader, data.HeaderValue);
                }

                string tokenHeader = client.RetrieveTokenHeader(response);

                Assert.AreEqual(
                    expected: data.Expected,
                    actual: tokenHeader,
                    message: "The token header is not the same as what was expected.");
            }
        }

        [TestMethod]
        public void RetrieveTokenHeaderTest_MultipleHeaders_Success()
        {
            Mock<HttpClient> moqHttpClient = new Mock<HttpClient>(MockBehavior.Strict);
            BadsecClient client = new BadsecClient(moqHttpClient.Object);

            IList<string> headerValues = new List<string>
            {
                "headernumber1",
                "headernumber2"
            };
            
            HttpResponseMessage response = new HttpResponseMessage();
            response.Headers.Add(BadsecClient.AuthTokenHeader, headerValues);
            string tokenHeader = client.RetrieveTokenHeader(response);

            Assert.AreEqual(
                expected: headerValues.First(),
                actual: tokenHeader,
                message: "The token header is not the same as what was expected.");
        }

        [TestMethod]
        public void GetAuthTest_StatusOk_Success()
        {
            const string ExpectedResult = "12A63255-1388-AB5E-071C-FA35D27C4098";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, BadsecClient.AuthUriPath);

            HttpResponseMessage moqResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            moqResponse.Headers.Add(BadsecClient.AuthTokenHeader, ExpectedResult);
            
            Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            moqHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(moqResponse);

            HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
            BadsecClient client = new BadsecClient(httpClient);

            HttpResponseMessage response = client.GetAuth().Result;

            IEnumerable<string> authTokenHeaders;
            bool authTokenFound = response.Headers.TryGetValues(BadsecClient.AuthTokenHeader, out authTokenHeaders);

            Assert.IsTrue(
                condition: authTokenFound,
                message: "There was no auth token header found.");

            Assert.AreEqual(
                expected: ExpectedResult,
                actual: authTokenHeaders.First(),
                message: "The auth token in the header is not what was expected.");
        }

        [TestMethod]
        public void GetAuthEndpointTests_RetryMechanism_Failure()
        {
            var testData = new[]
            {
                new
                {
                    StatusCode = System.Net.HttpStatusCode.NotFound
                },
                new
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized
                },
                new
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                }

            };

            foreach (var data in testData)
            {
                Console.WriteLine("Running the scenario: " + data.StatusCode.ToString());

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, BadsecClient.AuthUriPath);
                HttpResponseMessage moqResponse = new HttpResponseMessage(data.StatusCode);

                Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
                moqHttpMessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(moqResponse);

                HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
                BadsecClient client = new BadsecClient(httpClient);

                HttpResponseMessage response = client.GetAuth().Result;

                IEnumerable<string> authTokenHeaders;
                bool authTokenFound = response.Headers.TryGetValues(BadsecClient.AuthTokenHeader, out authTokenHeaders);
                
                Assert.AreEqual(
                    expected: data.StatusCode,
                    actual: response.StatusCode,
                    message: "The status codes were not the same.");

                Assert.IsNull(
                    value: response.Content,
                    message: "response.Content is not null.");

                Assert.IsFalse(
                    condition: authTokenFound,
                    message: "An auth token header was found.");

                moqHttpMessageHandler.Protected()
                    .Verify<Task<HttpResponseMessage>>(
                        "SendAsync",
                        Times.Exactly(3),
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>());
            }
        }

        [TestMethod]
        public void GetUsersEndpointTests_RetryMechanism_Failure()
        {
            var testData = new[]
            {
                new
                {
                    StatusCode = System.Net.HttpStatusCode.NotFound
                },
                new
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized
                },
                new
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                }

            };

            foreach (var data in testData)
            {
                Console.WriteLine("Running the scenario: " + data.StatusCode.ToString());

                HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, BadsecClient.UsersUriPath);
                HttpResponseMessage moqResponse = new HttpResponseMessage(data.StatusCode);
                moqResponse.Content = new StringContent("");

                Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
                moqHttpMessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(moqResponse);

                HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
                BadsecClient client = new BadsecClient(httpClient);

                HttpResponseMessage response = client.GetAuth().Result;
                string responseString = response.Content.ReadAsStringAsync().Result;

                Assert.AreEqual(
                    expected: data.StatusCode,
                    actual: response.StatusCode,
                    message: "The status codes were not the same.");

                Assert.IsTrue(
                    condition: String.IsNullOrWhiteSpace(responseString),
                    message: "Data was found in the response content.");

                moqHttpMessageHandler.Protected()
                    .Verify<Task<HttpResponseMessage>>(
                        "SendAsync",
                        Times.Exactly(3),
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>());
            }
        }
    }
}
