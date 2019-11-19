using System;
using System.Collections.Generic;
using System.Threading;

namespace DnsClient
{
    /// <summary>
    /// The readonly version of <see cref="LookupClientOptions"/> used as default settings in <see cref="LookupClient"/>.
    /// </summary>
    public class LookupClientSettings : DnsQuerySettings, IEquatable<LookupClientSettings>
    {
        /// <summary>
        /// Creates a new instance of <see cref="LookupClientSettings"/>.
        /// </summary>
        public LookupClientSettings(LookupClientOptions options) : base(options)
        {
            MinimumCacheTimeout = options.MinimumCacheTimeout;
        }

        /// <summary>
        /// Gets a <see cref="TimeSpan"/> which can override the TTL of a resource record in case the
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
        public TimeSpan? MinimumCacheTimeout { get; }

        /// <inheritdocs />
        public override bool Equals(object obj)
        {
            return Equals(obj as LookupClientSettings);
        }

        /// <inheritdocs />
        public bool Equals(LookupClientSettings other)
        {
            return other != null &&
                   base.Equals(other) &&
                   EqualityComparer<TimeSpan?>.Default.Equals(MinimumCacheTimeout, other.MinimumCacheTimeout);
        }

        /// <inheritdocs />
        public override int GetHashCode()
        {
            var hashCode = 1049610412;
            hashCode = hashCode * -1521134295 + base.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<TimeSpan?>.Default.GetHashCode(MinimumCacheTimeout);
            return hashCode;
        }

        internal LookupClientSettings Copy(
            IReadOnlyCollection<NameServer> nameServers,
            TimeSpan? minimumCacheTimeout,
            bool? continueOnDnsError = null,
            bool? enableAuditTrail = null,
            bool? recursion = null,
            int? retries = null,
            bool? throwDnsErrors = null,
            TimeSpan? timeout = null,
            bool? useCache = null,
            bool? useRandomNameServer = null,
            bool? useTcpFallback = null,
            bool? useTcpOnly = null)
        {
            // auto resolved flag might get lost here. But this stuff gets deleted anyways.
            return new LookupClientSettings(new LookupClientOptions(nameServers)
            {
                MinimumCacheTimeout = minimumCacheTimeout,
                ContinueOnDnsError = continueOnDnsError ?? ContinueOnDnsError,
                EnableAuditTrail = enableAuditTrail ?? EnableAuditTrail,
                Recursion = recursion ?? Recursion,
                Retries = retries ?? Retries,
                ThrowDnsErrors = throwDnsErrors ?? ThrowDnsErrors,
                Timeout = timeout ?? Timeout,
                UseCache = useCache ?? UseCache,
                UseRandomNameServer = useRandomNameServer ?? UseRandomNameServer,
                UseTcpFallback = useTcpFallback ?? UseTcpFallback,
                UseTcpOnly = useTcpOnly ?? UseTcpOnly
            });
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithContinueOnDnsError(bool value)
        {
            return Copy(NameServers, MinimumCacheTimeout, continueOnDnsError: value);
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithEnableAuditTrail(bool value)
        {
            return Copy(NameServers, MinimumCacheTimeout, enableAuditTrail: value);
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithMinimumCacheTimeout(TimeSpan? value)
        {
            return Copy(NameServers, minimumCacheTimeout: value);
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithRecursion(bool value)
        {
            return Copy(NameServers, MinimumCacheTimeout, recursion: value);
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithRetries(int value)
        {
            return Copy(NameServers, MinimumCacheTimeout, retries: value);
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithThrowDnsErrors(bool value)
        {
            return Copy(NameServers, MinimumCacheTimeout, throwDnsErrors: value);
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithTimeout(TimeSpan value)
        {
            return Copy(NameServers, MinimumCacheTimeout, timeout: value);
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithUseCache(bool value)
        {
            return Copy(NameServers, MinimumCacheTimeout, useCache: value);
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithUseTcpFallback(bool value)
        {
            return Copy(NameServers, MinimumCacheTimeout, useTcpFallback: value);
        }

        // TODO: remove if LookupClient settings can be made readonly
        internal LookupClientSettings WithUseTcpOnly(bool value)
        {
            return Copy(NameServers, MinimumCacheTimeout, useTcpOnly: value);
        }
    }
}