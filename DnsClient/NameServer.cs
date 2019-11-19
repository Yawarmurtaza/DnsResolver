using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace DnsClient
{
    /// <summary>
    /// Represents a name server instance used by <see cref="ILookupClient"/>.
    /// Also, comes with some static methods to resolve name servers from the local network configuration.
    /// </summary>
    public class NameServer : IEquatable<NameServer>
    {
        /// <summary>
        /// The default DNS server port.
        /// </summary>
        public const int DefaultPort = 53;

        /// <summary>
        /// The public google DNS IPv4 endpoint.
        /// </summary>
        public static readonly NameServer GooglePublicDns = new IPEndPoint(IPAddress.Parse("8.8.4.4"), DefaultPort);

        /// <summary>
        /// The second public google DNS IPv6 endpoint.
        /// </summary>
        public static readonly NameServer GooglePublicDns2 = new IPEndPoint(IPAddress.Parse("8.8.8.8"), DefaultPort);

        /// <summary>
        /// The public google DNS IPv6 endpoint.
        /// </summary>
        public static readonly NameServer GooglePublicDnsIPv6 = new IPEndPoint(IPAddress.Parse("2001:4860:4860::8844"), DefaultPort);

        /// <summary>
        /// The second public google DNS IPv6 endpoint.
        /// </summary>
        public static readonly NameServer GooglePublicDns2IPv6 = new IPEndPoint(IPAddress.Parse("2001:4860:4860::8888"), DefaultPort);

        internal const string EtcResolvConfFile = "/etc/resolv.conf";

        /// <summary>
        /// Creates NameServer instance.
        /// </summary>
        public NameServer()
        {
            
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NameServer"/> class.
        /// </summary>
        /// <param name="endPoint">The name server endpoint.</param>
        public NameServer(IPAddress endPoint)
            : this(new IPEndPoint(endPoint, DefaultPort))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NameServer"/> class.
        /// </summary>
        /// <param name="endPoint">The name server endpoint.</param>
        /// <param name="port">The name server port.</param>
        public NameServer(IPAddress endPoint, int port)
            : this(new IPEndPoint(endPoint, port))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NameServer"/> class.
        /// </summary>
        /// <param name="endPoint">The name server endpoint.</param>
        /// <exception cref="System.ArgumentNullException">If <paramref name="endPoint"/> is null.</exception>
        public NameServer(IPEndPoint endPoint)
        {
            IPEndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NameServer"/> class from a <see cref="IPEndPoint"/>.
        /// </summary>
        /// <param name="endPoint">The endpoint.</param>
        public static implicit operator NameServer(IPEndPoint endPoint)
        {
            if (endPoint == null)
            {
                return null;
            }

            return new NameServer(endPoint);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NameServer"/> class from a <see cref="IPAddress"/>.
        /// </summary>
        /// <param name="address">The address.</param>
        public static implicit operator NameServer(IPAddress address)
        {
            if (address == null)
            {
                return null;
            }

            return new NameServer(address);
        }

        /// <summary>
        /// Gets a value indicating whether this <see cref="NameServer"/> is enabled.
        /// <para>
        /// The instance might get disabled if <see cref="ILookupClient"/> encounters problems to connect to it.
        /// </para>
        /// </summary>
        /// <value>
        ///   <c>true</c> if enabled; otherwise, <c>false</c>.
        /// </value>
        public bool Enabled { get; internal set; } = true;

        /// <summary>
        /// Gets the string representation of the configured <see cref="IPAddress"/>.
        /// </summary>
        public string Address => IPEndPoint.Address.ToString();

        /// <summary>
        /// Gets the port.
        /// </summary>
        public int Port => IPEndPoint.Port;

        /// <summary>
        /// Gets the address family.
        /// </summary>
        public AddressFamily AddressFamily => IPEndPoint.AddressFamily;

        /// <summary>
        /// Gets the size of the supported UDP payload.
        /// <para>
        /// This value might get updated by <see cref="ILookupClient"/> by reading the options records returned by a query.
        /// </para>
        /// </summary>
        /// <value>
        /// The size of the supported UDP payload.
        /// </value>
        public int? SupportedUdpPayloadSize { get; internal set; }

        // for tracking if we can re-enable the server...
        internal DnsRequestMessage LastSuccessfulRequest { get; set; }

        internal IPEndPoint IPEndPoint { get; }

        /// <summary>
        /// Returns a <see cref="System.String" /> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String" /> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return $"{Address}:{Port} (Udp: {SupportedUdpPayloadSize ?? 512})";
        }

        internal NameServer Clone()
        {
            return this;
            // TODO: maybe not needed
            ////return new NameServer(IPEndPoint)
            ////{
            ////    Enabled = Enabled,
            ////    SupportedUdpPayloadSize = SupportedUdpPayloadSize
            ////};
        }

        /// <inheritdocs />
        public override bool Equals(object obj)
        {
            return Equals(obj as NameServer);
        }

        /// <inheritdocs />
        public bool Equals(NameServer other)
        {
            return other != null
                && EqualityComparer<IPEndPoint>.Default.Equals(IPEndPoint, other.IPEndPoint);
        }

        /// <inheritdocs />
        public override int GetHashCode()
        {
            return EqualityComparer<IPEndPoint>.Default.GetHashCode(IPEndPoint);
        }

        /// <summary>
        /// Gets a list of name servers by iterating over the available network interfaces.
        /// <para>
        /// If <paramref name="fallbackToGooglePublicDns" /> is enabled, this method will return the google public dns endpoints if no
        /// local DNS server was found.
        /// </para>
        /// </summary>
        /// <param name="skipIPv6SiteLocal">If set to <c>true</c> local IPv6 sites are skiped.</param>
        /// <param name="fallbackToGooglePublicDns">If set to <c>true</c> the public Google DNS servers are returned if no other servers could be found.</param>
        /// <returns>
        /// The list of name servers.
        /// </returns>
        public IEnumerable<NameServer> ResolveNameServers(bool skipIPv6SiteLocal = true, bool fallbackToGooglePublicDns = true)
        {
            IEnumerable<NameServer> endPoints = null;

            List<Exception> exceptions = new List<Exception>();

            try
            {
                endPoints = QueryNetworkInterfaces(skipIPv6SiteLocal);
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }

            if (exceptions.Count > 0)
            {
                if (exceptions.Count > 1)
                {
                    throw new AggregateException("Error resolving name servers", exceptions);
                }
                else
                {
                    throw exceptions.First();
                }
            }

            if (endPoints == null && fallbackToGooglePublicDns)
            {
                return new NameServer[]
                {
                    GooglePublicDnsIPv6,
                    GooglePublicDns2IPv6,
                    GooglePublicDns,
                    GooglePublicDns2,
                };
            }

            return endPoints;
        }

        /// <summary>
        /// Finds the local network interface cards, filters those that are active and non-loopback then gets the DNS address on each NIC.
        /// </summary>
        /// <param name="skipIPv6SiteLocal">Switch to allow IP v6.</param>
        /// <returns>Name server collection.</returns>
        private IEnumerable<NameServer> QueryNetworkInterfaces(bool skipIPv6SiteLocal)
        {
            // because we need distinct name servers thats why using Hashset type of collection of name server object.
            HashSet<NameServer> result = new HashSet<NameServer>();

            // step 1: Get all network adapters installed on this system...
            NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();

            // Step 2: Use the network adapters to get the active network interfaces except the loopback one because are not interested in 
            // getting the loopback interface since there is no data sent on wire using this interface.
            IEnumerable<NetworkInterface> interfaces 
                = adapters.Where(p => p.OperationalStatus == OperationalStatus.Up 
                                      && p.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            // Step 3: Loop though each network interface found 
            foreach (NetworkInterface nextInterface in interfaces)
            {
                // Step 4: Get the properties of this interface.
                IPInterfaceProperties interfaceProperties = nextInterface.GetIPProperties();

                // Step 5: Get the Domain Name Server for this interface 
                IEnumerable<IPAddress> dnsAddresses = interfaceProperties.DnsAddresses
                    .Where(i => i.AddressFamily == AddressFamily.InterNetwork || i.AddressFamily == AddressFamily.InterNetworkV6);
                
                // Step 6: Loop through each dns address of this interface to check if IP v6 is available and that we are interested in IP v6?
                foreach (IPAddress dnsAddress in dnsAddresses)
                {
                    if (dnsAddress.AddressFamily == AddressFamily.InterNetworkV6)
                    {
                        if (skipIPv6SiteLocal && dnsAddress.IsIPv6SiteLocal)
                        {
                            continue;
                        }
                    }

                    // add the DNS address assigned to this interface. This is the default gateway address when default windows settings 
                    // are used.
                    /*
                     * --- When used the DHCP, the ipconfig command return this:
                     * IPv4 Address. . . . . . . . . . . : 192.168.1.7
                     * Subnet Mask . . . . . . . . . . . : 255.255.255.0
                     * Default Gateway . . . . . . . . . : 192.168.1.1 <<-- this is our DNS server IP Address as well as the default gateway!!
                     *
                     * The default gateway and DNS Server IP addresses can be different.
                     */


                    result.Add(new IPEndPoint(dnsAddress, DefaultPort)); // the default port is 53 for DNS server.
                }
            }

            return result;
        }
    }
}