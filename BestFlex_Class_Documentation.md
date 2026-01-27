# BestFlex - Complete Class Documentation

## Core Domain Classes

### BestFlex.Domain\Entities.Users.cs
```csharp
public class Users
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string RolesCsv { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAtUtc { get; set; }
}
```
**Purpose**: User entity for authentication and authorization
- **Id**: Unique identifier
- **Username**: Login username
- **DisplayName**: User-friendly name
- **PasswordHash**: BCrypt hashed password
- **RolesCsv**: Comma-separated role list
- **CreatedAtUtc**: Account creation timestamp
- **IsActive**: Account status
- **LastLoginAtUtc**: Last login tracking

### BestFlex.Domain.Entities.ProductEntity.cs
```csharp
public class ProductEntity
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public string Unit { get; set; } = string.Empty;
    public int CurrentStock { get; set; }
    public int MinimumLevel { get; set; }
    public ProductCategory Category { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
```
**Purpose**: Product catalog entity
- **ProductCode**: Unique product identifier
- **ProductName**: Human-readable name
- **Description**: Product details
- **UnitPrice**: Selling price
- **Unit**: Measurement unit
- **CurrentStock**: Current inventory level
- **MinimumLevel**: Reorder point
- **Category**: Product classification
- **IsActive**: Product status

### BestFlex.Domain.Entities.InvoiceEntity.cs
```csharp
public class InvoiceEntity
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public Guid CustomerId { get; set; }
    public CustomerEntity Customer { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public InvoiceStatus Status { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    
    public ICollection<InvoiceLineEntity> InvoiceLines { get; set; } = new List<InvoiceLineEntity>();
}
```
**Purpose**: Sales invoice entity
- **InvoiceNumber**: Human-readable invoice number
- **InvoiceDate**: Invoice creation date
- **CustomerId**: Customer reference
- **TotalAmount**: Invoice total
- **TaxAmount**: Tax calculation
- **DiscountAmount**: Discount applied
- **PaymentStatus**: Payment tracking
- **Status**: Invoice lifecycle
- **InvoiceLines**: Line items collection

### BestFlex.Domain.Entities.JournalEntryEntity.cs
```csharp
public class JournalEntryEntity
{
    public Guid Id { get; set; }
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    
    public ICollection<JournalLineEntity> JournalLines { get; set; } = new List<JournalLineEntity>();
}
```
**Purpose**: Accounting journal entry for double-entry bookkeeping
- **EntryDate**: Transaction date
- **Description**: Transaction description
- **Reference**: Reference number/document
- **JournalLines**: Debit/credit lines

## Application Services

### BestFlex.Application.Services.SalesService.cs
```csharp
public class SalesService : ISalesService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IProductReadService _productService;
    private readonly ICustomerReadService _customerService;
    private readonly IStockValidationService _stockValidation;
    
    public async Task<SaleResult> ProcessSaleAsync(CreateSaleRequest request)
    {
        // Validate stock availability
        foreach (var item in request.Items)
        {
            var stockCheck = await _stockValidation.ValidateStockAsync(item.ProductId, item.Quantity);
            if (!stockCheck.IsAvailable)
                return SaleResult.Failure($"Insufficient stock for {stockCheck.ProductName}");
        }
        
        // Create invoice
        var invoice = new InvoiceEntity
        {
            Id = Guid.NewGuid(),
            InvoiceNumber = await GenerateInvoiceNumberAsync(),
            InvoiceDate = DateTime.UtcNow,
            CustomerId = request.CustomerId,
            TotalAmount = request.Items.Sum(i => i.Quantity * i.UnitPrice),
            Status = InvoiceStatus.Draft,
            PaymentStatus = PaymentStatus.Pending
        };
        
        // Add invoice lines
        foreach (var item in request.Items)
        {
            invoice.InvoiceLines.Add(new InvoiceLineEntity
            {
                Id = Guid.NewGuid(),
                ProductId = item.ProductId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Total = item.Quantity * item.UnitPrice
            });
            
            // Update stock
            await _stockValidation.ReduceStockAsync(item.ProductId, item.Quantity);
        }
        
        // Create journal entry
        var journalEntry = new JournalEntryEntity
        {
            Id = Guid.NewGuid(),
            EntryDate = DateTime.UtcNow,
            Description = $"Sales Invoice {invoice.InvoiceNumber}",
            Reference = invoice.InvoiceNumber
        };
        
        // Add debit (accounts receivable)
        journalEntry.JournalLines.Add(new JournalLineEntity
        {
            Id = Guid.NewGuid(),
            AccountCode = "1200", // Accounts Receivable
            Debit = invoice.TotalAmount,
            Credit = 0
        });
        
        // Add credit (sales revenue)
        journalEntry.JournalLines.Add(new JournalLineEntity
        {
            Id = Guid.NewGuid(),
            AccountCode = "4000", // Sales Revenue
            Debit = 0,
            Credit = invoice.TotalAmount
        });
        
        await _unitOfWork.SaveChangesAsync();
        return SaleResult.Success(invoice.Id);
    }
}
```
**Purpose**: Sales transaction processing service
- **Stock Validation**: Checks availability before processing
- **Invoice Creation**: Generates invoice with line items
- **Stock Updates**: Reduces inventory levels
- **Accounting Integration**: Creates journal entries
- **Transaction Management**: Ensures data consistency

