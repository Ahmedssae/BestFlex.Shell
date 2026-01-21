namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Service to check printing availability in the ERP system
    /// </summary>
    public interface IPrintingAvailabilityService
    {
        /// <summary>Check if printing is available</summary>
        bool IsPrintingAvailable { get; }
        
        /// <summary>Get the reason why printing is unavailable</summary>
        string? GetPrintingUnavailableReason();
    }
}