using System;
using System.Collections.Generic;
using System.Linq;
using BestFlex.Shell.Configuration;
using Microsoft.Extensions.Logging;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Service for querying ERP capabilities and enforcing v1.0 scope
    /// </summary>
    public interface ICapabilityService
    {
        bool IsFeatureAvailable(string category, string feature);
        FeatureStatus GetFeatureStatus(string category, string feature);
        FeatureStatus GetRouteStatus(string route);
        IEnumerable<Feature> GetProductionFeatures();
        IEnumerable<Feature> GetDevelopmentFeatures();
        IEnumerable<Feature> GetComingSoonFeatures();
        void LogCapabilities();
        string GetCapabilitySummary();
    }

    public class CapabilityService : ICapabilityService
    {
        private readonly ILogger<CapabilityService> _logger;

        public CapabilityService(ILogger<CapabilityService> logger)
        {
            _logger = logger;
        }

        public bool IsFeatureAvailable(string category, string feature)
        {
            return ErpCapabilityManifest.IsFeatureAvailable(category, feature);
        }

        public FeatureStatus GetFeatureStatus(string category, string feature)
        {
            if (!ErpCapabilityManifest.Capabilities.TryGetValue(category, out var featureCategory))
                return FeatureStatus.ComingSoon;
                
            if (!featureCategory.Features.TryGetValue(feature, out var featureInfo))
                return FeatureStatus.ComingSoon;
                
            return featureInfo.Status;
        }

        public FeatureStatus GetRouteStatus(string route)
        {
            return ErpCapabilityManifest.GetRouteStatus(route);
        }

        public IEnumerable<Feature> GetProductionFeatures()
        {
            return ErpCapabilityManifest.GetProductionFeatures();
        }

        public IEnumerable<Feature> GetDevelopmentFeatures()
        {
            return ErpCapabilityManifest.GetDevelopmentFeatures();
        }

        public IEnumerable<Feature> GetComingSoonFeatures()
        {
            return ErpCapabilityManifest.GetComingSoonFeatures();
        }

        public void LogCapabilities()
        {
            _logger.LogInformation("=== {Version} CAPABILITY MANIFEST ===", ErpCapabilityManifest.ReleaseName);
            
            var productionFeatures = GetProductionFeatures().ToList();
            var developmentFeatures = GetDevelopmentFeatures().ToList();
            var comingSoonFeatures = GetComingSoonFeatures().ToList();

            _logger.LogInformation("✅ PRODUCTION READY ({count}): {features}", 
                productionFeatures.Count, 
                string.Join(", ", productionFeatures.Select(f => f.Name)));

            _logger.LogInformation("🚧 IN DEVELOPMENT ({count}): {features}", 
                developmentFeatures.Count, 
                string.Join(", ", developmentFeatures.Select(f => f.Name)));

            _logger.LogInformation("❌ COMING SOON ({count}): {features}", 
                comingSoonFeatures.Count, 
                string.Join(", ", comingSoonFeatures.Select(f => f.Name)));

            _logger.LogInformation("=== END CAPABILITY MANIFEST ===");
        }

        public string GetCapabilitySummary()
        {
            var productionFeatures = GetProductionFeatures().ToList();
            var developmentFeatures = GetDevelopmentFeatures().ToList();
            var comingSoonFeatures = GetComingSoonFeatures().ToList();

            return $"{ErpCapabilityManifest.ReleaseName}\n" +
                   $"✅ {productionFeatures.Count} Production Features\n" +
                   $"🚧 {developmentFeatures.Count} In Development\n" +
                   $"❌ {comingSoonFeatures.Count} Coming Soon";
        }
    }
}