### BestFlex.Application.Services.StockValidationService.cs
```csharp
public class StockValidationService : IStockValidationService
{
    private readonly IUnitOfWork _unitOfWork;
    
    public async Task<StockValidationResult> ValidateStockAsync(Guid productId, int requestedQuantity)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product == null)
            return StockValidationResult.Failure("Product not found");
            
        if (product.CurrentStock < requestedQuantity)
            return StockValidationResult.Failure(
                $"Insufficient stock. Available: {product.CurrentStock}, Requested: {requestedQuantity}",
                product.ProductName,
                product.CurrentStock
            );
            
        return StockValidationResult.Success(product.ProductName, product.CurrentStock);
    }
    
    public async Task ReduceStockAsync(Guid productId, int quantity)
    {
        var product = await _unitOfWork.Products.GetByIdAsync(productId);
        if (product != null)
        {
            product.CurrentStock -= quantity;
            product.UpdatedAtUtc = DateTime.UtcNow;
            
            // Create stock transaction record
            var transaction = new StockTransactionEntity
            {
                Id = Guid.NewGuid(),
                ProductId = productId,
                Quantity = -quantity, // Negative for reduction
                TransactionType = StockTransactionType.Sale,
                TransactionDate = DateTime.UtcNow,
                Reference = "Sale"
            };
            
            _unitOfWork.StockTransactions.Add(transaction);
        }
    }
}
```
**Purpose**: Inventory stock validation and management
- **Stock Checking**: Validates availability
- **Stock Reduction**: Updates inventory levels
- **Transaction Recording**: Tracks stock movements
- **Error Handling**: Provides detailed failure information

## Infrastructure Services

### BestFlex.Infrastructure.Services.AuditService.cs
```csharp
public class AuditService : IAuditService
{
    private readonly BestFlexDbContext _db;
    private readonly ICurrentUserService _currentUser;
    
    public async Task LogNavigationAsync(string destination)
    {
        var entry = new AuditEntryEntity
        {
            Id = Guid.NewGuid(),
            Action = "NAVIGATION",
            EntityName = destination,
            EntityId = string.Empty,
            UserId = _currentUser.IsSignedIn ? _currentUser.UserId.ToString() : string.Empty,
            TimestampUtc = DateTime.UtcNow,
            Details = string.Empty
        };
        
        _db.Set<AuditEntryEntity>().Add(entry);
        await _db.SaveChangesAsync();
    }
    
    public async Task LogActionAsync(string action, string entityName, string entityId, string details)
    {
        var entry = new AuditEntryEntity
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            UserId = _currentUser.IsSignedIn ? _currentUser.UserId.ToString() : string.Empty,
            TimestampUtc = DateTime.UtcNow,
            Details = details
        };
        
        _db.Set<AuditEntryEntity>().Add(entry);
        await _db.SaveChangesAsync();
    }
}
```
**Purpose**: Audit logging service for tracking user actions
- **Navigation Logging**: Tracks page navigation
- **Action Logging**: Records business operations
- **User Context**: Associates actions with users
- **Timestamping**: Precise time tracking

