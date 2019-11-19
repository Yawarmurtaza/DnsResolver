using System;
using System.Collections.Generic;
using System.Globalization;
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
                string ipaddress = "172.17.131.1";
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
                if (string.IsNullOrEmpty(dseServerIpAddress))
                {
                    dseServerIpAddress = ipaddress;
                }

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

            //Name n = "yawar";
            //var ame = n.FirstName;

            //Currency cur = 99;
            //Currency cur1 = "PKR";
            //Currency cur2 = 99.12m;
            //string symbol = cur2;
            //decimal d = cur2;
        }
    }


    public class Name
    {
        public string FirstName { get; }
        public static implicit operator Name(string firstname)
        {
            return new Name(firstname);
        }

        public Name(string firstname)
        {
            this.FirstName = firstname;
        }
    }

    public class Currency
    {
        public decimal Value { get; }
        public string Symbol { get; }  
        
        public Currency(decimal value, string symbol)
        {
            Value = value;
            Symbol = symbol;
        }

        /// <span class="code-SummaryComment"><summary></span>
        /// Creates Currency object from string supplied as currency sign.
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="rhs">The currency sign like $,£,¥,€,Rs etc. </param></span>
        /// <span class="code-SummaryComment"><returns>Returns new Currency object.</returns></span>
        public static implicit operator Currency(string rhs)
        {
            Currency c = new Currency(0, rhs); //Internally call Currency constructor
            return c;

        }

        /// <span class="code-SummaryComment"><summary></span>
        /// Creates a currency object from decimal value. 
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="value">The currency value in decimal.</param></span>
        /// <span class="code-SummaryComment"><returns>Returns new Currency object.</returns></span>
        public static implicit operator Currency(decimal value)
        {
            Currency c = new Currency(value, NumberFormatInfo.CurrentInfo.CurrencySymbol);
            return c;
        }

        /// <span class="code-SummaryComment"><summary></span>
        /// Creates a decimal value from Currency object,
        /// used to assign currency to decimal.
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="currency">The Currency object.</param></span>
        /// <span class="code-SummaryComment"><returns>Returns decimal value of the currency</returns></span>
        public static implicit operator decimal(Currency currency)
        {
            return currency.Value;
        }

        /// <span class="code-SummaryComment"><summary></span>
        /// Creates a long value from Currency object, used to assign currency to long.
        /// <span class="code-SummaryComment"></summary></span>
        /// <span class="code-SummaryComment"><param name="rhs">The Currency object.</param></span>
        /// <span class="code-SummaryComment"><returns>Returns long value of the currency</returns></span>
        public static implicit operator long(Currency rhs)
        {
            return (long)rhs.Value;
        }

        public static implicit operator string(Currency cur)
        {
            return cur.Symbol;
        }
    }
}

