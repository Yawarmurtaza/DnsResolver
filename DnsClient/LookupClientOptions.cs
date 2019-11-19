using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;

namespace DnsClient
{
    /// <summary>
    /// The options used to configure defaults in <see cref="LookupClient"/> and to optionally use specific settings per query.
    /// </summary>
    public class LookupClientOptions : DnsQueryOptions
    {
        private static readonly TimeSpan s_infiniteTimeout = System.Threading.Timeout.InfiniteTimeSpan;

        // max is 24 days
        private static readonly TimeSpan s_maxTimeout = TimeSpan.FromMilliseconds(int.MaxValue);

        private TimeSpan? _minimumCacheTimeout;

        /// <summary>
        /// Creates a new instance of <see cref="LookupClientOptions"/> without name servers.
        /// </summary>
        public LookupClientOptions(bool resolveNameServers = true)
            : base(resolveNameServers)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="LookupClientOptions"/> with one name server.
        /// <see cref="IPAddress"/> or <see cref="IPEndPoint"/> can be used as well thanks to implicit conversion.
        /// </summary>
        /// <param name="nameServer">The name servers.</param>
        public LookupClientOptions(NameServer nameServer) : base(nameServer)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="LookupClientOptions"/>.
        /// </summary>
        /// <param name="nameServers">A collection of name servers.</param>
        public LookupClientOptions(params NameServer[] nameServers) : base(nameServers)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="LookupClientOptions"/>.
        /// </summary>
        /// <param name="nameServers">A collection of name servers.</param>
        public LookupClientOptions(params IPEndPoint[] nameServers) : base(nameServers)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="LookupClientOptions"/>.
        /// </summary>
        /// <param name="nameServers">A collection of name servers.</param>
        public LookupClientOptions(params IPAddress[] nameServers)
            : base(nameServers)
        {
        }

        /// <summary>
        /// Creates a new instance of <see cref="LookupClientOptions"/>.
        /// </summary>
        /// <param name="nameServers">A collection of name servers.</param>
        public LookupClientOptions(IReadOnlyCollection<NameServer> nameServers) : base(nameServers)
        {
        }

        /// <summary>
        /// Gets or sets a <see cref="TimeSpan"/> which can override the TTL of a resource record in case the
        /// TTL of the record is lower than this minimum value.
        /// Default is <c>Null</c>.
        /// <para>
        /// This is useful in cases where the server retruns records with zero TTL.
        /// </para>
        /// </summary>
        /// <remarks>
        /// This setting gets igonred in case <see cref="DnsQueryOptions.UseCache"/> is set to <c>False</c>.
        /// The maximum value is 24 days or <see cref="Timeout.Infinite"/>.
        /// </remarks>
        public TimeSpan? MinimumCacheTimeout
        {
            get { return _minimumCacheTimeout; }
            set
            {
                if (value.HasValue &&
                    (value < TimeSpan.Zero || value > s_maxTimeout) && value != s_infiniteTimeout)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                _minimumCacheTimeout = value;
            }
        }

        /// <summary>
        /// Converts the options into readonly settings.
        /// </summary>
        /// <param name="fromOptions">The options.</param>
        public static implicit operator LookupClientSettings(LookupClientOptions fromOptions)
        {
            if (fromOptions == null)
            {
                return null;
            }

            return new LookupClientSettings(fromOptions);
        }
    }
}