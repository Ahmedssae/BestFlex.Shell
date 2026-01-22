using BestFlex.Application.Abstractions;

namespace BestFlex.Domain.Entities
{
    public sealed class ForensicEventEntity
    {
        public long Id { get; private set; }

        public ForensicEventType EventType { get; private set; }
        public DateTime OccurredAtUtc { get; private set; }
        public string MachineName { get; private set; } = null!;
        public string UserName { get; private set; } = null!;
        public string Description { get; private set; } = null!;
        public string? CorrelationId { get; private set; }
        public string? StackTrace { get; private set; }

        private ForensicEventEntity() { }

        public ForensicEventEntity(ForensicEvent evt)
        {
            EventType = evt.EventType;
            OccurredAtUtc = evt.OccurredAtUtc;
            MachineName = evt.MachineName;
            UserName = evt.UserName;
            Description = evt.Description;
            CorrelationId = evt.CorrelationId;
            StackTrace = evt.StackTrace;
        }
    }
}
