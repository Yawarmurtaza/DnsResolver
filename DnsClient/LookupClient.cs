﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DnsClient.Protocol.Options;

namespace DnsClient
{
    /// <summary>
    /// The <see cref="LookupClient"/> is the main query class of this library and should be used for any kind of DNS lookup query.
    /// <para>
    /// It implements <see cref="ILookupClient"/> and <see cref="IDnsQuery"/> which contains a number of extension methods, too.
    /// The extension methods internally all invoke the standard <see cref="IDnsQuery"/> queries though.
    /// </para>
    /// </summary>
    /// <seealso cref="IDnsQuery"/>
    /// <seealso cref="ILookupClient"/>
    /// <example>
    /// A basic example wihtout specifying any DNS server, which will use the DNS server configured by your local network.
    /// <code>
    /// <![CDATA[
    /// var client = new LookupClient();
    /// var result = client.Query("google.com", QueryType.A);
    ///
    /// foreach (var aRecord in result.Answers.ARecords())
    /// {
    ///     Console.WriteLine(aRecord);
    /// }
    /// ]]>
    /// </code>
    /// </example>
    public class LookupClient : ILookupClient
    {
        private static readonly int s_serverHealthCheckInterval = (int)TimeSpan.FromSeconds(30).TotalMilliseconds;
        private static int _uniqueId;
        private readonly DnsMessageHandler _messageHandler;
        private readonly DnsMessageHandler _tcpFallbackHandler;
        private readonly Random _random = new Random();
        private bool _healthCheckRunning ;
        private int _lastHealthCheck;

        // for backward compat
        /// <summary>
        /// Gets the list of configured name servers.
        /// </summary>
        public IReadOnlyCollection<NameServer> NameServers => Settings.NameServers;
        /// <summary>
        /// Gets the settings.
        /// </summary>
        public LookupClientSettings Settings { get; private set; }
        
        [Obsolete("This property will be removed from LookupClient in the next version. Use LookupClientOptions to initialize LookupClient instead.")]
        public bool UseTcpOnly
        {
            get => Settings.UseTcpOnly;
            set
            {
                if (Settings.UseTcpOnly != value)
                {
                    Settings = Settings.WithUseTcpOnly(value);
                }
            }
        }

        [Obsolete("This property will be removed from LookupClient in the next version. Use LookupClientOptions to initialize LookupClient instead.")]
        public bool EnableAuditTrail
        {
            get => Settings.EnableAuditTrail;
            set
            {
                if (Settings.EnableAuditTrail != value)
                {
                    Settings = Settings.WithEnableAuditTrail(value);
                }
            }
        }

        [Obsolete("This property will be removed from LookupClient in the next version. Use LookupClientOptions to initialize LookupClient instead.")]
        public bool Recursion
        {
            get => Settings.Recursion;
            set
            {
                if (Settings.Recursion != value)
                {
                    Settings = Settings.WithRecursion(value);
                }
            }
        }

        [Obsolete("This property will be removed from LookupClient in the next version. Use LookupClientOptions to initialize LookupClient instead.")]
        public bool ThrowDnsErrors
        {
            get => Settings.ThrowDnsErrors;
            set
            {
                if (Settings.ThrowDnsErrors != value)
                {
                    Settings = Settings.WithThrowDnsErrors(value);
                }
            }
        }

        [Obsolete("This property will be removed from LookupClient in the next version. Use LookupClientOptions to initialize LookupClient instead.")]
        public TimeSpan Timeout
        {
            get => Settings.Timeout;
            set
            {
                if (Settings.Timeout != value)
                {
                    Settings = Settings.WithTimeout(value);
                }
            }
        }

        internal ResponseCache ResponseCache { get; }

        /// <summary>
        /// Creates a new instance of <see cref="LookupClient"/> without specifying any name server.
        /// This will implicitly use the name server(s) configured by the local network adapter(s).
        /// </summary>
        /// <remarks>
        /// This uses <see cref="NameServer.ResolveNameServers(bool, bool)"/>.
        /// The resulting list of name servers is highly dependent on the local network configuration and OS.
        /// </remarks>
        /// <example>
        /// In the following example, we will create a new <see cref="LookupClient"/> without explicitly defining any DNS server.
        /// This will use the DNS server configured by your local network.
        /// <code>
        /// <![CDATA[
        /// var client = new LookupClient();
        /// var result = client.Query("google.com", QueryType.A);
        ///
        /// foreach (var aRecord in result.Answers.ARecords())
        /// {
        ///     Console.WriteLine(aRecord);
        /// }
        /// ]]>
        /// </code>
        /// </example>
        public LookupClient() : this(new LookupClientOptions(resolveNameServers: true))
        {
        }
        
