using System;
using System.Collections.Generic;

namespace DnsClient
{
    /// <summary>
    /// The contract for the LookupClient.
    /// <para>
    /// The interfaces for the query methods and the lookup client properties are separated so that one can
    /// inject or expose only the <see cref="IDnsQuery"/> without exposing the configuration options.
    /// </para>
    /// </summary>
    public interface ILookupClient : IDnsQuery
    { 
        // all settings will be moved into DnsQueryOptions/LookupClientOptions

        [Obsolete("This property will be removed from LookupClient in the next version. Use LookupClientOptions to initialize LookupClient instead.")]
        bool EnableAuditTrail { get; set; }

        [Obsolete("This property will be removed from LookupClient in the next version. Use LookupClientOptions to initialize LookupClient instead.")]
        bool ThrowDnsErrors { get; set; }

        [Obsolete("This property will be removed from LookupClient in the next version. Use LookupClientOptions to initialize LookupClient instead.")]
        TimeSpan Timeout { get; set; }

        [Obsolete("This property will be removed from LookupClient in the next version. Use LookupClientOptions to initialize LookupClient instead.")]
        bool UseTcpOnly { get; set; }
    }

    // TODO: revisit if we need this AND LookupClientSettings, might not be needed for per query options
}