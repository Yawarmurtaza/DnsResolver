using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using DnsClient;
using DnsClient.Protocol;

namespace DnsResolver.ConsoleRunner
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                string dn = "microsoft.com";
                // string dn = "someone.sometwo.iman.yawar";
                Console.WriteLine($"Domain name: defaults to {dn}");
                Console.WriteLine($"Domain name: defaults to {dn}");
                string domainName = Console.ReadLine();
                if (string.IsNullOrEmpty(domainName))
                {
                    domainName = dn;
                }

                Console.WriteLine("DSE Server IP Address: ");
                string dseServerIpAddress = Console.ReadLine();

                ILookupClient client = new LookupClient();

                IReadOnlyCollection<NameServer> servers = new List<NameServer>()
                {
                    new NameServer(IPAddress.Parse(dseServerIpAddress.Trim()))
                };

                IDnsQueryResponse response = client.QueryServer(servers, domainName.Trim(), QueryType.A);
                foreach (DnsResourceRecord nextRec in response.AllRecords)
                {
                    Console.WriteLine(nextRec);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }
    }
}

