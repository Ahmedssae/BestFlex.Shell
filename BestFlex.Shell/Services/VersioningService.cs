using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Provides versioning and build identity management
    /// </summary>
    public class VersioningService : IVersioningService
    {
        private readonly ILogger<VersioningService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IStructuredLoggingService _structuredLogger;
        private readonly ICorrelationService _correlationService;

        private static readonly string VersionInfoPath = "version_info.json";
        private static readonly string BuildInfoPath = "build_info.json";

        public VersioningService(
            ILogger<VersioningService> logger,
            ICurrentUserService currentUserService,
            IStructuredLoggingService structuredLogger,
            ICorrelationService correlationService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
            _structuredLogger = structuredLogger;
            _correlationService = correlationService;
        }

        public VersionInfo GetCurrentVersion()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var assemblyVersion = assembly.GetName().Version ?? new Version(1, 0, 0, 0);
                
                // Try to get version from file first (for injected version)
                var fileVersion = LoadVersionFromFile();
                if (fileVersion != null)
                {
                    return fileVersion;
                }

                // Fallback to assembly version
                return new VersionInfo
                {
                    Major = assemblyVersion.Major,
                    Minor = assemblyVersion.Minor,
                    Patch = assemblyVersion.Build,
                    Build = assemblyVersion.Revision,
                    SemanticVersion = $"{assemblyVersion.Major}.{assemblyVersion.Minor}.{assemblyVersion.Build}",
                    FullVersion = assemblyVersion.ToString(),
                    BuildDate = File.GetLastWriteTime(assembly.Location),
                    BuildType = "Release"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VERSION_LOAD_ERROR] Error loading version information");
                
                return new VersionInfo
                {
                    Major = 1,
                    Minor = 0,
                    Patch = 0,
                    Build = 0,
                    SemanticVersion = "1.0.0",
                    FullVersion = "1.0.0.0",
                    BuildDate = DateTime.UtcNow,
                    BuildType = "Unknown"
                };
            }
        }

        public BuildInfo GetBuildInfo()
        {
            try
            {
                // Try to load from file first
                var fileBuildInfo = LoadBuildInfoFromFile();
                if (fileBuildInfo != null)
                {
                    return fileBuildInfo;
                }

                // Generate build info from current assembly
                return GenerateBuildInfo();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[BUILD_INFO_ERROR] Error loading build information");
                
                return new BuildInfo
                {
                    BuildId = "unknown",
                    BuildTimestamp = DateTime.UtcNow,
                    CommitHash = "unknown",
                    Branch = "unknown",
                    BuildNumber = 0,
                    BuildType = "Unknown"
                };
            }
        }

        public async Task<bool> InjectVersion(VersionInfo version, BuildInfo buildInfo)
        {
            try
            {
                // Validate version
                if (!IsValidSemanticVersion(version))
                {
                    _logger.LogError("[VERSION_INJECT_ERROR] Invalid semantic version: {Version}", version.SemanticVersion);
                    return false;
                }

                // Save version info
                await SaveVersionToFile(version);
                await SaveBuildInfoToFile(buildInfo);

                _structuredLogger.LogUserAction("VersionInjected", "System", new Dictionary<string, object>
                {
                    ["Version"] = version.SemanticVersion,
                    ["BuildId"] = buildInfo.BuildId,
                    ["BuildType"] = buildInfo.BuildType
                });

                _logger.LogInformation("[VERSION_INJECTED] [Version:{Version}] [BuildId:{BuildId}] Version injected successfully", 
                    version.SemanticVersion, buildInfo.BuildId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VERSION_INJECT_ERROR] Error injecting version");
                return false;
            }
        }

        public bool IsVersionCompatible(VersionInfo requiredVersion, VersionInfo currentVersion)
        {
            // Semantic versioning compatibility rules:
            // - Major version must match exactly
            // - Current minor must be >= required minor
            // - Current patch must be >= required patch (if major and minor match)
            
            if (requiredVersion.Major != currentVersion.Major)
            {
                return false;
            }

            if (currentVersion.Minor < requiredVersion.Minor)
            {
                return false;
            }

            if (currentVersion.Minor == requiredVersion.Minor && currentVersion.Patch < requiredVersion.Patch)
            {
                return false;
            }

            return true;
        }

        public VersionComparisonResult CompareVersions(VersionInfo version1, VersionInfo version2)
        {
            // Compare semantic versions
            var majorComparison = version1.Major.CompareTo(version2.Major);
            if (majorComparison != 0)
            {
                return new VersionComparisonResult
                {
                    Comparison = majorComparison,
                    IsCompatible = false,
                    Message = $"Major version difference: {version1.Major} vs {version2.Major}"
                };
            }

            var minorComparison = version1.Minor.CompareTo(version2.Minor);
            if (minorComparison != 0)
            {
                return new VersionComparisonResult
                {
                    Comparison = minorComparison,
                    IsCompatible = version1.Minor >= version2.Minor,
                    Message = $"Minor version difference: {version1.Minor} vs {version2.Minor}"
                };
            }

            var patchComparison = version1.Patch.CompareTo(version2.Patch);
            return new VersionComparisonResult
            {
                Comparison = patchComparison,
                IsCompatible = true,
                Message = patchComparison == 0 ? "Versions are identical" : 
                          patchComparison > 0 ? "Version is newer" : "Version is older"
            };
        }

        public string GetVersionDisplayText()
        {
            var version = GetCurrentVersion();
            var buildInfo = GetBuildInfo();
            
            return $"BestFlex ERP v{version.SemanticVersion} (Build {buildInfo.BuildId})";
        }

        public string GetDetailedVersionInfo()
        {
            var version = GetCurrentVersion();
            var buildInfo = GetBuildInfo();
            
            return $"Version: {version.SemanticVersion}\n" +
                   $"Build: {buildInfo.BuildId}\n" +
                   $"Build Date: {buildInfo.BuildTimestamp:yyyy-MM-dd HH:mm:ss}\n" +
                   $"Commit: {buildInfo.CommitHash}\n" +
                   $"Branch: {buildInfo.Branch}\n" +
                   $"Build Type: {buildInfo.BuildType}";
        }

        public async Task<VersionValidationResult> ValidateVersionIntegrity()
        {
            try
            {
                var version = GetCurrentVersion();
                var buildInfo = GetBuildInfo();
                var result = new VersionValidationResult
                {
                    IsValid = true,
                    Version = version,
                    BuildInfo = buildInfo
                };

                // Validate semantic version format
                if (!IsValidSemanticVersion(version))
                {
                    result.IsValid = false;
                    result.Errors.Add("Invalid semantic version format");
                }

                // Validate build info
                if (string.IsNullOrWhiteSpace(buildInfo.BuildId))
                {
                    result.IsValid = false;
                    result.Errors.Add("Build ID is missing");
                }

                // Validate build date
                if (buildInfo.BuildTimestamp == default)
                {
                    result.IsValid = false;
                    result.Errors.Add("Build timestamp is invalid");
                }

                // Check if build is too old (more than 5 years)
                if (buildInfo.BuildTimestamp < DateTime.UtcNow.AddYears(-5))
                {
                    result.Warnings.Add("Build is very old and may not be supported");
                }

                // Validate version files exist and are consistent
                await ValidateVersionFiles(result);

                _structuredLogger.LogSystemEvent("VersionValidation", result.IsValid ? "Success" : "Failed", 
                    result.IsValid ? "Version integrity validated" : string.Join("; ", result.Errors));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[VERSION_VALIDATION_ERROR] Error validating version integrity");
                
                return new VersionValidationResult
                {
                    IsValid = false,
                    Errors = new List<string> { $"Validation error: {ex.Message}" }
                };
            }
        }

        private VersionInfo? LoadVersionFromFile()
        {
            try
            {
                if (!File.Exists(VersionInfoPath))
                    return null;

                var json = File.ReadAllText(VersionInfoPath);
                return JsonSerializer.Deserialize<VersionInfo>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[VERSION_FILE_ERROR] Error loading version from file: {File}", VersionInfoPath);
                return null;
            }
        }

        private BuildInfo? LoadBuildInfoFromFile()
        {
            try
            {
                if (!File.Exists(BuildInfoPath))
                    return null;

                var json = File.ReadAllText(BuildInfoPath);
                return JsonSerializer.Deserialize<BuildInfo>(json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[BUILD_FILE_ERROR] Error loading build info from file: {File}", BuildInfoPath);
                return null;
            }
        }

        private async Task SaveVersionToFile(VersionInfo version)
        {
            var json = JsonSerializer.Serialize(version, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(VersionInfoPath, json);
        }

        private async Task SaveBuildInfoToFile(BuildInfo buildInfo)
        {
            var json = JsonSerializer.Serialize(buildInfo, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(BuildInfoPath, json);
        }

        private BuildInfo GenerateBuildInfo()
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            return new BuildInfo
            {
                BuildId = GenerateBuildId(),
                BuildTimestamp = File.GetLastWriteTime(assembly.Location),
                CommitHash = GetCommitHash(),
                Branch = GetBranchName(),
                BuildNumber = Environment.GetEnvironmentVariable("BUILD_NUMBER")?.ToInt32() ?? 0,
                BuildType = GetBuildType()
            };
        }

        private string GenerateBuildId()
        {
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var hash = GenerateShortHash();
            return $"B{timestamp}_{hash}";
        }

        private string GenerateShortHash()
        {
            using var sha256 = SHA256.Create();
            var input = $"{DateTime.UtcNow.Ticks}{Environment.MachineName}{Guid.NewGuid()}";
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(hash)[..8].ToLowerInvariant();
        }

        private string GetCommitHash()
        {
            // In a real implementation, this would get the actual Git commit hash
            // For now, return a placeholder
            return "unknown";
        }

        private string GetBranchName()
        {
            // In a real implementation, this would get the actual Git branch
            // For now, try to get from environment variable
            return Environment.GetEnvironmentVariable("GIT_BRANCH") ?? "main";
        }

        private string GetBuildType()
        {
            // Determine build type from compilation symbols or environment
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }

        private bool IsValidSemanticVersion(VersionInfo version)
        {
            return version.Major >= 0 && version.Minor >= 0 && version.Patch >= 0;
        }

        private async Task ValidateVersionFiles(VersionValidationResult result)
        {
            // Check version file consistency
            var fileVersion = LoadVersionFromFile();
            if (fileVersion != null)
            {
                if (fileVersion.SemanticVersion != result.Version.SemanticVersion)
                {
                    result.Warnings.Add("Version file differs from assembly version");
                }
            }
            else
            {
                result.Warnings.Add("Version file not found");
            }

            // Check build info file consistency
            var fileBuildInfo = LoadBuildInfoFromFile();
            if (fileBuildInfo != null)
            {
                if (fileBuildInfo.BuildId != result.BuildInfo.BuildId)
                {
                    result.Warnings.Add("Build info file differs from generated build info");
                }
            }
            else
            {
                result.Warnings.Add("Build info file not found");
            }
        }
    }

    public interface IVersioningService
    {
        VersionInfo GetCurrentVersion();
        BuildInfo GetBuildInfo();
        Task<bool> InjectVersion(VersionInfo version, BuildInfo buildInfo);
        bool IsVersionCompatible(VersionInfo requiredVersion, VersionInfo currentVersion);
        VersionComparisonResult CompareVersions(VersionInfo version1, VersionInfo version2);
        string GetVersionDisplayText();
        string GetDetailedVersionInfo();
        Task<VersionValidationResult> ValidateVersionIntegrity();
    }

    // Data classes
    public class VersionInfo
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
        public int Build { get; set; }
        public string SemanticVersion { get; set; } = string.Empty;
        public string FullVersion { get; set; } = string.Empty;
        public DateTime BuildDate { get; set; }
        public string BuildType { get; set; } = string.Empty;
    }

    public class BuildInfo
    {
        public string BuildId { get; set; } = string.Empty;
        public DateTime BuildTimestamp { get; set; }
        public string CommitHash { get; set; } = string.Empty;
        public string Branch { get; set; } = string.Empty;
        public int BuildNumber { get; set; }
        public string BuildType { get; set; } = string.Empty;
    }

    public class VersionComparisonResult
    {
        public int Comparison { get; set; } // -1: version1 < version2, 0: equal, 1: version1 > version2
        public bool IsCompatible { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class VersionValidationResult
    {
        public bool IsValid { get; set; }
        public VersionInfo Version { get; set; } = new();
        public BuildInfo BuildInfo { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    // Extension methods
    internal static class StringExtensions
    {
        public static int ToInt32(this string? value)
        {
            return int.TryParse(value, out var result) ? result : 0;
        }
    }
}
