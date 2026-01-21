using System;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Infrastructure.Services
{
    public sealed class PrintingAvailabilityService : IPrintingAvailabilityService
    {
        private readonly ILogger<PrintingAvailabilityService> _logger;

        public PrintingAvailabilityService(ILogger<PrintingAvailabilityService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool IsPrintingAvailable
        {
            get
            {
                var printingModuleType =
                    Type.GetType("BestFlex.Printing.IInvoicePrintEngine, BestFlex.Printing");

                var available = printingModuleType != null;
                _logger.LogDebug("Printing availability checked: {Available}", available);
                return available;
            }
        }

        public string GetPrintingUnavailableReason()
        {
            return IsPrintingAvailable
                ? string.Empty
                : "Printing module is not installed. Install BestFlex.Printing to enable printing features.";
        }
    }
}
