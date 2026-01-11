using System.Threading;

namespace BestFlex.Shell.Services
{
    /// <summary>Process-wide, thread-safe store for "last created invoice".</summary>
    public sealed class LastInvoiceTracker : ILastInvoiceTracker
    {
        private int? _id;
        private System.DateTimeOffset? _when;
        private readonly object _gate = new object();

        public int? LastInvoiceId
        {
            get { lock (_gate) return _id; }
            set { lock (_gate) _id = value; }
        }

        public System.DateTimeOffset? When
        {
            get { lock (_gate) return _when; }
            set { lock (_gate) _when = value; }
        }
    }
}
