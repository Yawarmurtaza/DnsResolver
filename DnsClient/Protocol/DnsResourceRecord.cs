using System;

namespace DnsClient.Protocol
{
    /// <summary>
    /// Base class for all resource records.
    /// </summary>
    /// <seealso cref="DnsClient.Protocol.ResourceRecordInfo" />
    public abstract class DnsResourceRecord : ResourceRecordInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DnsResourceRecord" /> class.
        /// </summary>
        /// <param name="info">The information.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="info"/> is null.</exception>
        protected DnsResourceRecord(ResourceRecordInfo info)
            : base(
                  info?.DomainName ?? throw new ArgumentNullException(nameof(info)),
                  info?.RecordType ?? throw new ArgumentNullException(nameof(info)),
                  info?.RecordClass ?? throw new ArgumentNullException(nameof(info)),
                  info?.InitialTimeToLive ?? throw new ArgumentNullException(nameof(info)),
                  info?.RawDataLength ?? throw new ArgumentNullException(nameof(info)))
        {
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return ToString(0);
        }

        /// <summary>
        /// Same as <c>ToString</c> but offsets the <see cref="ResourceRecordInfo.DomainName"/>
        /// by <paramref name="offset"/>.
        /// Set the offset to -32 for example to make it print nicely in consols.
        /// </summary>
        /// <param name="offset">The offset.</param>
        /// <returns>A string representing this instance.</returns>
        public virtual string ToString(int offset = 0)
        {
            return string.Format("{0," + offset + "}{1} \t{2} \t{3} \t{4}",
                DomainName,
                TimeToLive,
                RecordClass,
                RecordType,
                RecordToString());
        }

        /// <summary>
        /// Returns a string representation of the record's value only.
        /// <see cref="ToString(int)"/> uses this to compose the full string value of this instance.
        /// </summary>
        /// <returns>A string representing this record.</returns>
        private protected abstract string RecordToString();
    }
}