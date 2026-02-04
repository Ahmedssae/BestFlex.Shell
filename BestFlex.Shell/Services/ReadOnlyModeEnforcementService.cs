using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    /// <summary>
    /// Enforces real read-only behavior for various system conditions
    /// </summary>
    public class ReadOnlyModeEnforcementService : IReadOnlyModeEnforcementService
    {
        private readonly ILogger<ReadOnlyModeEnforcementService> _logger;
        private readonly ICurrentUserService _currentUserService;
        private readonly IFailSafeModeService _failSafeModeService;
        private readonly ISessionReliabilityService _sessionService;
        private readonly IFeatureService _featureService;

        public ReadOnlyModeEnforcementService(
            ILogger<ReadOnlyModeEnforcementService> logger,
            ICurrentUserService currentUserService,
            IFailSafeModeService failSafeModeService,
            ISessionReliabilityService sessionService,
            IFeatureService featureService)
        {
            _logger = logger;
            _currentUserService = currentUserService;
            _failSafeModeService = failSafeModeService;
            _sessionService = sessionService;
            _featureService = featureService;
        }

        public ReadOnlyModeStatus GetReadOnlyStatus(string? entityType = null, string? operation = null)
        {
            var reasons = new List<string>();
            var isReadOnly = false;

            // Check fail-safe mode
            var failSafeMode = _failSafeModeService.CurrentMode;
            if (failSafeMode == FailSafeMode.ReadOnly || failSafeMode == FailSafeMode.Emergency)
            {
                isReadOnly = true;
                reasons.Add($"System is in {failSafeMode} mode");
            }

            // Check session validity
            if (!_sessionService.IsSessionValid)
            {
                isReadOnly = true;
                reasons.Add("User session is invalid or expired");
            }

            // Check user permissions
            if (!HasWritePermission(entityType))
            {
                isReadOnly = true;
                reasons.Add("User lacks write permissions");
            }

            // Check period status for financial entities
            if (IsFinancialEntity(entityType) && IsPeriodClosed())
            {
                isReadOnly = true;
                reasons.Add("Accounting period is closed");
            }

            // Check feature availability
            if (!string.IsNullOrEmpty(operation) && !_featureService.IsFeatureAvailable(operation))
            {
                isReadOnly = true;
                reasons.Add($"Feature '{operation}' is not available");
            }

            return new ReadOnlyModeStatus
            {
                IsReadOnly = isReadOnly,
                Reasons = reasons,
                Mode = failSafeMode,
                UserCanEdit = !isReadOnly,
                VisualIndicator = GetVisualIndicator(isReadOnly, reasons)
            };
        }

        public bool CanPerformOperation(string operation, string? entityType = null)
        {
            var status = GetReadOnlyStatus(entityType, operation);
            
            if (status.IsReadOnly)
            {
                _logger.LogWarning("[READONLY_BLOCK] [User:{User}] [Operation:{Operation}] [Entity:{Entity}] Blocked: {Reasons}", 
                    _currentUserService.Username, operation, entityType ?? "<none>", string.Join("; ", status.Reasons));
                return false;
            }

            return true;
        }

        public void EnforceReadOnlyUI(FrameworkElement element, string? entityType = null)
        {
            var status = GetReadOnlyStatus(entityType);
            
            if (status.IsReadOnly)
            {
                // Disable input controls
                DisableInputControls(element);
                
                // Add visual indication
                AddReadOnlyVisual(element, status);
                
                _logger.LogDebug("[READONLY_UI] [User:{User}] Applied read-only UI to element: {ElementType}", 
                    _currentUserService.Username, element.GetType().Name);
            }
        }

        public async Task<bool> AttemptWriteOperation(string operation, string? entityType = null, string? details = null)
        {
            var status = GetReadOnlyStatus(entityType, operation);
            
            if (status.IsReadOnly)
            {
                _logger.LogWarning("[READONLY_ATTEMPT] [User:{User}] [Operation:{Operation}] [Entity:{Entity}] Write operation blocked: {Reasons}", 
                    _currentUserService.Username, operation, entityType ?? "<none>", string.Join("; ", status.Reasons));

                // Show user-friendly message
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show(
                        status.VisualIndicator,
                        "Read-Only Mode",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                });

                return false;
            }

            return true;
        }

        public string GetReadOnlyMessage(string? entityType = null)
        {
            var status = GetReadOnlyStatus(entityType);
            return status.VisualIndicator;
        }

        private bool HasWritePermission(string? entityType)
        {
            // In a real implementation, this would check user roles and permissions
            // For now, assume all logged-in users have write permissions unless in read-only mode
            return _sessionService.IsUserLoggedIn;
        }

        private bool IsFinancialEntity(string? entityType)
        {
            if (string.IsNullOrEmpty(entityType))
                return false;

            var financialEntities = new[]
            {
                "Invoice", "SalesOrder", "JournalEntry", "Payment", 
                "Receipt", "Adjustment", "Period", "Account"
            };

            return financialEntities.Contains(entityType, StringComparer.OrdinalIgnoreCase);
        }

        private bool IsPeriodClosed()
        {
            // In a real implementation, this would check the current accounting period status
            // For now, assume period is open
            return false;
        }

        private void DisableInputControls(FrameworkElement element)
        {
            if (element is TextBox textBox)
            {
                textBox.IsReadOnly = true;
                textBox.Background = System.Windows.SystemColors.ControlBrush;
            }
            else if (element is ComboBox comboBox)
            {
                comboBox.IsEnabled = false;
                comboBox.Background = System.Windows.SystemColors.ControlBrush;
            }
            else if (element is CheckBox checkBox)
            {
                checkBox.IsEnabled = false;
            }
            else if (element is RadioButton radioButton)
            {
                radioButton.IsEnabled = false;
            }
            else if (element is Button button)
            {
                // Only disable action buttons, not navigation buttons
                if (IsActionButton(button))
                {
                    button.IsEnabled = false;
                }
            }
            else if (element is DataGrid dataGrid)
            {
                dataGrid.IsReadOnly = true;
                dataGrid.CanUserAddRows = false;
                dataGrid.CanUserDeleteRows = false;
            }

            // Recursively process child elements
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;
                if (child != null)
                {
                    DisableInputControls(child);
                }
            }
        }

        private bool IsActionButton(Button button)
        {
            var buttonName = button.Name?.ToLowerInvariant() ?? "";
            var buttonContent = button.Content?.ToString()?.ToLowerInvariant() ?? "";

            var actionKeywords = new[]
            {
                "save", "post", "delete", "add", "edit", "update", 
                "create", "submit", "approve", "adjust", "close"
            };

            return actionKeywords.Any(keyword => 
                buttonName.Contains(keyword) || buttonContent.Contains(keyword));
        }

        private void AddReadOnlyVisual(FrameworkElement element, ReadOnlyModeStatus status)
        {
            // Add a visual overlay or border to indicate read-only state
            if (element is Panel panel)
            {
                var overlay = new System.Windows.Shapes.Rectangle
                {
                    Fill = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Transparent),
                    Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Gray),
                    StrokeThickness = 1,
                    StrokeDashArray = new System.Windows.Media.DoubleCollection { 5, 5 },
                    IsHitTestVisible = false
                };

                panel.Children.Add(overlay);
            }
        }

        private string GetVisualIndicator(bool isReadOnly, List<string> reasons)
        {
            if (!isReadOnly)
                return string.Empty;

            var primaryReason = reasons.FirstOrDefault() ?? "System is in read-only mode";

            return primaryReason switch
            {
                "System is in ReadOnly mode" => "🔒 System is in read-only mode. Data modifications are disabled.",
                "System is in Emergency mode" => "🚨 System is in emergency mode. Only critical functions are available.",
                "User session is invalid or expired" => "⚠️ Your session has expired. Please log in again.",
                "User lacks write permissions" => "🔒 You don't have permission to modify this data.",
                "Accounting period is closed" => "🔒 This accounting period is closed. No modifications allowed.",
                _ => $"🔒 {primaryReason}"
            };
        }
    }

    public interface IReadOnlyModeEnforcementService
    {
        ReadOnlyModeStatus GetReadOnlyStatus(string? entityType = null, string? operation = null);
        bool CanPerformOperation(string operation, string? entityType = null);
        void EnforceReadOnlyUI(FrameworkElement element, string? entityType = null);
        Task<bool> AttemptWriteOperation(string operation, string? entityType = null, string? details = null);
        string GetReadOnlyMessage(string? entityType = null);
    }

    public class ReadOnlyModeStatus
    {
        public bool IsReadOnly { get; set; }
        public List<string> Reasons { get; set; } = new();
        public FailSafeMode Mode { get; set; }
        public bool UserCanEdit { get; set; }
        public string VisualIndicator { get; set; } = string.Empty;
    }
}