        /// <summary>
        /// Creates a new instance of <see cref="LookupClient"/> with custom settings.
        /// </summary>
        /// <param name="options">The options to use with this <see cref="LookupClient"/> instance.</param>
        public LookupClient(LookupClientOptions options)
            : this(options, null, null)
        {
        }

        internal LookupClient(LookupClientOptions options, DnsMessageHandler udpHandler = null, DnsMessageHandler tcpHandler = null)
        {
            Settings = options ?? throw new ArgumentNullException(nameof(options));
            _messageHandler = udpHandler ?? new DnsUdpMessageHandler(true);
            _tcpFallbackHandler = tcpHandler ?? new DnsTcpMessageHandler();
            ResponseCache = new ResponseCache(true, Settings.MinimumCacheTimeout);
        }

        /// <summary>
        /// Does a reverse lookup for the <paramref name="ipAddress"/>.
        /// </summary>
        /// <param name="ipAddress">The <see cref="IPAddress"/>.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which might contain the <see cref="DnsClient.Protocol.PtrRecord" /> for the <paramref name="ipAddress"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="ipAddress"/> is null.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public IDnsQueryResponse QueryReverse(IPAddress ipAddress)
            => Query(GetReverseQuestion(ipAddress));

        /// <summary>
        /// Does a reverse lookup for the <paramref name="ipAddress"/>.
        /// </summary>
        /// <param name="ipAddress">The <see cref="IPAddress"/>.</param>
        /// <param name="queryOptions">Query options to be used instead of <see cref="LookupClient"/>'s settings.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which might contain the <see cref="DnsClient.Protocol.PtrRecord" /> for the <paramref name="ipAddress"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="ipAddress"/> is null.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public IDnsQueryResponse QueryReverse(IPAddress ipAddress, DnsQueryOptions queryOptions)
            => Query(GetReverseQuestion(ipAddress), queryOptions);

        /// <summary>
        /// Does a reverse lookup for the <paramref name="ipAddress" />.
        /// </summary>
        /// <param name="ipAddress">The <see cref="IPAddress" />.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which might contain the <see cref="DnsClient.Protocol.PtrRecord" /> for the <paramref name="ipAddress"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="ipAddress"/> is null.</exception>
        /// <exception cref="OperationCanceledException">If cancellation has been requested for the passed in <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public Task<IDnsQueryResponse> QueryReverseAsync(IPAddress ipAddress, CancellationToken cancellationToken = default)
            => QueryAsync(GetReverseQuestion(ipAddress), cancellationToken);

        /// <summary>
        /// Does a reverse lookup for the <paramref name="ipAddress" />.
        /// </summary>
        /// <param name="ipAddress">The <see cref="IPAddress" />.</param>
        /// <param name="queryOptions">Query options to be used instead of <see cref="LookupClient"/>'s settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which might contain the <see cref="DnsClient.Protocol.PtrRecord" /> for the <paramref name="ipAddress"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="ipAddress"/> is null.</exception>
        /// <exception cref="OperationCanceledException">If cancellation has been requested for the passed in <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public Task<IDnsQueryResponse> QueryReverseAsync(IPAddress ipAddress, DnsQueryOptions queryOptions, CancellationToken cancellationToken = default)
            => QueryAsync(GetReverseQuestion(ipAddress), queryOptions, cancellationToken);

