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
        static void Main(string[] args)
        {
            // create singleton
            BadsecClient client = new BadsecClient();
            
                client.GetUsers().Wait();
        }
    }
}
