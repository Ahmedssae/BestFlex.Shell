using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using BestFlex.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Services
{
    /// <summary>
    /// Feature service implementation with severity-based availability
    /// </summary>
    public class FeatureService : IFeatureService
    {
        private readonly ILogger<FeatureService> _logger;
        private readonly ConcurrentDictionary<string, FeatureDefinition> _features = new();
        private readonly ConcurrentDictionary<string, (bool Available, string? Reason)> _availability = new();

        public FeatureService(ILogger<FeatureService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Register core features (fatal if missing)
            RegisterFeature(new FeatureDefinition("Authentication", FeatureSeverity.Core, "User authentication and session management"));
            RegisterFeature(new FeatureDefinition("Navigation", FeatureSeverity.Core, "Shell navigation and routing"));
            RegisterFeature(new FeatureDefinition("Sales", FeatureSeverity.Core, "Sales creation and management"));
            RegisterFeature(new FeatureDefinition("ProductLookup", FeatureSeverity.Core, "Product catalog and search"));
            RegisterFeature(new FeatureDefinition("CustomerLookup", FeatureSeverity.Core, "Customer management and search"));
            
            // Register optional features (non-fatal if missing)
            RegisterFeature(new FeatureDefinition("Printing", FeatureSeverity.Optional, "Document printing and PDF export"));
            RegisterFeature(new FeatureDefinition("PrintPreview", FeatureSeverity.Optional, "Print preview functionality"));
            RegisterFeature(new FeatureDefinition("TemplateDesigner", FeatureSeverity.Optional, "Invoice template designer"));
            RegisterFeature(new FeatureDefinition("Reports", FeatureSeverity.Optional, "Business reports and analytics"));
            RegisterFeature(new FeatureDefinition("PdfExport", FeatureSeverity.Optional, "PDF export functionality"));
            
            // Initialize availability based on service registration
            InitializeFeatureAvailability();
        }

        public bool IsFeatureAvailable(string featureName)
        {
            if (string.IsNullOrWhiteSpace(featureName))
                return false;
                
            return _availability.TryGetValue(featureName, out var result) && result.Available;
        }

        public string? GetFeatureUnavailableReason(string featureName)
        {
            if (string.IsNullOrWhiteSpace(featureName))
                return "Invalid feature name";
                
            return _availability.TryGetValue(featureName, out var result) ? result.Reason : "Feature not registered";
        }

        public FeatureDefinition? GetFeatureDefinition(string featureName)
        {
            if (string.IsNullOrWhiteSpace(featureName))
                return null;
                
            _features.TryGetValue(featureName, out var feature);
            return feature;
        }

        public void RegisterFeature(FeatureDefinition feature)
        {
            if (feature == null || string.IsNullOrWhiteSpace(feature.Name))
                return;
                
            _features.TryAdd(feature.Name, feature);
            _logger.LogDebug("Registered feature: {FeatureName} ({Severity})", feature.Name, feature.Severity);
        }

        /// <summary>
        /// Initialize feature availability based on service registration
        /// This is called once during startup to detect available services
        /// </summary>
        private void InitializeFeatureAvailability()
        {
            _logger.LogInformation("Initializing feature availability");
            
            // Core features - assume available unless explicitly marked unavailable
            SetFeatureAvailable("Authentication", true, null);
            SetFeatureAvailable("Navigation", true, null);
            SetFeatureAvailable("Sales", true, null);
            SetFeatureAvailable("ProductLookup", true, null);
            SetFeatureAvailable("CustomerLookup", true, null);
            
            // Optional features - check service availability
            // For now, we'll assume they're available but can be overridden by service detection
            // In a real implementation, this would check for actual service registration
            SetFeatureAvailable("Printing", true, null);
            SetFeatureAvailable("PrintPreview", true, null);
            SetFeatureAvailable("TemplateDesigner", true, null);
            SetFeatureAvailable("Reports", true, null);
            SetFeatureAvailable("PdfExport", true, null);
            
            _logger.LogInformation("Feature availability initialized");
        }

        /// <summary>
        /// Set feature availability (for testing or service detection)
        /// </summary>
        public void SetFeatureAvailable(string featureName, bool available, string? reason)
        {
            if (string.IsNullOrWhiteSpace(featureName))
                return;
                
            _availability.AddOrUpdate(featureName, (available, reason), (_, existing) => (available, reason));
            
            var severity = GetFeatureDefinition(featureName)?.Severity ?? FeatureSeverity.Optional;
            var logLevel = severity == FeatureSeverity.Core && !available ? LogLevel.Critical : LogLevel.Information;
            
            _logger.Log(logLevel, "Feature {FeatureName} ({Severity}): {Available} - {Reason}", 
                featureName, severity, available ? "Available" : "Unavailable", reason ?? "No reason");
        }

        /// <summary>
        /// Mark feature as unavailable (for service detection failures)
        /// </summary>
        public void MarkFeatureUnavailable(string featureName, string reason)
        {
            SetFeatureAvailable(featureName, false, reason);
        }

        /// <summary>
        /// Get all features by severity
        /// </summary>
        public IEnumerable<FeatureDefinition> GetFeaturesBySeverity(FeatureSeverity severity)
        {
            return _features.Values.Where(f => f.Severity == severity);
        }

        /// <summary>
        /// Check if any core features are unavailable
        /// </summary>
        public bool HasUnavailableCoreFeatures()
        {
            return GetFeaturesBySeverity(FeatureSeverity.Core)
                .Any(f => !IsFeatureAvailable(f.Name));
        }

        /// <summary>
        /// Get unavailable core features
        /// </summary>
        public IEnumerable<FeatureDefinition> GetUnavailableCoreFeatures()
        {
            return GetFeaturesBySeverity(FeatureSeverity.Core)
                .Where(f => !IsFeatureAvailable(f.Name));
        }
    }
}