        /// <summary>
        /// Performs a DNS lookup for the given <paramref name="query" />, <paramref name="queryType" /> and <paramref name="queryClass" />.
        /// </summary>
        /// <param name="query">The domain name query.</param>
        /// <param name="queryType">The <see cref="QueryType" />.</param>
        /// <param name="queryClass">The <see cref="QueryClass"/>.</param>
        /// <param name="queryOptions">Query options to be used instead of <see cref="LookupClient"/>'s settings.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which contains the response headers and lists of resource records.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="query"/> is null.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        /// <remarks>
        /// The behavior of the query can be controlled by default settings of this <see cref="LookupClient"/> instance or via <paramref name="queryOptions"/>.
        /// <see cref="Recursion"/> for example can be disabled and would instruct the DNS server to return no additional records.
        /// </remarks>
        public IDnsQueryResponse Query(string query, QueryType queryType, QueryClass queryClass = QueryClass.IN, DnsQueryOptions queryOptions = null)
        {
            var question = new DnsQuestion(query, queryType, queryClass);
            if (queryOptions == null)
            {
                return QueryInternal(question, Settings);
            }

            return Query(question, queryOptions);
        }

        /// <summary>
        /// Performs a DNS lookup for the given <paramref name="question" />.
        /// </summary>
        /// <param name="question">The domain name query.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which contains the response headers and lists of resource records.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="question"/> is null.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public IDnsQueryResponse Query(DnsQuestion question)
            => QueryInternal(question, Settings);

        /// <summary>
        /// Performs a DNS lookup for the given <paramref name="question" />.
        /// </summary>
        /// <param name="question">The domain name query.</param>
        /// <param name="queryOptions">Query options to be used instead of <see cref="LookupClient"/>'s settings.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which contains the response headers and lists of resource records.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="question"/> is null.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public IDnsQueryResponse Query(DnsQuestion question, DnsQueryOptions queryOptions)
        {
            if (queryOptions == null)
            {
                throw new ArgumentNullException(nameof(queryOptions));
            }

            DnsQuerySettings settings;
            if (queryOptions.NameServers.Any() && queryOptions.AutoResolvedNameServers == false)
            {
                // fallback to already configured nameservers in case none are specified.
                settings = new DnsQuerySettings(queryOptions, Settings.NameServers);
            }
            else
            {
                settings = new DnsQuerySettings(queryOptions);
            }

            return QueryInternal(question, settings);
        }

        /// <summary>
        /// Performs a DNS lookup for the given <paramref name="query" />, <paramref name="queryType" /> and <paramref name="queryClass" />.
        /// </summary>
        /// <param name="query">The domain name query.</param>
        /// <param name="queryType">The <see cref="QueryType" />.</param>
        /// <param name="queryClass">The <see cref="QueryClass" />.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="queryOptions">Query options to be used instead of <see cref="LookupClient"/>'s settings.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which contains the response headers and lists of resource records.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="query"/> is null.</exception>
        /// <exception cref="OperationCanceledException">If cancellation has been requested for the passed in <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        /// <remarks>
        /// The behavior of the query can be controlled by default settings of this <see cref="LookupClient"/> instance or via <paramref name="queryOptions"/>.
        /// <see cref="Recursion"/> for example can be disabled and would instruct the DNS server to return no additional records.
        /// </remarks>
        public Task<IDnsQueryResponse> QueryAsync(string query, QueryType queryType, QueryClass queryClass = QueryClass.IN, DnsQueryOptions queryOptions = null, CancellationToken cancellationToken = default)
        {
            var question = new DnsQuestion(query, queryType, queryClass);
            if (queryOptions == null)
            {
                return QueryInternalAsync(question, Settings, cancellationToken: cancellationToken);
            }

            return QueryAsync(question, queryOptions, cancellationToken);
        }

        /// <summary>
        /// Performs a DNS lookup for the given <paramref name="question" />.
        /// </summary>
        /// <param name="question">The domain name query.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which contains the response headers and lists of resource records.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="question"/> is null.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public Task<IDnsQueryResponse> QueryAsync(DnsQuestion question, CancellationToken cancellationToken = default)
            => QueryInternalAsync(question, Settings, cancellationToken: cancellationToken);

