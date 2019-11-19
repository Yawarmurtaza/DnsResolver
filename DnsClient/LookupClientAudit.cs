using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace DnsClient
{
    internal class LookupClientAudit
    {
        private const string c_placeHolder = "$$REPLACEME$$";
        private static readonly int s_printOffset = -32;
        private StringBuilder _auditWriter = new StringBuilder();
        private Stopwatch _swatch;

        public DnsQuerySettings Settings { get; }

        public LookupClientAudit(DnsQuerySettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void StartTimer()
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            _swatch = Stopwatch.StartNew();
            _swatch.Restart();
        }

        public void AuditResolveServers(int count)
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            _auditWriter.AppendLine($"; ({count} server found)");
        }

        public string Build(IDnsQueryResponse queryResponse)
        {
            if (!Settings.EnableAuditTrail)
            {
                return string.Empty;
            }

            var writer = new StringBuilder();

            if (queryResponse != null)
            {
                if (queryResponse.Questions.Count > 0)
                {
                    writer.AppendLine(";; QUESTION SECTION:");
                    foreach (var question in queryResponse.Questions)
                    {
                        writer.AppendLine(question.ToString(s_printOffset));
                    }
                    writer.AppendLine();
                }

                if (queryResponse.Answers.Count > 0)
                {
                    writer.AppendLine(";; ANSWER SECTION:");
                    foreach (var answer in queryResponse.Answers)
                    {
                        writer.AppendLine(answer.ToString(s_printOffset));
                    }
                    writer.AppendLine();
                }

                if (queryResponse.Authorities.Count > 0)
                {
                    writer.AppendLine(";; AUTHORITIES SECTION:");
                    foreach (var auth in queryResponse.Authorities)
                    {
                        writer.AppendLine(auth.ToString(s_printOffset));
                    }
                    writer.AppendLine();
                }

                if (queryResponse.Additionals.Count > 0)
                {
                    writer.AppendLine(";; ADDITIONALS SECTION:");
                    foreach (var additional in queryResponse.Additionals)
                    {
                        writer.AppendLine(additional.ToString(s_printOffset));
                    }
                    writer.AppendLine();
                }
            }

            var all = _auditWriter.ToString();
            var dynamic = writer.ToString();

            return all.Replace(c_placeHolder, dynamic);
        }

        public void AuditTruncatedRetryTcp()
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            _auditWriter.AppendLine(";; Truncated, retrying in TCP mode.");
            _auditWriter.AppendLine();
        }

        public void AuditResponseError(DnsResponseCode responseCode)
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            _auditWriter.AppendLine($";; ERROR: {DnsResponseCodeText.GetErrorText(responseCode)}");
        }

        public void AuditOptPseudo()
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            _auditWriter.AppendLine(";; OPT PSEUDOSECTION:");
        }

        public void AuditResponseHeader(DnsResponseHeader header)
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            _auditWriter.AppendLine(";; Got answer:");
            _auditWriter.AppendLine(header.ToString());
            if (header.RecursionDesired && !header.RecursionAvailable)
            {
                _auditWriter.AppendLine(";; WARNING: recursion requested but not available");
            }
            _auditWriter.AppendLine();
        }

        public void AuditEdnsOpt(short udpSize, byte version, DnsResponseCode responseCodeEx)
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            // TODO: flags
            _auditWriter.AppendLine($"; EDNS: version: {version}, flags:; udp: {udpSize}");
        }

        public void AuditResponse()
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            _auditWriter.AppendLine(c_placeHolder);
        }

        public void AuditEnd(DnsQueryResponse queryResponse)
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            var elapsed = _swatch.ElapsedMilliseconds;
            _auditWriter.AppendLine($";; Query time: {elapsed} msec");
            _auditWriter.AppendLine($";; SERVER: {queryResponse.NameServer.Address}#{queryResponse.NameServer.Port}");
            _auditWriter.AppendLine($";; WHEN: {DateTime.UtcNow.ToString("ddd MMM dd HH:mm:ss K yyyy", CultureInfo.InvariantCulture)}");
            _auditWriter.AppendLine($";; MSG SIZE  rcvd: {queryResponse.MessageSize}");
        }

        public void AuditException(Exception ex)
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            var aggEx = ex as AggregateException;
            if (ex is DnsResponseException dnsEx)
            {
                _auditWriter.AppendLine($";; Error: {DnsResponseCodeText.GetErrorText(dnsEx.Code)} {dnsEx.InnerException?.Message ?? dnsEx.Message}");
            }
            else if (aggEx != null)
            {
                _auditWriter.AppendLine($";; Error: {aggEx.InnerException?.Message ?? aggEx.Message}");
            }
            else
            {
                _auditWriter.AppendLine($";; Error: {ex.Message}");
            }

            if (Debugger.IsAttached)
            {
                _auditWriter.AppendLine(ex.ToString());
            }
        }

        public void AuditRetryNextServer(NameServer current)
        {
            if (!Settings.EnableAuditTrail)
            {
                return;
            }

            _auditWriter.AppendLine();
            _auditWriter.AppendLine($"; SERVER: {current.Address}#{current.Port} failed; Retrying with the next server.");
        }
    }
}