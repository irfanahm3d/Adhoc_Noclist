using System;
using System.Net.Http;

namespace Adhoc.Noclist
{
    class Program
    {
        static int Main(string[] args)
        {
            int statusCode = 0;
            HttpClient httpClient = new HttpClient();
            BadsecClient client = new BadsecClient(httpClient);

            try
            {
                string jsonString = client.GetUsersList().Result;
                Console.Out.WriteLine(jsonString);
            }
            catch (AggregateException aggregateException)
            {
                foreach (Exception ex in aggregateException.InnerExceptions)
                {
                    Console.Error.WriteLine(ex.Message);
                }

                statusCode = -1;
            }

            return statusCode;
        }
    }
}