        /// <summary>
        /// Performs a DNS lookup for the given <paramref name="question" />.
        /// </summary>
        /// <param name="question">The domain name query.</param>
        /// <param name="queryOptions">Query options to be used instead of <see cref="LookupClient"/>'s settings.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which contains the response headers and lists of resource records.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="question"/> or <paramref name="queryOptions"/> is null.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public Task<IDnsQueryResponse> QueryAsync(DnsQuestion question, DnsQueryOptions queryOptions, CancellationToken cancellationToken = default)
        {
            if (queryOptions == null)
            {
                throw new ArgumentNullException(nameof(queryOptions));
            }

            DnsQuerySettings settings;
            if (queryOptions.NameServers.Any() && queryOptions.AutoResolvedNameServers == false)
            {
                // fallback to already configured nameservers in case none are specified.
                settings = new DnsQuerySettings(queryOptions, Settings.NameServers);
            }
            else
            {
                settings = new DnsQuerySettings(queryOptions);
            }

            return QueryInternalAsync(question, settings, cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Performs a DNS lookup for the given <paramref name="query" />, <paramref name="queryType" /> and <paramref name="queryClass" />
        /// using only the passed in <paramref name="servers"/>.
        /// </summary>
        /// <remarks>
        /// To query specific servers can be useful in cases where you have to use a different DNS server than initially configured
        /// (without creating a new instance of <see cref="ILookupClient"/> for example).
        /// </remarks>
        /// <param name="servers">The list of one or more server(s) which should be used for the lookup.</param>
        /// <param name="query">The domain name query.</param>
        /// <param name="queryType">The <see cref="QueryType" />.</param>
        /// <param name="queryClass">The <see cref="QueryClass" />.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which contains the response headers and lists of resource records.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">If the <paramref name="servers"/> collection doesn't contain any elements.</exception>
        /// <exception cref="ArgumentNullException">If <paramref name="query"/> is null.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public IDnsQueryResponse QueryServer(IReadOnlyCollection<NameServer> servers, string query, QueryType queryType, QueryClass queryClass = QueryClass.IN)
        {
            if (servers == null)
            {
                throw new ArgumentNullException(nameof(servers));
            }

            var question = new DnsQuestion(query, queryType, queryClass);

            return QueryInternal(question, Settings, servers);
        }

        /// <summary>
        /// Performs a DNS lookup for the given <paramref name="query" />, <paramref name="queryType" /> and <paramref name="queryClass" />
        /// using only the passed in <paramref name="servers"/>.
        /// </summary>
        /// <remarks>
        /// To query specific servers can be useful in cases where you have to use a different DNS server than initially configured
        /// (without creating a new instance of <see cref="ILookupClient"/> for example).
        /// </remarks>
        /// <param name="servers">The list of one or more server(s) which should be used for the lookup.</param>
        /// <param name="query">The domain name query.</param>
        /// <param name="queryType">The <see cref="QueryType" />.</param>
        /// <param name="queryClass">The <see cref="QueryClass" />.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which contains the response headers and lists of resource records.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">If the <paramref name="servers"/> collection doesn't contain any elements.</exception>
        /// <exception cref="ArgumentNullException">If <paramref name="query"/> is null.</exception>
        /// <exception cref="OperationCanceledException">If cancellation has been requested for the passed in <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public Task<IDnsQueryResponse> QueryServerAsync(IReadOnlyCollection<NameServer> servers, string query, QueryType queryType, QueryClass queryClass = QueryClass.IN, CancellationToken cancellationToken = default)
        {
            if (servers == null)
            {
                throw new ArgumentNullException(nameof(servers));
            }

            var question = new DnsQuestion(query, queryType, queryClass);

            return QueryInternalAsync(question, Settings, servers, cancellationToken);
        }

        /// <summary>
        /// Does a reverse lookup for the <paramref name="ipAddress" />
        /// using only the passed in <paramref name="servers"/>.
        /// </summary>
        /// <param name="servers">The list of one or more server(s) which should be used for the lookup.</param>
        /// <param name="ipAddress">The <see cref="IPAddress" />.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which might contain the <see cref="DnsClient.Protocol.PtrRecord" /> for the <paramref name="ipAddress"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">If the <paramref name="servers"/> collection doesn't contain any elements.</exception>
        /// <exception cref="ArgumentNullException">If <paramref name="ipAddress"/> is null.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public IDnsQueryResponse QueryServerReverse(IReadOnlyCollection<NameServer> servers, IPAddress ipAddress)
        {
            if (servers == null)
            {
                throw new ArgumentNullException(nameof(servers));
            }

            return QueryInternal(GetReverseQuestion(ipAddress), Settings, servers);
        }

        /// <summary>
        /// Does a reverse lookup for the <paramref name="ipAddress" />
        /// using only the passed in <paramref name="servers"/>.
        /// </summary>
        /// <param name="servers">The list of one or more server(s) which should be used for the lookup.</param>
        /// <param name="ipAddress">The <see cref="IPAddress" />.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The <see cref="IDnsQueryResponse" /> which might contain the <see cref="DnsClient.Protocol.PtrRecord" /> for the <paramref name="ipAddress"/>.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">If the <paramref name="servers"/> collection doesn't contain any elements.</exception>
        /// <exception cref="ArgumentNullException">If <paramref name="ipAddress"/> is null.</exception>
        /// <exception cref="OperationCanceledException">If cancellation has been requested for the passed in <paramref name="cancellationToken"/>.</exception>
        /// <exception cref="DnsResponseException">After retries and fallbacks, if none of the servers were accessible, timed out or (if <see cref="ILookupClient.ThrowDnsErrors"/> is enabled) returned error results.</exception>
        public Task<IDnsQueryResponse> QueryServerReverseAsync(IReadOnlyCollection<NameServer> servers, IPAddress ipAddress, CancellationToken cancellationToken = default)
        {
            if (servers == null)
            {
                throw new ArgumentNullException(nameof(servers));
            }

            return QueryInternalAsync(GetReverseQuestion(ipAddress), Settings, servers, cancellationToken);
        }

        private IDnsQueryResponse QueryInternal(DnsQuestion question, DnsQuerySettings settings, IReadOnlyCollection<NameServer> useServers = null)
        {
            if (question == null)
            {
                throw new ArgumentNullException(nameof(question));
            }

            DnsRequestHeader head = new DnsRequestHeader(GetNextUniqueId(), settings.Recursion, DnsOpCode.Query);
            DnsRequestMessage request = new DnsRequestMessage(head, question);
            DnsMessageHandler handler = settings.UseTcpOnly ? _tcpFallbackHandler : _messageHandler;

            IReadOnlyCollection<NameServer> servers = useServers ?? settings.ShuffleNameServers();

            if (servers.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(servers), "List of configured name servers must not be empty.");
            }

            return ResolveQuery(servers, settings, handler, request);
        }

        

        // making it internal for unit testing
        internal IDnsQueryResponse ResolveQuery(IReadOnlyCollection<NameServer> servers, DnsQuerySettings settings, DnsMessageHandler handler, DnsRequestMessage request, LookupClientAudit continueAudit = null)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            LookupClientAudit audit = null;
            if (settings.EnableAuditTrail)
            {
                audit = continueAudit ?? new LookupClientAudit(settings);
            }
            
            DnsResponseException lastDnsResponseException = null;
            Exception lastException = null;
            DnsQueryResponse lastQueryResponse = null;

            foreach (NameServer serverInfo in servers)
            {
                var cacheKey = string.Empty;
                if (settings.UseCache)
                {
                    cacheKey = ResponseCache.GetCacheKey(request.Question, serverInfo);
                    var item = ResponseCache.Get(cacheKey);
                    if (item != null)
                    {
                        return item;
                    }
                }

                var tries = 0;
                do
                {
                    tries++;
                    lastDnsResponseException = null;
                    lastException = null;
                    Console.WriteLine($"Resolve query attempt count: {tries}");
                    try
                    {
                        audit?.StartTimer();

                        // calling the Query method on "handler" type - It could be Udp or Tcp.
                        DnsResponseMessage response = handler.Query(serverInfo.IPEndPoint, request, settings.Timeout);

                        response.Audit = audit;

                        if (response.Header.ResultTruncated && settings.UseTcpFallback && handler.GetType() != typeof(DnsTcpMessageHandler))
                        {
                            audit?.AuditTruncatedRetryTcp();

                            return ResolveQuery(new[] { serverInfo }, settings, _tcpFallbackHandler, request, audit);
                        }

                        audit?.AuditResolveServers(servers.Count);
                        audit?.AuditResponseHeader(response.Header);

                        if (settings.EnableAuditTrail && response.Header.ResponseCode != DnsResponseCode.NoError)
                        {
                            audit.AuditResponseError(response.Header.ResponseCode);
                        }

                        HandleOptRecords(settings, audit, serverInfo, response);

                        DnsQueryResponse queryResponse = response.AsQueryResponse(serverInfo.Clone(), settings);

                        audit?.AuditResponse();
                        audit?.AuditEnd(queryResponse);

                        serverInfo.Enabled = true;
                        serverInfo.LastSuccessfulRequest = request;
                        lastQueryResponse = queryResponse;

                        if (response.Header.ResponseCode != DnsResponseCode.NoError &&
                            (settings.ThrowDnsErrors || settings.ContinueOnDnsError))
                        {
                            throw new DnsResponseException(response.Header.ResponseCode);
                        }

                        if (settings.UseCache)
                        {
                            ResponseCache.Add(cacheKey, queryResponse);
                        }

                        // TODO: trigger here?
                        RunHealthCheck();

                        return queryResponse;
                    }
                    catch (DnsResponseException ex)
                    {
                        ////audit.AuditException(ex);
                        ex.AuditTrail = audit?.Build(null);
                        lastDnsResponseException = ex;

                        if (settings.ContinueOnDnsError)
                        {
                            break; // don't retry this server, response was kinda valid
                        }

                        throw ex;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                    {
                        // this socket error might indicate the server endpoint is actually bad and should be ignored in future queries.
                        DisableServer(serverInfo);
                        break;
                    }
                    catch (Exception ex) when (
                        ex is TimeoutException
                        || handler.IsTransientException(ex)
                        || ex is OperationCanceledException)
                    {
                        DisableServer(serverInfo);
                        continue;
                        // retrying the same server...
                    }
                    catch (Exception ex)
                    {
                        DisableServer(serverInfo);

                        audit?.AuditException(ex);

                        lastException = ex;

                        // not retrying the same server, use next or return
                        break;
                    }
                } while (tries <= settings.Retries && serverInfo.Enabled);

                if (settings.EnableAuditTrail && servers.Count > 1 && serverInfo != servers.Last())
                {
                    audit?.AuditRetryNextServer(serverInfo);
                }
            }

            if (lastDnsResponseException != null && settings.ThrowDnsErrors)
            {
                throw lastDnsResponseException;
            }

            if (lastQueryResponse != null)
            {
                return lastQueryResponse;
            }

            if (lastException != null)
            {
                throw new DnsResponseException(DnsResponseCode.Unassigned, "Unhandled exception", lastException)
                {
                    AuditTrail = audit?.Build(null)
                };
            }

            throw new DnsResponseException(DnsResponseCode.ConnectionTimeout, $"No connection could be established to any of the following name servers: {string.Join(", ", servers)}.")
            {
                AuditTrail = audit?.Build(null)
            };
        }

        private Task<IDnsQueryResponse> QueryInternalAsync(DnsQuestion question, DnsQuerySettings settings, IReadOnlyCollection<NameServer> useServers = null, CancellationToken cancellationToken = default)
        {
            if (question == null)
            {
                throw new ArgumentNullException(nameof(question));
            }
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var head = new DnsRequestHeader(GetNextUniqueId(), settings.Recursion, DnsOpCode.Query);
            var request = new DnsRequestMessage(head, question);
            var handler = settings.UseTcpOnly ? _tcpFallbackHandler : _messageHandler;

            var servers = useServers ?? settings.ShuffleNameServers();

            if (servers.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(servers), "List of configured name servers must not be empty.");
            }

            return ResolveQueryAsync(servers, settings, handler, request, cancellationToken: cancellationToken);
        }
        internal async Task<IDnsQueryResponse> ResolveQueryAsync(IReadOnlyCollection<NameServer> servers, DnsQuerySettings settings, DnsMessageHandler handler, DnsRequestMessage request, LookupClientAudit continueAudit = null, CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            LookupClientAudit audit = null;
            if (settings.EnableAuditTrail)
            {
                audit = continueAudit ?? new LookupClientAudit(settings);
            }

            DnsResponseException lastDnsResponseException = null;
            Exception lastException = null;
            DnsQueryResponse lastQueryResponse = null;

            foreach (var serverInfo in servers)
            {
                var cacheKey = string.Empty;
                if (settings.UseCache)
                {
                    cacheKey = ResponseCache.GetCacheKey(request.Question, serverInfo);
                    var item = ResponseCache.Get(cacheKey);
                    if (item != null)
                    {
                        return item;
                    }
                }

                var tries = 0;
                do
                {
                    tries++;
                    lastDnsResponseException = null;
                    lastException = null;

                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        audit?.StartTimer();

                        DnsResponseMessage response;
                        Action onCancel = () => { };
                        Task<DnsResponseMessage> resultTask = handler.QueryAsync(serverInfo.IPEndPoint, request, cancellationToken, (cancel) =>
                        {
                            onCancel = cancel;
                        });

                        if (settings.Timeout != System.Threading.Timeout.InfiniteTimeSpan || (cancellationToken != CancellationToken.None && cancellationToken.CanBeCanceled))
                        {
                            var cts = new CancellationTokenSource(settings.Timeout);
                            CancellationTokenSource linkedCts = null;
                            if (cancellationToken != CancellationToken.None)
                            {
                                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                            }
                            using (cts)
                            using (linkedCts)
                            {
                                response = await resultTask.WithCancellation((linkedCts ?? cts).Token, onCancel).ConfigureAwait(false);
                            }
                        }
                        else
                        {
                            response = await resultTask.ConfigureAwait(false);
                        }

                        response.Audit = audit;

                        // TODO: better way to prevent infinity looping TCP calls (remove GetType.Equals...)
                        if (response.Header.ResultTruncated && settings.UseTcpFallback && !handler.GetType().Equals(typeof(DnsTcpMessageHandler)))
                        {
                            audit?.AuditTruncatedRetryTcp();

                            return await ResolveQueryAsync(new[] { serverInfo }, settings, _tcpFallbackHandler, request, audit, cancellationToken).ConfigureAwait(false);
                        }

                        audit?.AuditResolveServers(servers.Count);
                        audit?.AuditResponseHeader(response.Header);

                        if (settings.EnableAuditTrail && response.Header.ResponseCode != DnsResponseCode.NoError)
                        {
                            audit?.AuditResponseError(response.Header.ResponseCode);
                        }

                        HandleOptRecords(settings, audit, serverInfo, response);

                        DnsQueryResponse queryResponse = response.AsQueryResponse(serverInfo.Clone(), settings);

                        audit?.AuditResponse();
                        audit?.AuditEnd(queryResponse);

                        // got a valid result, lets enabled the server again if it was disabled
                        serverInfo.Enabled = true;
                        lastQueryResponse = queryResponse;
                        serverInfo.LastSuccessfulRequest = request;

                        if (response.Header.ResponseCode != DnsResponseCode.NoError &&
                            (settings.ThrowDnsErrors || settings.ContinueOnDnsError))
                        {
                            throw new DnsResponseException(response.Header.ResponseCode);
                        }

                        if (settings.UseCache)
                        {
                            ResponseCache.Add(cacheKey, queryResponse);
                        }

                        // TODO: trigger here?
                        RunHealthCheck();

                        return queryResponse;
                    }
                    catch (DnsResponseException ex)
                    {
                        ex.AuditTrail = audit?.Build(null);
                        lastDnsResponseException = ex;

                        if (settings.ContinueOnDnsError)
                        {
                            break; // don't retry this server, response was kinda valid
                        }

                        throw;
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.AddressFamilyNotSupported)
                    {
                        // this socket error might indicate the server endpoint is actually bad and should be ignored in future queries.
                        DisableServer(serverInfo);
                        break;
                    }
                    catch (Exception ex) when (
                        ex is TimeoutException timeoutEx
                        || handler.IsTransientException(ex)
                        || ex is OperationCanceledException)
                    {
                        // user's token got canceled, throw right away...
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }

                        DisableServer(serverInfo);
                    }
                    catch (Exception ex)
                    {
                        DisableServer(serverInfo);

                        if (ex is AggregateException agg)
                        {
                            agg.Handle((e) =>
                            {
                                if (e is TimeoutException
                                    || handler.IsTransientException(e)
                                    || e is OperationCanceledException)
                                {
                                    if (cancellationToken.IsCancellationRequested)
                                    {
                                        throw new OperationCanceledException(cancellationToken);
                                    }

                                    return true;
                                }

                                return false;
                            });
                        }

                        audit?.AuditException(ex);
                        lastException = ex;

                        // try next server (this is actually a change and is not configurable, but should be a good thing I guess)
                        break;
                    }
                } while (tries <= settings.Retries && !cancellationToken.IsCancellationRequested && serverInfo.Enabled);

