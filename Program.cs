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
            catch (Exception ex)
            {
                var aggEx = (AggregateException)ex.InnerException;
                foreach (Exception e in aggEx.InnerExceptions)
                {
                    Console.Error.WriteLine(e.Message);
                }

                statusCode = -1;
            }
            
            return statusCode;
        }
    }
}
