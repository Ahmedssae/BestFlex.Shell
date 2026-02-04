using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using BestFlex.Application.UseCases.SalesOrders;
using MediatR;
using Microsoft.Extensions.Logging;
using BestFlex.Shell.Models;
using BestFlex.Shell.Repositories;
using BestFlex.Shell.Services;

namespace BestFlex.Shell.ViewModels
{
    // STANDARD SALES ORDER ENTRY OBJECT MODEL
    public class NewSaleLocalViewModel : INotifyPropertyChanged
    {
        private readonly BestFlex.Shell.Repositories.ISalesOrderRepository? _repository;
        private readonly IMediator? _mediator;
        private readonly ILogger<NewSaleLocalViewModel>? _logger;
        private readonly IInventoryReadService? _inventoryService;
        private readonly IPostingService? _postingService;
        private int? _persistedOrderId;
        private bool _isPosted;
        private bool _isPosting;

        // LOCAL DRAFT MODEL
        public SalesOrderDraft Draft { get; private set; }
        public ObservableCollection<SalesOrderLineDraftViewModel> Lines { get; private set; }

        // Constructor
        public NewSaleLocalViewModel(
            IMediator? mediator = null,
            ILogger<NewSaleLocalViewModel>? logger = null,
            BestFlex.Shell.Repositories.ISalesOrderRepository? repository = null, 
            IInventoryReadService? inventoryService = null,
            IPostingService? postingService = null)
        {
            _mediator = mediator;
            _logger = logger;
            _repository = repository;
            _inventoryService = inventoryService;
            _postingService = postingService;
            Draft = new SalesOrderDraft();
            
            // Initialize lines collection
            Lines = new ObservableCollection<SalesOrderLineDraftViewModel>();
            
            // Initialize commands
            AddLineCommand = new LocalRelayCommand(AddLine);
            RemoveLineCommand = new LocalRelayCommand(RemoveLine);
            SaveDraftCommand = new LocalRelayCommand(SaveDraft, () => CanSaveDraft);
            LoadDraftCommand = new LocalRelayCommand(LoadDraft);
            PostInvoiceCommand = new LocalRelayCommand(PostInvoice, () => CanPostInvoice);
            PostOrderCommand = new LocalRelayCommand(PostOrder, () => CanPostOrder);

            // Subscribe to line changes for totals updates
            Lines.CollectionChanged += (_, _) => 
            {
                UpdateTotals();
                UpdateValidation();
                UpdateCommands();
            };
            
            // Subscribe to property changes for button state
            // Note: SalesOrderDraft from Models doesn't have PropertyChanged event
            // This will be handled by individual property setters in the UI
            
            // Initial validation
            UpdateValidation();
        }

        // Validation state
        public string ValidationMessage { get; private set; } = string.Empty;
        public bool HasValidationErrors => !string.IsNullOrEmpty(ValidationMessage);

        // Success message state
        public string SuccessMessage { get; private set; } = string.Empty;
        public bool HasSuccessMessage => !string.IsNullOrEmpty(SuccessMessage);

        // Validation state model (Phase 3)
        public bool IsCustomerValid => !string.IsNullOrWhiteSpace(Draft.CustomerName);
        public bool HasLines => Lines.Any();
        public bool AreLinesValid => Lines.All(l => !string.IsNullOrWhiteSpace(l.Description) && l.Quantity > 0 && l.UnitPrice >= 0);

        // Inline field feedback (Phase 3)
        public string CustomerFieldError => !IsCustomerValid ? "Customer is required" : string.Empty;
        public string LinesFieldError => !HasLines ? "At least one line item is required" : string.Empty;
        public string SaveDraftExplanation => CanSaveDraft ? string.Empty : "Complete required fields to save draft";

        // Commands
        public ICommand AddLineCommand { get; }
        public ICommand RemoveLineCommand { get; }
        public ICommand SaveDraftCommand { get; }
        public ICommand LoadDraftCommand { get; }
        public ICommand PostInvoiceCommand { get; }
        public ICommand PostOrderCommand { get; }

        // Posting state
        public bool IsPosted
        {
            get => _isPosted;
            private set
            {
                if (_isPosted != value)
                {
                    _isPosted = value;
                    OnPropertyChanged(nameof(IsPosted));
                    OnPropertyChanged(nameof(CanSaveDraft));
                    OnPropertyChanged(nameof(CanPostInvoice));
                }
            }
        }