                if (settings.EnableAuditTrail && servers.Count > 1 && serverInfo != servers.Last())
                {
                    audit?.AuditRetryNextServer(serverInfo);
                }
            }

            if (lastDnsResponseException != null && settings.ThrowDnsErrors)
            {
                throw lastDnsResponseException;
            }

            if (lastQueryResponse != null)
            {
                return lastQueryResponse;
            }

            if (lastException != null)
            {
                throw new DnsResponseException(DnsResponseCode.Unassigned, "Unhandled exception", lastException)
                {
                    AuditTrail = audit?.Build(null)
                };
            }

            throw new DnsResponseException(DnsResponseCode.ConnectionTimeout, $"No connection could be established to any of the following name servers: {string.Join(", ", servers)}.")
            {
                AuditTrail = audit?.Build(null)
            };
        }

        private static DnsQuestion GetReverseQuestion(IPAddress ipAddress)
        {
            if (ipAddress == null)
            {
                throw new ArgumentNullException(nameof(ipAddress));
            }

            var arpa = ipAddress.GetArpaName();
            return new DnsQuestion(arpa, QueryType.PTR, QueryClass.IN);
        }

        private void HandleOptRecords(DnsQuerySettings settings, LookupClientAudit audit, NameServer serverInfo, DnsResponseMessage response)
        {
            OptRecord record = null;
            foreach (var add in response.Additionals)
            {
                if (add is OptRecord optRecord)
                {
                    record = optRecord;
                }
            }

            if (record != null)
            {
                if (settings.EnableAuditTrail)
                {
                    audit.AuditOptPseudo();
                }

                serverInfo.SupportedUdpPayloadSize = record.UdpSize;

                // TODO: handle opt records and remove them later
                response.Additionals.Remove(record);

                if (settings.EnableAuditTrail)
                {
                    audit.AuditEdnsOpt(record.UdpSize, record.Version, record.ResponseCodeEx);
                }
            }
        }

        private void RunHealthCheck()
        {
            // TickCount jump every 25days to int.MinValue, adjusting...
            var currentTicks = Environment.TickCount & int.MaxValue;
            if (_lastHealthCheck + s_serverHealthCheckInterval < 0 || currentTicks + s_serverHealthCheckInterval < 0) _lastHealthCheck = 0;
            if (!_healthCheckRunning && _lastHealthCheck + s_serverHealthCheckInterval < currentTicks)
            {
                _lastHealthCheck = currentTicks;

                var source = new CancellationTokenSource(TimeSpan.FromMinutes(1));

                Task.Factory.StartNew(
                    state => DoHealthCheck((CancellationToken)state),
                    source.Token,
                    source.Token,
                    TaskCreationOptions.DenyChildAttach,
                    TaskScheduler.Default);
            }
        }

        private async Task DoHealthCheck(CancellationToken cancellationToken)
        {
            _healthCheckRunning = true;

            foreach (var server in Settings.NameServers)
            {
                if (!server.Enabled && server.LastSuccessfulRequest != null)
                {
                    try
                    {
                        var result = await QueryAsync(
                            server.LastSuccessfulRequest.Question,
                            new DnsQueryOptions(server)
                            {
                                Retries = 0,
                                Timeout = TimeSpan.FromSeconds(10),
                                UseCache = false
                            },
                            cancellationToken);
                    }
                    catch { }
                }
            }

            _healthCheckRunning = false;
        }

        private void DisableServer(NameServer server)
        {
            if (NameServers.Count > 1)
            {
                server.Enabled = false;
            }
        }

        private ushort GetNextUniqueId()
        {
            if (_uniqueId == ushort.MaxValue || _uniqueId == 0)
            {
                Interlocked.Exchange(ref _uniqueId, _random.Next(ushort.MaxValue / 2));
                return (ushort)_uniqueId;
            }

            return unchecked((ushort)Interlocked.Increment(ref _uniqueId));
        }
    }
}