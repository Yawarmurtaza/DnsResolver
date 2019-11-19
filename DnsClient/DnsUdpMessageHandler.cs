using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DnsClient.Internal;

namespace DnsClient
{
    internal class DnsUdpMessageHandler : DnsMessageHandler
    {
        private const int MaxSize = 4096;
        private static ConcurrentQueue<UdpClient> _clients = new ConcurrentQueue<UdpClient>();
        private static ConcurrentQueue<UdpClient> _clientsIPv6 = new ConcurrentQueue<UdpClient>();
        private readonly bool _enableClientQueue;

        public DnsUdpMessageHandler(bool enableClientQueue)
        {
            _enableClientQueue = enableClientQueue;
        }

        public override bool IsTransientException<T>(T exception)
        {
            if (exception is SocketException) return true;
            return false;
        }

        public override DnsResponseMessage Query(IPEndPoint server, DnsRequestMessage request, TimeSpan timeout)
        {
            using (UdpClient udpClient = GetNextUdpClient(server.AddressFamily))
            {
                Socket client = udpClient.Client;
                // -1 indicates infinite
                int timeoutInMillis = timeout.TotalMilliseconds >= int.MaxValue ? -1 : (int) timeout.TotalMilliseconds;
                client.ReceiveTimeout = timeoutInMillis;
                client.SendTimeout = timeoutInMillis;

                bool mustDispose = false;
                try
                {
                    using (var writer = new DnsDatagramWriter())
                    {
                        GetRequestData(request, writer);

                        // udpClient is a .net FCL class that allows us to access the underlying socket object.
                        client.SendTo(writer.Data.Array, writer.Data.Offset, writer.Data.Count, SocketFlags.None, server);
                    }

                    int readSize = udpClient.Available > MaxSize ? udpClient.Available : MaxSize;

                    using (var memory = new PooledBytes(readSize))
                    {
                        int received = client.Receive(memory.Buffer, 0, readSize, SocketFlags.None);

                        DnsResponseMessage response = GetResponseMessage(new ArraySegment<byte>(memory.Buffer, 0, received));
                        if (request.Header.Id != response.Header.Id)
                        {
                            throw new DnsResponseException("Header id mismatch.");
                        }

                        Enqueue(server.AddressFamily, udpClient);
                        return response;
                    }
                }
                catch
                {
                    mustDispose = true;
                    throw;
                }
                finally
                {
                    if (!_enableClientQueue || mustDispose)
                    {
                        try
                        {
                            udpClient.Close();
                        }
                        catch
                        {
                        }
                    }
                }
            }
        }

        public override async Task<DnsResponseMessage> QueryAsync(
            IPEndPoint endpoint,
            DnsRequestMessage request,
            CancellationToken cancellationToken,
            Action<Action> cancellationCallback)
        {
            cancellationToken.ThrowIfCancellationRequested();

            UdpClient udpClient = GetNextUdpClient(endpoint.AddressFamily);

            bool mustDispose = false;
            try
            {
                // setup timeout cancellation, dispose socket (the only way to actually cancel the request in async...
                cancellationCallback(() =>
                {
                    udpClient.Close();
                });

                using (var writer = new DnsDatagramWriter())
                {
                    GetRequestData(request, writer);
                    await udpClient.SendAsync(writer.Data.Array, writer.Data.Count, endpoint).ConfigureAwait(false);
                }

                var readSize = udpClient.Available > MaxSize ? udpClient.Available : MaxSize;

                using (var memory = new PooledBytes(readSize))
                {
                    var result = await udpClient.ReceiveAsync().ConfigureAwait(false);
                    var response = GetResponseMessage(new ArraySegment<byte>(result.Buffer, 0, result.Buffer.Length));
                    if (request.Header.Id != response.Header.Id)
                    {
                        throw new DnsResponseException("Header id mismatch.");
                    }

                    Enqueue(endpoint.AddressFamily, udpClient);

                    return response;
                }
            }
            catch (ObjectDisposedException)
            {
                // we disposed it in case of a timeout request, lets indicate it actually timed out...
                throw new TimeoutException();
            }
            catch
            {
                mustDispose = true;
                throw;
            }
            finally
            {
                if (!_enableClientQueue || mustDispose)
                {
                    try
                    {
                        udpClient.Close();
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        /// Gets the UdpClient object - thread safe - using given address family e.g. IP v4.
        /// </summary>
        /// <param name="family"></param>
        /// <returns></returns>
        private UdpClient GetNextUdpClient(AddressFamily family)
        {
            UdpClient udpClient = null;
            if (_enableClientQueue)
            {
                while (udpClient == null && !TryDequeue(family, out udpClient))
                { 
                    udpClient = new UdpClient(family);
                }
            }
            else
            {
                udpClient = new UdpClient(family);
            }

            return udpClient;
        }

        private void Enqueue(AddressFamily family, UdpClient client)
        {
            if (_enableClientQueue)
            {
                if (family == AddressFamily.InterNetwork)
                {
                    _clients.Enqueue(client);
                }
                else
                {
                    _clientsIPv6.Enqueue(client);
                }
            }
        }

        private bool TryDequeue(AddressFamily family, out UdpClient client)
        {
            if (family == AddressFamily.InterNetwork)
            {
                return _clients.TryDequeue(out client);
            }

            return _clientsIPv6.TryDequeue(out client);
        }
    }
}