        public bool IsPosting
        {
            get => _isPosting;
            private set
            {
                if (_isPosting != value)
                {
                    _isPosting = value;
                    OnPropertyChanged(nameof(IsPosting));
                    OnPropertyChanged(nameof(CanSaveDraft));
                    OnPropertyChanged(nameof(CanPostOrder));
                }
            }
        }

        public bool CanPostInvoice => !_isPosted && _persistedOrderId.HasValue && IsCustomerValid && HasLines && AreLinesValid;
        public bool CanSaveDraft => IsCustomerValid && HasLines && AreLinesValid && !_isPosted;
        public bool CanPostOrder => _persistedOrderId.HasValue && IsCustomerValid && HasLines && AreLinesValid && !_isPosted;


        private void UpdateValidation()
        {
            var errors = new List<string>();

            // Check customer name
            if (string.IsNullOrWhiteSpace(Draft.CustomerName))
            {
                errors.Add("Customer name is required");
            }

            // Check if there are any lines
            if (!Lines.Any())
            {
                errors.Add("At least one line item is required");
            }
            else
            {
                // Check each line for validation errors
                for (int i = 0; i < Lines.Count; i++)
                {
                    var line = Lines[i];
                    var linePrefix = $"Line {i + 1}";

                    if (string.IsNullOrWhiteSpace(line.Description))
                    {
                        errors.Add($"{linePrefix}: Item description is required");
                    }

                    if (line.Quantity <= 0)
                    {
                        errors.Add($"{linePrefix}: Quantity must be greater than 0");
                    }

                    if (line.UnitPrice < 0)
                    {
                        errors.Add($"{linePrefix}: Unit price cannot be negative");
                    }
                }
            }

            // Update validation message
            ValidationMessage = errors.Any() ? string.Join("\n", errors) : string.Empty;
            
            // Notify property changes (Phase 3)
            OnPropertyChanged(nameof(ValidationMessage));
            OnPropertyChanged(nameof(HasValidationErrors));
            OnPropertyChanged(nameof(IsCustomerValid));
            OnPropertyChanged(nameof(HasLines));
            OnPropertyChanged(nameof(AreLinesValid));
            OnPropertyChanged(nameof(CanSaveDraft));
            OnPropertyChanged(nameof(CustomerFieldError));
            OnPropertyChanged(nameof(LinesFieldError));
            OnPropertyChanged(nameof(SaveDraftExplanation));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void UpdateTotals()
        {
            // Update draft totals based on Lines collection
            OnPropertyChanged(nameof(Draft.Subtotal));
            OnPropertyChanged(nameof(Draft.GrandTotal));
        }

        private void UpdateCommands()
        {
            ((LocalRelayCommand)SaveDraftCommand).RaiseCanExecuteChanged();
        }

        private async void AddLine()
        {
            var newLine = new SalesOrderLineDraftViewModel
            {
                Description = string.Empty,
                Quantity = 1,
                UnitPrice = 0
            };
            
            Lines.Add(newLine);
            
            // Subscribe to line property changes
            newLine.PropertyChanged += (_, _) =>
            {
                UpdateTotals();
                UpdateValidation();
                UpdateCommands();
            };
            
            // Load inventory for the new line
            await LoadInventoryForLine(newLine);
            
            UpdateTotals();
            UpdateValidation();
            UpdateCommands();
        }

        private async Task LoadInventoryForLine(SalesOrderLineDraftViewModel line)
        {
            if (_inventoryService == null)
            {
                // Note: SalesOrderLineDraft doesn't have InventoryInfo property
                return;
            }

            try
            {
                // For demo purposes, we'll use the line index as a mock product ID
                var productId = Lines.IndexOf(line) + 1;
                var inventoryInfo = await _inventoryService.GetInventoryInfoAsync(productId);
                // Note: SalesOrderLineDraftViewModel doesn't have InventoryInfo property
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load inventory for line");
                // Note: SalesOrderLineDraft doesn't have InventoryInfo property
            }
        }

        private async Task LoadInventoryForAllLines()
        {
            if (_inventoryService == null) return;

            try
            {
                var productIds = Lines.Select((line, index) => index + 1).ToList();
                var inventoryInfos = await _inventoryService.GetInventoryInfoAsync(productIds);
                
                // Note: SalesOrderLineDraftViewModel doesn't have InventoryInfo property
                _logger?.LogInformation("Loaded inventory for {Count} lines", inventoryInfos.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load inventory for all lines");
            }
        }

        private void RemoveLine(object? parameter)
        {
            if (parameter is SalesOrderLineDraftViewModel line)
            {
                Lines.Remove(line);
                UpdateTotals();
                UpdateValidation();
                UpdateCommands();
            }
        }

        private async void SaveDraft()
        {
            // Phase 3: Local UI validation ONLY - no service calls, no exceptions
            UpdateValidation();
            
            // Phase 3: Do nothing silently when invalid - validation state handles UI feedback
            if (!CanSaveDraft)
            {
                return; // Silent return - UI shows validation state
            }

            // Phase 4: Try Application layer first, fall back to legacy repository
            if (_mediator != null)
            {
                await SaveDraftWithApplicationLayer();
            }
            else if (_repository != null)
            {
                await SaveDraftWithLegacyRepository();
            }
            else
            {
                _logger?.LogWarning("No persistence mechanism available - draft not saved");
                return;
            }
        }

        private async Task SaveDraftWithApplicationLayer()
        {
            try
            {
                var command = new SaveSalesOrderDraftCommand
                {
                    DraftId = _persistedOrderId,
                    CustomerName = Draft.CustomerName,
                    OrderDate = Draft.OrderDate,
                    Lines = Draft.Lines.Select(l => new SaveSalesOrderDraftCommand.SalesOrderLineDto
                    {
                        Description = l.Description,
                        Quantity = l.Quantity,
                        UnitPrice = l.UnitPrice,
                        Discount = 0
                    }).ToList()
                };

                var result = await _mediator!.Send(command);

                if (result.Success)
                {
                    _persistedOrderId = result.DraftId;
                    SuccessMessage = $"Draft saved at {result.SavedAt:HH:mm}";
                    OnPropertyChanged(nameof(SuccessMessage));
                    OnPropertyChanged(nameof(HasSuccessMessage));
                    _logger?.LogInformation("Draft saved successfully: {OrderNumber}", result.OrderNumber);
                }
                else
                {
                    // Phase 3: Show validation errors inline, no popups
                    ValidationMessage = string.Join("\n", result.ValidationErrors);
                    OnPropertyChanged(nameof(ValidationMessage));
                    OnPropertyChanged(nameof(HasValidationErrors));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving draft with Application layer");
                // Phase 3: Silent failure - no exceptions to UI
            }
        }

        private async Task SaveDraftWithLegacyRepository()
        {
            try
            {
                // Convert local draft to domain entity
                var domainOrder = ConvertToDomainOrder();

                SalesOrder? savedOrder;
                
                if (_persistedOrderId.HasValue)
                {
                    // Update existing draft
                    domainOrder.Id = _persistedOrderId.Value;
                    savedOrder = await _repository!.UpdateDraftAsync(domainOrder);
                }
                else
                {
                    // Create new draft
                    savedOrder = await _repository!.SaveDraftAsync(domainOrder);
                    if (savedOrder != null)
                    {
                        _persistedOrderId = savedOrder.Id;
                        UpdateCommands(); // Update Post Invoice button state
                    }
                }

                if (savedOrder != null)
                {
                    SuccessMessage = $"Draft saved successfully: {savedOrder.OrderNumber}";
                    OnPropertyChanged(nameof(SuccessMessage));
                    OnPropertyChanged(nameof(HasSuccessMessage));
                    _logger?.LogInformation("Draft saved successfully: {OrderNumber}", savedOrder.OrderNumber);
                }
                else
                {
                    _logger?.LogError("Failed to save draft");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving draft with legacy repository");
                // Phase 3: Silent failure - no exceptions to UI
            }
        }

        private async void PostOrder()
        {
            // Phase 5: Validate before posting
            UpdateValidation();
            
            if (!CanPostOrder || !_persistedOrderId.HasValue)
            {
                // Phase 3: Silent return - validation state handles UI feedback
                return;
            }

            // Phase 5: Try Application layer first
            if (_mediator != null)
            {
                await PostOrderWithApplicationLayer();
            }
            else
            {
                _logger?.LogWarning("Mediator not available - posting not possible");
                // Phase 3: Show inline error
                ValidationMessage = "Posting service not available";
                OnPropertyChanged(nameof(ValidationMessage));
                OnPropertyChanged(nameof(HasValidationErrors));
            }
        }

        private async Task PostOrderWithApplicationLayer()
        {
            try
            {
                // Phase 5: Disable inputs during posting
                IsPosting = true;
                OnPropertyChanged(nameof(IsPosting));
                OnPropertyChanged(nameof(CanSaveDraft));
                OnPropertyChanged(nameof(CanPostOrder));

                var command = new PostSalesOrderCommand
                {
                    DraftOrderId = _persistedOrderId!.Value
                };

                var result = await _mediator!.Send(command);

                if (result.Success)
                {
                    // Phase 5: Mark as posted - order becomes immutable
                    _isPosted = true;
                    SuccessMessage = $"Order {result.OrderNumber} posted successfully at {result.PostingDate:HH:mm}";
                    
                    // Phase 5: Show posting details
                    if (result.InventoryMovements.Any())
                    {
                        SuccessMessage += $" - {result.InventoryMovements.Count} inventory movements processed";
                    }
                    
                    OnPropertyChanged(nameof(SuccessMessage));
                    OnPropertyChanged(nameof(HasSuccessMessage));
                    OnPropertyChanged(nameof(IsPosted));
                    OnPropertyChanged(nameof(CanSaveDraft));
                    OnPropertyChanged(nameof(CanPostOrder));
                    
                    _logger?.LogInformation("Order posted successfully: {OrderNumber}", result.OrderNumber);
                }
                else
                {
                    // Phase 3: Show errors inline
                    var allErrors = result.ValidationErrors.Concat(result.BusinessErrors);
                    ValidationMessage = string.Join("\n", allErrors);
                    OnPropertyChanged(nameof(ValidationMessage));
                    OnPropertyChanged(nameof(HasValidationErrors));
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error posting order");
                // Phase 3: Silent failure with inline message
                ValidationMessage = "Posting failed due to a system error";
                OnPropertyChanged(nameof(ValidationMessage));
                OnPropertyChanged(nameof(HasValidationErrors));
            }
            finally
            {
                // Phase 5: Re-enable inputs
                IsPosting = false;
                OnPropertyChanged(nameof(IsPosting));
                OnPropertyChanged(nameof(CanSaveDraft));
                OnPropertyChanged(nameof(CanPostOrder));
            }
        }

        private async void LoadDraft(object? parameter)
        {
            if (_repository == null || parameter == null)
            {
                _logger?.LogWarning("Repository not available or no parameter - draft not loaded");
                return;
            }

            try
            {
                var draftId = (int)parameter;
                var domainOrder = await _repository.LoadDraftAsync(draftId);
                
                if (domainOrder != null)
                {
                    // Convert domain entity back to local draft
                    ConvertFromDomainOrder(domainOrder);
                    _persistedOrderId = domainOrder.Id;
                    
                    _logger?.LogInformation("Draft loaded successfully: {OrderNumber}", domainOrder.OrderNumber);
                }
                else
                {
                    _logger?.LogError("Failed to load draft {Id}", draftId);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading draft {Id}", parameter);
            }
        }

        private SalesOrder ConvertToDomainOrder()
        {
            return new SalesOrder
            {
                CustomerName = Draft.CustomerName,
                OrderDate = Draft.OrderDate,
                Currency = Draft.Currency,
                Subtotal = Draft.Subtotal,
                Tax = Draft.Tax,
                GrandTotal = Draft.GrandTotal,
                Lines = Draft.Lines.Select(l => new SalesOrderLine
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    LineTotal = l.LineTotal
                }).ToList()
            };
        }

        private void ConvertFromDomainOrder(SalesOrder domainOrder)
        {
            Draft.CustomerName = domainOrder.CustomerName;
            Draft.Currency = domainOrder.Currency;
            
            // Clear existing lines and load from domain order
            Draft.Lines.Clear();
            
            UpdateValidation();
        }

        private async void PostInvoice()
        {
            if (_postingService == null || !_persistedOrderId.HasValue)
            {
                _logger?.LogWarning("Posting service not available or no persisted order ID");
                return;
            }

            try
            {
                var result = await _postingService.PostOrderAsync(_persistedOrderId.Value);
                
                if (result.Success)
                {
                    IsPosted = true;
                    _logger?.LogInformation("Order posted successfully: {InvoiceNumber}", result.InvoiceNumber);
                }
                else
                {
                    _logger?.LogWarning("Failed to post order: {Error}", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error posting order");
            }
        }
    }

    // Simple RelayCommand implementation - no external dependencies
    public class LocalRelayCommand : ICommand
    {
        private readonly Action? _execute;
        private readonly Func<bool>? _canExecute;
        private readonly Action<object?>? _executeWithParameter;

        public LocalRelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public LocalRelayCommand(Action<object?> executeWithParameter, Func<bool>? canExecute = null)
        {
            _executeWithParameter = executeWithParameter ?? throw new ArgumentNullException(nameof(executeWithParameter));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            if (_executeWithParameter != null)
            {
                _executeWithParameter(parameter);
            }
            else
            {
                _execute?.Invoke();
            }
        }

        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
