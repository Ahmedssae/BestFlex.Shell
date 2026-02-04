using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using BestFlex.Domain.Entities;
using BestFlex.Shell.Services;

namespace BestFlex.Shell.ViewModels
{
    // PHASE 3: Validation Components
    public class NewSaleValidationError
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Field { get; set; } = string.Empty;
    }

    public class NewSaleValidationResult
    {
        public bool IsValid => !Errors.Any();
        public List<NewSaleValidationError> Errors { get; set; } = new();
    }

    // PHASE 2: Domain Models (Offline Brain)
    public class SalesOrderDraft
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string CustomerName { get; set; } = string.Empty;
        public DateTime OrderDate { get; set; } = DateTime.Today;
        public string Currency { get; set; } = "USD";
        public ObservableCollection<SalesOrderLineDraftViewModel> Lines { get; set; } = new();
        
        public decimal Subtotal => Lines.Sum(l => l.LineTotal);
        public decimal Tax => Subtotal * 0.10m; // 10% hardcoded
        public decimal GrandTotal => Subtotal + Tax;
    }

    public class SalesOrderLineDraftViewModel : INotifyPropertyChanged
    {
        private Guid _lineId = Guid.NewGuid();
        private string _description = string.Empty;
        private decimal _quantity = 1;
        private decimal _unitPrice = 0;

        public Guid LineId
        {
            get => _lineId;
            set => _lineId = value;
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged(nameof(Description));
                }
            }
        }

        public decimal Quantity
        {
            get => _quantity;
            set
            {
                if (_quantity != value)
                {
                    _quantity = value;
                    OnPropertyChanged(nameof(Quantity));
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (_unitPrice != value)
                {
                    _unitPrice = value;
                    OnPropertyChanged(nameof(UnitPrice));
                    OnPropertyChanged(nameof(LineTotal));
                }
            }
        }

        public decimal LineTotal => Quantity * UnitPrice;

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // PHASE 5: ViewModel with Posting & Integration
    public class NewSaleViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly INewSaleDraftSession _session;
        private readonly SalesOrderDraftService? _draftService;
        private readonly PostingService? _postingService;
        private readonly ILogger<NewSaleViewModel>? _logger;
        private bool _canSaveDraft;
        private bool _canPostOrder;
        private NewSaleValidationResult _validationResult = new();
        private readonly NotifyCollectionChangedEventHandler _linesCollectionChangedHandler;

        public NewSaleViewModel(
            INewSaleDraftSession session,
            SalesOrderDraftService? draftService = null, 
            PostingService? postingService = null,
            ILogger<NewSaleViewModel>? logger = null)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _draftService = draftService;
            _postingService = postingService;
            _logger = logger;
            
            // Store handler for disposal
            _linesCollectionChangedHandler = (_, _) => 
            {
                UpdateTotalsAndButtonState();
                ValidateOrder();
            };
            
            Lines.CollectionChanged += _linesCollectionChangedHandler;
        }

        // Properties for UI binding
        public string CustomerName
        {
            get => _session.Draft.CustomerName;
            set
            {
                if (_session.Draft.CustomerName != value)
                {
                    _session.Draft.CustomerName = value;
                    OnPropertyChanged(nameof(CustomerName));
                    ValidateOrder();
                }
            }
        }

        public DateTime OrderDate
        {
            get => _session.Draft.OrderDate;
            set
            {
                if (_session.Draft.OrderDate != value)
                {
                    _session.Draft.OrderDate = value;
                    OnPropertyChanged(nameof(OrderDate));
                }
            }
        }

        public string Currency
        {
            get => _session.Draft.Currency;
            set
            {
                if (_session.Draft.Currency != value)
                {
                    _session.Draft.Currency = value;
                    OnPropertyChanged(nameof(Currency));
                    ValidateOrder();
                }
            }
        }

        public ObservableCollection<SalesOrderLineDraftViewModel> Lines => _session.Draft.Lines;

        public decimal Subtotal => _session.Draft.Subtotal;
        public decimal Tax => _session.Draft.Tax;
        public decimal GrandTotal => _session.Draft.GrandTotal;

        public bool CanSaveDraft
        {
            get => _canSaveDraft && !_session.IsPosted;
            private set
            {
                if (_canSaveDraft != value)
                {
                    _canSaveDraft = value;
                    OnPropertyChanged(nameof(CanSaveDraft));
                }
            }
        }

        public bool CanPostOrder
        {
            get => _canPostOrder && !_session.IsPosted && _session.PersistedOrderId.HasValue;
            private set
            {
                if (_canPostOrder != value)
                {
                    _canPostOrder = value;
                    OnPropertyChanged(nameof(CanPostOrder));
                }
            }
        }

        public bool IsPosted => _session.IsPosted;

        // PHASE 3: Validation Properties
        public NewSaleValidationResult ValidationResult
        {
            get => _validationResult;
            private set
            {
                if (_validationResult != value)
                {
                    _validationResult = value;
                    OnPropertyChanged(nameof(ValidationResult));
                    OnPropertyChanged(nameof(HasErrors));
                    OnPropertyChanged(nameof(ErrorMessage));
                }
            }
        }

        public bool HasErrors => !ValidationResult.IsValid;
        public string ErrorMessage => HasErrors ? string.Join("\n", ValidationResult.Errors.Select(e => e.Message)) : string.Empty;

        // PHASE 5: Posting Methods
        public async Task<PostingResult> PostOrderAsync()
        {
            if (!ValidationResult.IsValid || !_session.PersistedOrderId.HasValue || _postingService == null)
            {
                return new PostingResult 
                { 
                    Success = false, 
                    ErrorMessage = "Order must be saved and valid before posting" 
                };
            }

            try
            {
                var result = await _postingService.PostOrderAsync(_session.PersistedOrderId.Value);
                
                if (result.Success)
                {
                    _session.IsPosted = true;
                    _logger?.LogInformation("Order {OrderId} posted successfully with invoice {InvoiceId}", 
                        _session.PersistedOrderId, result.InvoiceId);
                }
                else
                {
                    _logger?.LogWarning("Failed to post order {OrderId}: {Error}", 
                        _session.PersistedOrderId, result.ErrorMessage);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error posting order {OrderId}", _session.PersistedOrderId);
                return new PostingResult 
                { 
                    Success = false, 
                    ErrorMessage = "An error occurred while posting the order" 
                };
            }
        }
        public async Task<bool> SaveDraftAsync()
        {
            if (!ValidationResult.IsValid || _draftService == null)
                return false;

            try
            {
                var draftLines = Lines.Select(l => new SalesOrderLineDraft
                {
                    Description = l.Description,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice
                }).ToList();

                SalesOrder? savedOrder;

                if (_session.PersistedOrderId.HasValue)
                {
                    // Update existing draft
                    savedOrder = await _draftService.UpdateDraftAsync(_session.PersistedOrderId.Value, CustomerName, OrderDate, draftLines);
                }
                else
                {
                    // Create new draft
                    savedOrder = await _draftService.CreateDraftAsync(CustomerName, OrderDate, draftLines);
                    if (savedOrder != null)
                    {
                        _session.PersistedOrderId = savedOrder.Id;
                    }
                }

                _logger?.LogInformation("Draft saved successfully: {OrderId}", _session.PersistedOrderId);
                return savedOrder != null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to save draft");
                return false;
            }
        }

        public async Task<bool> LoadDraftAsync(int id)
        {
            if (_draftService == null)
                return false;

            try
            {
                var draft = await _draftService.LoadDraftAsync(id);
                if (draft == null)
                    return false;

                // Update UI with loaded data
                CustomerName = $"Customer {draft.CustomerId}"; // Simplified - would need customer lookup
                OrderDate = draft.OrderDate;
                Currency = "USD"; // Default

                // Clear existing lines and load from draft
                Lines.Clear();
                foreach (var line in draft.Lines)
                {
                    Lines.Add(new SalesOrderLineDraftViewModel
                    {
                        Description = $"Product {line.ProductId}", // Simplified - would need product lookup
                        Quantity = line.Quantity,
                        UnitPrice = line.UnitPrice
                    });
                }

                _session.PersistedOrderId = draft.Id;
                _logger?.LogInformation("Draft loaded successfully: {OrderId}", id);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to load draft {Id}", id);
                return false;
            }
        }

        // PHASE 3: Validation Methods
        public void ValidateOrder()
        {
            var errors = new List<NewSaleValidationError>();

            // Header Validation
            if (string.IsNullOrWhiteSpace(CustomerName))
            {
                errors.Add(new NewSaleValidationError
                {
                    Code = "CUSTOMER_REQUIRED",
                    Message = "Customer is required",
                    Field = "CustomerName"
                });
            }

            if (string.IsNullOrWhiteSpace(Currency))
            {
                errors.Add(new NewSaleValidationError
                {
                    Code = "CURRENCY_REQUIRED",
                    Message = "Currency is required",
                    Field = "Currency"
                });
            }

            // Lines Validation
            if (!Lines.Any())
            {
                errors.Add(new NewSaleValidationError
                {
                    Code = "AT_LEAST_ONE_LINE",
                    Message = "At least one line is required",
                    Field = "Lines"
                });
            }
            else
            {
                for (int i = 0; i < Lines.Count; i++)
                {
                    var line = Lines[i];
                    var linePrefix = $"Line {i + 1}";

                    if (string.IsNullOrWhiteSpace(line.Description))
                    {
                        errors.Add(new NewSaleValidationError
                        {
                            Code = "DESCRIPTION_REQUIRED",
                            Message = $"{linePrefix}: Description is required",
                            Field = $"Lines[{i}].Description"
                        });
                    }

                    if (line.Quantity <= 0)
                    {
                        errors.Add(new NewSaleValidationError
                        {
                            Code = "QUANTITY_GT_ZERO",
                            Message = $"{linePrefix}: Quantity must be greater than 0",
                            Field = $"Lines[{i}].Quantity"
                        });
                    }

                    if (line.UnitPrice < 0)
                    {
                        errors.Add(new NewSaleValidationError
                        {
                            Code = "UNIT_PRICE_GE_ZERO",
                            Message = $"{linePrefix}: Unit price must be 0 or greater",
                            Field = $"Lines[{i}].UnitPrice"
                        });
                    }
                }
            }

            ValidationResult = new NewSaleValidationResult { Errors = errors };
        }

        // PHASE 2: Local Interaction Methods (unchanged)
        public void AddLine()
        {
            var newLine = new SalesOrderLineDraftViewModel
            {
                Description = $"Item {Lines.Count + 1}",
                Quantity = 1,
                UnitPrice = 0
            };
            Lines.Add(newLine);
        }

        public void RemoveLine(SalesOrderLineDraftViewModel line)
        {
            if (line != null && Lines.Contains(line))
            {
                Lines.Remove(line);
            }
        }

        private void UpdateTotalsAndButtonState()
        {
            // Auto-calc totals (handled by domain model)
            OnPropertyChanged(nameof(Subtotal));
            OnPropertyChanged(nameof(Tax));
            OnPropertyChanged(nameof(GrandTotal));

            // Button logic: Save Draft enabled if order is valid
            CanSaveDraft = ValidationResult.IsValid;
        }

        public void Dispose()
        {
            Lines.CollectionChanged -= _linesCollectionChangedHandler;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
