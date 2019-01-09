using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Adhoc_Noclist
{
    class Program
    {
        static int Main(string[] args)
        {
            int statusCode = 0;
            // create singleton
            BadsecClient client = new BadsecClient();

            try
            {
                string jsonString = client.GetUsers().Result;
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