### BestFlex.Infrastructure.Diagnostics.DatabaseIntegrityValidator.cs
```csharp
public class DatabaseIntegrityValidator : IDataIntegrityValidator
{
    private readonly BestFlexDbContext _db;
    
    public async Task<DataIntegrityResult> ValidateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check database connectivity
            if (!await _db.Database.CanConnectAsync(cancellationToken))
                return new DataIntegrityResult(false, "Cannot connect to database");
            
            // Validate accounting invariants (Sum(Debit) == Sum(Credit))
            var mismatch = await _db.JournalEntries
                .AsNoTracking()
                .Where(je => Math.Abs(
                    _db.JournalLines.Where(jl => jl.JournalEntryId == je.Id).Sum(jl => (double)jl.Debit) -
                    _db.JournalLines.Where(jl => jl.JournalEntryId == je.Id).Sum(jl => (double)jl.Credit)) > 0.0001)
                .Select(je => je.Id)
                .FirstOrDefaultAsync(cancellationToken);
                
            if (mismatch != Guid.Empty)
                return new DataIntegrityResult(false, $"Journal totals mismatch for entry: {mismatch}");
            
            // Check for orphaned records
            var orphanedLines = await _db.JournalLines
                .Where(jl => !_db.JournalEntries.Any(je => je.Id == jl.JournalEntryId))
                .CountAsync(cancellationToken);
                
            if (orphanedLines > 0)
                return new DataIntegrityResult(false, $"Found {orphanedLines} orphaned journal lines");
            
            // Validate invoice totals
            var invoiceTotalMismatches = await _db.Invoices
                .Where(i => i.InvoiceLines.Sum(il => il.Total) != i.TotalAmount)
                .CountAsync(cancellationToken);
                
            if (invoiceTotalMismatches > 0)
                return new DataIntegrityResult(false, $"Found {invoiceTotalMismatches} invoices with total mismatches");
            
            return new DataIntegrityResult(true, null);
        }
        catch (Exception ex)
        {
            return new DataIntegrityResult(false, $"Validation failed: {ex.Message}");
        }
    }
}
```
**Purpose**: Database integrity validation service
- **Connectivity Check**: Ensures database is accessible
- **Accounting Validation**: Validates double-entry bookkeeping rules
- **Orphan Detection**: Finds orphaned records
- **Invoice Validation**: Checks invoice calculations
- **Error Reporting**: Detailed failure information

## Shell ViewModels

### BestFlex.Shell.ViewModels.LoginViewModel.cs
```csharp
public class LoginViewModel : INotifyPropertyChanged
{
    private readonly IUserRepository _userRepository;
    private readonly IForensicLogger _forensicLogger;
    private readonly ILogger<LoginViewModel> _logger;
    
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool IsBusy { get; set; }
    public bool LoginSucceeded { get; private set; }
    
    public ICommand LoginCommand { get; }
    
    public LoginViewModel(IUserRepository userRepository, IForensicLogger forensicLogger, ILogger<LoginViewModel> logger)
    {
        _userRepository = userRepository;
        _forensicLogger = forensicLogger;
        _logger = logger;
        LoginCommand = new RelayCommand(async () => await ExecuteLogin(), CanLogin);
    }
    
    private async Task ExecuteLogin()
    {
        try
        {
            _logger.LogInformation("Login clicked");
            IsBusy = true;
            ErrorMessage = string.Empty;
            
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Enter username and password.";
                return;
            }
            
            var user = await _userRepository.FindByUsernameAsync(Username);
            if (user == null)
            {
                _logger.LogWarning("Login failed: user not found for Username='{Username}'", Username);
                ErrorMessage = "User not found";
                
                await _forensicLogger.LogAsync(new ForensicEvent(
                    ForensicEventType.LoginFailure,
                    DateTime.UtcNow,
                    Environment.MachineName,
                    Username,
                    "User not found",
                    null,
                    null
                ));
                return;
            }
            
            if (!BCrypt.Net.BCrypt.Verify(Password, user.PasswordHash))
            {
                _logger.LogWarning("Login failed: invalid password for Username='{Username}'", Username);
                ErrorMessage = "Invalid password";
                
                await _forensicLogger.LogAsync(new ForensicEvent(
                    ForensicEventType.LoginFailure,
                    DateTime.UtcNow,
                    Environment.MachineName,
                    Username,
                    "Invalid password",
                    null,
                    null
                ));
                return;
            }
            
            if (!user.IsActive)
            {
                _logger.LogWarning("Login failed: inactive user for Username='{Username}'", Username);
                ErrorMessage = "Account is disabled";
                return;
            }
            
            // Update last login
            user.LastLoginAtUtc = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);
            
            _logger.LogInformation("Login successful for Username='{Username}'", Username);
            
            await _forensicLogger.LogAsync(new ForensicEvent(
                ForensicEventType.LoginSuccess,
                DateTime.UtcNow,
                Environment.MachineName,
                Username,
                "Successful login",
                null,
                null
            ));
            
            LoginSucceeded = true;
            OnPropertyChanged(nameof(LoginSucceeded));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
            ErrorMessage = "An error occurred during login";
        }
        finally
        {
            IsBusy = false;
        }
    }
    
    private bool CanLogin()
    {
        return !IsBusy && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    }
}
```
**Purpose**: Login business logic and state management
- **User Validation**: Username/password verification
- **Security**: BCrypt password verification
- **Forensic Logging**: Tracks login attempts
- **Error Handling**: User-friendly error messages
- **State Management**: Busy states and success tracking

