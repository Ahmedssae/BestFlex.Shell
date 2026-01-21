namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Feature severity levels for ERP system
    /// CORE features are fatal if missing - they prevent basic ERP operation
    /// OPTIONAL features are non-fatal if missing - they enhance functionality
    /// </summary>
    public enum FeatureSeverity
    {
        Core,      // Fatal if missing - prevents basic ERP operation
        Optional    // Non-fatal if missing - enhances functionality
    }

    /// <summary>
    /// Feature definition with severity level
    /// </summary>
    public record FeatureDefinition(
        string Name,
        FeatureSeverity Severity,
        string Description
    );

    /// <summary>
    /// Global feature service for managing feature availability
    /// </summary>
    public interface IFeatureService
    {
        /// <summary>Check if a feature is available</summary>
        bool IsFeatureAvailable(string featureName);
        
        /// <summary>Get feature availability reason</summary>
        string? GetFeatureUnavailableReason(string featureName);
        
        /// <summary>Get feature definition</summary>
        FeatureDefinition? GetFeatureDefinition(string featureName);
        
        /// <summary>Register a feature definition</summary>
        void RegisterFeature(FeatureDefinition feature);
        
        /// <summary>Get all features by severity</summary>
        IEnumerable<FeatureDefinition> GetFeaturesBySeverity(FeatureSeverity severity);
        
        /// <summary>Check if any core features are unavailable</summary>
        bool HasUnavailableCoreFeatures();
        
        /// <summary>Get unavailable core features</summary>
        IEnumerable<FeatureDefinition> GetUnavailableCoreFeatures();
    }
}
