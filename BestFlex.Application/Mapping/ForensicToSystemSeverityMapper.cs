using BestFlex.Application.Abstractions;

namespace BestFlex.Application.Mapping
{
    public static class ForensicToSystemSeverityMapper
    {
        public static SystemEventSeverity Map(
            BestFlex.Domain.ForensicEventType type)
        {
            return type switch
            {
                BestFlex.Domain.ForensicEventType.Critical =>
                    SystemEventSeverity.Critical,

                BestFlex.Domain.ForensicEventType.Error =>
                    SystemEventSeverity.Error,

                _ => SystemEventSeverity.Error
            };
        }
    }
}