### BestFlex.Shell.ViewModels.NewSaleViewModel.cs
```csharp
public class NewSaleViewModel : INotifyPropertyChanged
{
    private readonly ISalesService _salesService;
    private readonly ICustomerReadService _customerService;
    private readonly IProductReadService _productService;
    private readonly INavigationService _navigationService;
    
    public ObservableCollection<CustomerDto> Customers { get; set; } = new();
    public ObservableCollection<SaleItemDto> SaleItems { get; set; } = new();
    public Guid SelectedCustomerId { get; set; }
    public decimal GrandTotal => SaleItems.Sum(item => item.Total);
    
    public ICommand AddItemCommand { get; }
    public ICommand CompleteSaleCommand { get; }
    public ICommand QuickAddCustomerCommand { get; }
    public ICommand ViewCustomerDetailsCommand { get; }
    
    public NewSaleViewModel(ISalesService salesService, ICustomerReadService customerService, 
                           IProductReadService productService, INavigationService navigationService)
    {
        _salesService = salesService;
        _customerService = customerService;
        _productService = productService;
        _navigationService = navigationService;
        
        AddItemCommand = new RelayCommand(ExecuteAddItem);
        CompleteSaleCommand = new RelayCommand(async () => await ExecuteCompleteSale(), CanCompleteSale);
        QuickAddCustomerCommand = new RelayCommand(ExecuteQuickAddCustomer);
        ViewCustomerDetailsCommand = new RelayCommand(ExecuteViewCustomerDetails, CanViewCustomerDetails);
        
        LoadData();
    }
    
    private async void LoadData()
    {
        var customers = await _customerService.GetAllCustomersAsync();
        Customers.Clear();
        foreach (var customer in customers)
        {
            Customers.Add(customer);
        }
    }
    
    private void ExecuteAddItem()
    {
        // Show product selection dialog
        var item = new SaleItemDto
        {
            Id = Guid.NewGuid(),
            ProductId = Guid.Empty,
            ProductName = "Select Product",
            Quantity = 1,
            UnitPrice = 0,
            Total = 0
        };
        
        SaleItems.Add(item);
        OnPropertyChanged(nameof(GrandTotal));
    }
    
    private async Task ExecuteCompleteSale()
    {
        try
        {
            var request = new CreateSaleRequest
            {
                CustomerId = SelectedCustomerId,
                Items = SaleItems.Select(item => new SaleItemRequest
                {
                    ProductId = item.ProductId,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice
                }).ToList()
            };
            
            var result = await _salesService.ProcessSaleAsync(request);
            
            if (result.IsSuccess)
            {
                // Navigate to invoice details
                await _navigationService.NavigateToInvoiceDetailsAsync(result.InvoiceId);
                
                // Clear current sale
                SaleItems.Clear();
                OnPropertyChanged(nameof(GrandTotal));
            }
            else
            {
                // Show error message
                MessageBox.Show($"Sale failed: {result.ErrorMessage}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private bool CanCompleteSale()
    {
        return SelectedCustomerId != Guid.Empty && 
               SaleItems.Any() && 
               SaleItems.All(item => item.ProductId != Guid.Empty && item.Quantity > 0);
    }
}
```
**Purpose**: Sales transaction management
- **Customer Management**: Load and select customers
- **Item Management**: Add/remove sale items
- **Total Calculation**: Automatic grand total updates
- **Sale Processing**: Complete sales transaction
- **Navigation**: Navigate to invoice details

## Data Access Layer

