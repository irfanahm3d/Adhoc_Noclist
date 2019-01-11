using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;

namespace Adhoc.Noclist.Tests
{
    [TestClass]
    public class BadsecClientTests
    {
        #region Unit Tests

        [TestMethod]
        public void ConvertStringToJsonTest_Success()
        {
            const string ExpectedResult = "[\"123456789\", \"234567891\", \"345678912\"]";
            const string UserList = "123456789\n234567891\n345678912\n";

            Mock<HttpClient> moqHttpClient = new Mock<HttpClient>(MockBehavior.Strict);
            BadsecClient client = new BadsecClient(moqHttpClient.Object);

            string jsonString = client.ConvertStringToJson(UserList);
            Assert.AreEqual(
                expected: ExpectedResult,
                actual: jsonString,
                message: "The json string is not correctly formatted.");
        }

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
                    HeaderValue = "thisisatest",
                    Expected = "thisisatest"
                },
                new
                {
                    HeaderValue = String.Empty,
                    Expected = String.Empty
                }

            };

            foreach (var data in testData)
            {
                Console.WriteLine("Running the scenario - Auth Token Header =  " + data.HeaderValue);

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
        public void HttpRequestTests_ExceptionHandling()
        {
            IList<Exception> exceptionsToThrow = new List<Exception>
            {
                new TaskCanceledException(),
                new HttpRequestException(),
                new InsufficientMemoryException(),
                new Exception()
            };

            foreach (var exception in exceptionsToThrow)
            {
                Console.WriteLine("Running the scenario - Throwing " + exception.ToString());

                Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
                moqHttpMessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ThrowsAsync(exception);

                HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
                BadsecClient client = new BadsecClient(httpClient);

                try
                {
                    HttpResponseMessage response =
                        client.HttpRequest(
                            new HttpRequestMessage(HttpMethod.Get, "endpoint"),
                            It.IsAny<HttpCompletionOption>()).Result;
                }
                catch (Exception ex)
                {
                    Assert.AreEqual(
                        expected: exception.GetType(),
                        actual: ex.GetBaseException().GetType(),
                        message: "The exceptions are not the same.");
                }
            }
        }

        [TestMethod]
        public void HttpRequestTests_StatusCodes()
        {
            var testData = new[]
            {
                new
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                },
                new
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError
                }
            };

            foreach (var data in testData)
            {
                Console.WriteLine("Running the scenario - Status Code " + data.StatusCode.ToString());

                Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
                moqHttpMessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage(data.StatusCode));

                HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
                BadsecClient client = new BadsecClient(httpClient);

                HttpResponseMessage response =
                    client.HttpRequest(
                        new HttpRequestMessage(HttpMethod.Get, "endpoint"),
                        It.IsAny<HttpCompletionOption>()).Result;

                Assert.AreEqual(
                    expected: data.StatusCode,
                    actual: response.StatusCode,
                    message: "The status codes are not the same.");
            }
        }

        [TestMethod]
        public void GetHttpWithRetryTest()
        {
            var testData = new[]
            {
                new
                {
                    StatusCode = System.Net.HttpStatusCode.NotFound,
                    Endpoint = "endpoint",
                    Checksum = String.Empty,
                    Content = "Endpoint not found",
                    TimesCalled = 3
                },
                new
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Endpoint = "users",
                    Checksum = String.Empty,
                    Content = "You must authorize yourself before calling /users",
                    TimesCalled = 3
                },
                new
                {
                    StatusCode = System.Net.HttpStatusCode.OK,
                    Endpoint = "users",
                    Checksum = "checksum",
                    Content = "123456789,123456789,123456789",
                    TimesCalled = 1
                },
                new
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Endpoint = "users",
                    Checksum = "checksum",
                    Content = "Invalid checksum, expected 92D5FF242A0 but got checksum",
                    TimesCalled = 3
                }
            };

            foreach (var data in testData)
            {
                Console.WriteLine(
                    "Running the scenario - Calling endpoint /" + data.Endpoint +
                    ". Returning Status Code " + data.StatusCode.ToString());

                HttpResponseMessage moqResponse = new HttpResponseMessage(data.StatusCode);
                moqResponse.Content = new StringContent(data.Content);

                Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
                moqHttpMessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(moqResponse);

                HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
                BadsecClient client = new BadsecClient(httpClient);

                HttpResponseMessage response =
                    client.GetHttpWithRetry(data.Endpoint, It.IsAny<HttpCompletionOption>(), It.IsAny<string>()).Result;
                string responseString = response.Content.ReadAsStringAsync().Result;

                Assert.AreEqual(
                    expected: data.StatusCode,
                    actual: moqResponse.StatusCode,
                    message: "The status codes were not the same.");

                Assert.AreEqual(
                    expected: data.Content,
                    actual: responseString,
                    message: "response content is not the same.");

                moqHttpMessageHandler.Protected()
                    .Verify<Task<HttpResponseMessage>>(
                        "SendAsync",
                        Times.Exactly(data.TimesCalled),
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>());
            }
        }

        [TestMethod]
        public void GetHttpWithRetryTest_ExceptionHandling()
        {
            Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            moqHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new Exception());

            HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
            BadsecClient client = new BadsecClient(httpClient);

            try
            {
                HttpResponseMessage response =
                    client.GetHttpWithRetry(It.IsAny<string>(), It.IsAny<HttpCompletionOption>(), It.IsAny<string>()).Result;
            }
            catch (AggregateException aggregateException)
            {
                Assert.AreEqual(
                    expected: aggregateException.GetType(),
                    actual: typeof(AggregateException),
                    message: "The actual returned exception is not an AggregateException.");
                
                moqHttpMessageHandler.Protected()
                    .Verify<Task<HttpResponseMessage>>(
                        "SendAsync",
                        Times.Exactly(3),
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>());
            }
        }

        [TestMethod]
        public void GetHttpWithRetryTest_ExceptionHandling_Success()
        {
            Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            moqHttpMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new Exception())
                .ReturnsAsync(new HttpResponseMessage(System.Net.HttpStatusCode.OK));

            HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
            BadsecClient client = new BadsecClient(httpClient);

            HttpResponseMessage response =
                client.GetHttpWithRetry(It.IsAny<string>(), It.IsAny<HttpCompletionOption>(), It.IsAny<string>()).Result;

            Assert.AreEqual(
                expected: System.Net.HttpStatusCode.OK,
                actual: response.StatusCode,
                message: "The status codes were not the same.");

            moqHttpMessageHandler.Protected()
                .Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.Exactly(2),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }

        #endregion

        #region Functional Tests

        [TestMethod]
        public void GetAuthTest_StatusOk_Success()
        {
            const string ExpectedResult = "12A63255-1388-AB5E-071C-FA35D27C4098";

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
        public void GetUsersTests_MultipleRetries_Success()
        {
            const string ErrorCallResponse = "The BADSEC server timed out";
            const string OKCallResponse = "123456789\n234567891\n345678912\n";

            IList<HttpResponseMessage> responseList = new List<HttpResponseMessage>
            {
                new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError),
                new HttpResponseMessage(System.Net.HttpStatusCode.OK),
            };

            responseList[0].Content = new StringContent(ErrorCallResponse);
            responseList[1].Content = new StringContent(OKCallResponse);

            Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            moqHttpMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(responseList[0])
                .ReturnsAsync(responseList[1]);

            HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
            BadsecClient client = new BadsecClient(httpClient);

            HttpResponseMessage response = client.GetUsers(It.IsAny<string>()).Result;
            string responseString = response.Content.ReadAsStringAsync().Result;

            Assert.AreEqual(
                expected: System.Net.HttpStatusCode.OK,
                actual: response.StatusCode,
                message: "The status codes were not the same.");

            Assert.AreEqual(
                expected: OKCallResponse,
                actual: responseString,
                message: "response content was not the same.");

            moqHttpMessageHandler.Protected()
                .Verify<Task<HttpResponseMessage>>(
                    "SendAsync",
                    Times.Exactly(2),
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod]
        public void GetAuthTests_RetryMechanism_Failure()
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
                Console.WriteLine("Running the scenario - Status Code " + data.StatusCode.ToString());

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
        public void GetUsersTests_RetryMechanism_Failure()
        {
            var testData = new[]
            {
                new
                {
                    StatusCode = System.Net.HttpStatusCode.Unauthorized,
                    Content = "Unauthorized call",
                },
                new
                {
                    StatusCode = System.Net.HttpStatusCode.InternalServerError,
                    Content = "You must authorize yourself before calling /users",
                }
            };

            foreach (var data in testData)
            {
                Console.WriteLine("Running the scenario - Status Code " + data.StatusCode.ToString());

                HttpResponseMessage moqResponse = new HttpResponseMessage(data.StatusCode);
                moqResponse.Content = new StringContent(data.Content);

                Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
                moqHttpMessageHandler.Protected()
                    .Setup<Task<HttpResponseMessage>>(
                        "SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(moqResponse);

                HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
                BadsecClient client = new BadsecClient(httpClient);

                HttpResponseMessage response = client.GetUsers(It.IsAny<string>()).Result;
                string responseString = response.Content.ReadAsStringAsync().Result;

                Assert.AreEqual(
                    expected: data.StatusCode,
                    actual: response.StatusCode,
                    message: "The status codes were not the same.");

                Assert.AreEqual(
                    expected: data.Content,
                    actual: responseString,
                    message: "response content was not the same.");

                moqHttpMessageHandler.Protected()
                    .Verify<Task<HttpResponseMessage>>(
                        "SendAsync",
                        Times.Exactly(3),
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>());
            }
        }

        [TestMethod]
        public void GetUsersListTests_Success()
        {
            const string ExpectedUserList = "[\"123456789\", \"234567891\", \"345678912\"]";
            const string ReturningUserList = "123456789\n234567891\n345678912\n";
            const string AuthToken = "12A63255-1388-AB5E-071C-FA35D27C4098";

            HttpResponseMessage authResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            authResponse.Headers.Add(BadsecClient.AuthTokenHeader, AuthToken);
            HttpResponseMessage usersResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            usersResponse.Content = new StringContent(ReturningUserList);
            
            Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            moqHttpMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(authResponse)
                .ReturnsAsync(usersResponse);

            HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
            BadsecClient client = new BadsecClient(httpClient);

            string userList = client.GetUsersList().Result;

            Assert.AreEqual(
                expected: ExpectedUserList,
                actual: userList,
                message: "The actual result is not the same.");
        }

        [TestMethod]
        public void GetUsersListTests_GetAuthFailure()
        {
            HttpResponseMessage authFailResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
            Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            moqHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(authFailResponse);

            HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
            BadsecClient client = new BadsecClient(httpClient);

            try
            {
                string userList = client.GetUsersList().Result;
            }
            catch (Exception ex)
            {
                Assert.AreEqual(
                    expected: typeof(Exception),
                    actual: ex.InnerException.GetType(),
                    message: "The exceptions are not the same.");

                Assert.AreEqual(
                    expected: BadsecClient.AuthFailureMessage,
                    actual: ex.InnerException.Message,
                    message: "The exception was not expected.");
            }
        }

        [TestMethod]
        public void GetUsersListTests_GetUsersFailure()
        {
            const string AuthToken = "12A63255-1388-AB5E-071C-FA35D27C4098";

            HttpResponseMessage authSuccessResponse = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
            authSuccessResponse.Headers.Add(BadsecClient.AuthTokenHeader, AuthToken);
            HttpResponseMessage usersFailureResponse = new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);

            Mock<HttpMessageHandler> moqHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            moqHttpMessageHandler.Protected()
                .SetupSequence<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(authSuccessResponse)
                .ReturnsAsync(usersFailureResponse)
                .ReturnsAsync(usersFailureResponse)
                .ReturnsAsync(usersFailureResponse);

            HttpClient httpClient = new HttpClient(moqHttpMessageHandler.Object);
            BadsecClient client = new BadsecClient(httpClient);

            try
            {
                string userList = client.GetUsersList().Result;
            }
            catch (Exception ex)
            {
                Assert.AreEqual(
                    expected: typeof(Exception),
                    actual: ex.InnerException.GetType(),
                    message: "The exceptions are not the same.");

                Assert.AreEqual(
                    expected: BadsecClient.UserListRetrievalFailureMessage,
                    actual: ex.InnerException.Message,
                    message: "The exception was not expected.");
            }
        }

        #endregion
    }
}