### BestFlex.Persistence.Data.BestFlexDbContext.cs
```csharp
public class BestFlexDbContext : DbContext
{
    public DbSet<Users> Users { get; set; }
    public DbSet<ProductEntity> Products { get; set; }
    public DbSet<CustomerEntity> Customers { get; set; }
    public DbSet<InvoiceEntity> Invoices { get; set; }
    public DbSet<InvoiceLineEntity> InvoiceLines { get; set; }
    public DbSet<JournalEntryEntity> JournalEntries { get; set; }
    public DbSet<JournalLineEntity> JournalLines { get; set; }
    public DbSet<AuditEntryEntity> AuditEntries { get; set; }
    public DbSet<StockTransactionEntity> StockTransactions { get; set; }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = @"d:\personal\BestFlex.Shell\bestflex_local.db";
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }
    }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Configure Users
        modelBuilder.Entity<Users>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(500);
            entity.Property(e => e.RolesCsv).HasMaxLength(500);
        });
        
        // Configure Products
        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProductCode).IsUnique();
            entity.Property(e => e.ProductCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Unit).HasMaxLength(20);
        });
        
        // Configure Invoices
        modelBuilder.Entity<InvoiceEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.Property(e => e.InvoiceNumber).IsRequired().HasMaxLength(50);
            entity.HasOne(e => e.Customer)
                  .WithMany(c => c.Invoices)
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        // Configure Invoice Lines
        modelBuilder.Entity<InvoiceLineEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.InvoiceLines)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure Journal Entries
        modelBuilder.Entity<JournalEntryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reference).HasMaxLength(100);
        });
        
        // Configure Journal Lines
        modelBuilder.Entity<JournalLineEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.JournalEntry)
                  .WithMany(je => je.JournalLines)
                  .HasForeignKey(e => e.JournalEntryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```
**Purpose**: Entity Framework database context
- **DbSets**: All entity collections
- **Configuration**: SQLite database configuration
- **Entity Mapping**: Fluent API configurations
- **Relationships**: Foreign key and navigation properties
- **Indexes**: Performance optimization indexes

## Navigation Service

### BestFlex.Shell.Services.NavigationService.cs
```csharp
public class NavigationService : INavigationService
{
    private readonly ContentControl _mainHost;
    private readonly IServiceProvider _services;
    private readonly Stack<UserControl> _navigationStack = new();
    
    public NavigationService(ContentControl mainHost, IServiceProvider services)
    {
        _mainHost = mainHost;
        _services = services;
    }
    
    public async Task NavigateToAsync<T>() where T : UserControl
    {
        var page = _services.GetRequiredService<T>();
        await NavigateToPageAsync(page);
    }
    
    public async Task NavigateToDashboardAsync()
    {
        await NavigateToAsync<DashboardPage>();
    }
    
    public async Task NavigateToNewSaleAsync()
    {
        await NavigateToAsync<NewSalePage>();
    }
    
    public async Task NavigateToInvoicesAsync()
    {
        await NavigateToAsync<InvoicesPage>();
    }
    
    public async Task NavigateToInvoiceDetailsAsync(Guid invoiceId)
    {
        var window = new InvoiceDetailsWindow(invoiceId);
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }
    
    public async Task NavigateToCustomerDetailsAsync(Guid customerId)
    {
        var window = new CustomerDetailsWindow(customerId);
        window.Owner = Application.Current.MainWindow;
        window.ShowDialog();
    }
    
    public void GoBack()
    {
        if (_navigationStack.Count > 1)
        {
            _navigationStack.Pop(); // Remove current page
            var previousPage = _navigationStack.Peek();
            _mainHost.Content = previousPage;
        }
    }
    
    private async Task NavigateToPageAsync(UserControl page)
    {
        // Add current page to navigation stack
        if (_mainHost.Content is UserControl currentPage)
        {
            _navigationStack.Push(currentPage);
        }
        
        // Navigate to new page
        _mainHost.Content = page;
        
        // Initialize page if needed
        if (page is IInitializablePage initializable)
        {
            await initializable.InitializeAsync();
        }
    }
}
```
**Purpose**: UI navigation management service
- **Page Navigation**: Navigate between main pages
- **Modal Windows**: Open detail windows
- **Navigation Stack**: Back navigation support
- **Dependency Injection**: Service integration
- **Async Support**: Asynchronous page initialization

This documentation provides comprehensive coverage of all major classes in the BestFlex ERP system, including their purpose, key properties, methods, and relationships. Any AI system given this documentation will have complete understanding of the system architecture, business logic, data models, and implementation details.
