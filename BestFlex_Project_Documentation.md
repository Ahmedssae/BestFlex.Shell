# BestFlex ERP System - Complete Project Documentation

## Table of Contents
1. [Project Overview](#project-overview)
2. [Solution Structure](#solution-structure)
3. [Architecture](#architecture)
4. [Core Projects](#core-projects)
5. [Shell Project (WPF Application)](#shell-project-wpf-application)
6. [Data Layer](#data-layer)
7. [Business Logic](#business-logic)
8. [Infrastructure](#infrastructure)
9. [Key Features](#key-features)
10. [Configuration](#configuration)

---

## Project Overview

BestFlex is a comprehensive Enterprise Resource Planning (ERP) system built with .NET 8.0 and WPF. It provides inventory management, sales, invoicing, customer management, and accounting capabilities with a focus on data integrity, security, and user experience.

**Key Technologies:**
- .NET 8.0 (C#)
- WPF (Windows Presentation Foundation)
- Entity Framework Core with SQLite
- Dependency Injection
- MVVM Pattern
- Material Design UI

---

## Solution Structure

```
BestFlex/
├── BestFlex.sln                           # Solution file
├── BestFlex/                              # Domain layer
├── BestFlex.Application/                  # Application services
├── BestFlex.Application.Abstractions/      # Application interfaces
├── BestFlex.Application.Services/          # Application service implementations
├── BestFlex.Domain/                       # Domain entities and logic
├── BestFlex.Infrastructure/                # Infrastructure implementations
├── BestFlex.Localization/                  # Localization resources
├── BestFlex.Modules.Common/               # Common module functionality
├── BestFlex.Modules.Warehouse/            # Warehouse-specific functionality
├── BestFlex.Persistence/                   # Data access layer
├── BestFlex.Printing/                     # Printing services
├── BestFlex.Shell/                        # WPF application shell
├── BestFlex.Tests/                        # Unit tests
└── bestflex_local.db                       # SQLite database
```

---

## Architecture

The system follows a clean architecture pattern with clear separation of concerns:

1. **Domain Layer** - Core business entities and logic
2. **Application Layer** - Application services and interfaces
3. **Infrastructure Layer** - External concerns (data, printing, etc.)
4. **Presentation Layer** - WPF UI with MVVM pattern

---

## Core Projects

### BestFlex.Domain
**Purpose**: Core domain entities and business logic

**Key Files:**
- `Entities/` - Domain entities (Users, Products, Invoices, etc.)
- `ForensicEvent.cs` - Forensic logging events
- `ValueObjects/` - Value objects and enums

**Key Entities:**
- `Users` - User management
- `Products` - Product catalog
- `Invoices` - Sales invoices
- `Customers` - Customer management
- `JournalEntries` - Accounting entries
- `AuditEntryEntity` - Audit trail

### BestFlex.Application.Abstractions
**Purpose**: Interface definitions for application services

**Key Interfaces:**
- `IAccountingService` - Accounting operations
- `IAuditService` - Audit logging
- `ICurrentUserService` - Current user context
- `IInvoiceQueryService` - Invoice queries
- `INavigationService` - UI navigation
- `IProductReadService` - Product queries
- `IUnitOfWork` - Unit of work pattern
- `IDependencyHealthService` - Dependency validation
- `IDataIntegrityValidator` - Data integrity checks

### BestFlex.Application
**Purpose**: Application services and DTOs

**Key Services:**
- `SalesService` - Sales business logic
- `StockValidationService` - Inventory validation
- `Mapping/` - AutoMapper profiles

---

## Shell Project (WPF Application)

### Application Structure

```
BestFlex.Shell/
├── App.xaml/.cs                          # Application entry point
├── MainWindow.xaml/.cs                   # Main application window
├── LoginWindow.xaml/.cs                  # Login authentication
├── Windows/                              # Modal windows
│   ├── AccountStatementWindow.xaml/.cs
│   ├── GrnPreviewWindow.xaml/.cs
│   ├── InvoicePreviewWindow.xaml/.cs
│   ├── LowStockWindow.xaml/.cs
│   ├── QuickAddCustomerWindow.xaml/.cs
│   ├── QuickAddProductWindow.xaml/.cs
│   ├── StatementPreviewWindow.xaml/.cs
│   └── UnpaidInvoicesWindow.xaml/.cs
├── Pages/                                # Main content pages
│   ├── DashboardPage.xaml/.cs           # Main dashboard
│   ├── InvoicesPage.xaml/.cs             # Invoice management
│   ├── NewSalePage.xaml/.cs              # Sales interface
│   └── TemplateDesignerPage.xaml/.cs    # Template designer
├── Views/                                # Additional views
│   ├── Pages/Inventory/
│   │   ├── ProductsPage.xaml/.cs        # Product management
│   │   └── ReceiveStockPage.xaml/.cs    # Stock receiving
│   ├── Pages/Sales/
│   │   └── CustomerStatementsPage.xaml/.cs
│   └── SafeFallbackView.xaml/.cs        # Error fallback
├── ViewModels/                           # MVVM view models
│   ├── LoginViewModel.cs                 # Login logic
│   ├── MainWindowViewModel.cs            # Main window logic
│   ├── DashboardViewModel.cs             # Dashboard logic
│   ├── InvoiceListViewModel.cs           # Invoice list logic
│   ├── NewSaleViewModel.cs               # Sales logic
│   └── ChangePasswordViewModel.cs        # Password management
├── Services/                             # Application services
│   ├── NavigationService.cs              # UI navigation
│   └── FeatureAwareNavigationService.cs  # Feature-aware navigation
├── Theme/                                # UI themes
│   ├── Theme.xaml                        # Light theme
│   └── Theme.Dark.xaml                   # Dark theme
├── UI/Toasts/                            # Toast notifications
│   └── ToastWindow.xaml/.cs
├── Infrastructure/                       # Shell-specific infrastructure
│   ├── Commands/                         # Relay commands
│   ├── Services/                         # Shell services
│   └── Diagnostics/                      # Diagnostics and validation
├── Printing/                             # Printing functionality
│   └── QuickPrintPreviewWindow.xaml/.cs
└── InvoiceTemplates/                     # Invoice templates
    └── DefaultInvoiceTemplate.xaml
```

### Key Windows and Pages

#### App.xaml/.cs
**Purpose**: Application entry point and configuration
- Sets up dependency injection container
- Configures services and database
- Handles application startup and shutdown
- Implements data integrity validation
- Manages application lifecycle

#### MainWindow.xaml/.cs
**Purpose**: Main application window shell
- Contains navigation sidebar
- Hosts content pages
- Provides main menu and user interface
- Manages theme switching
- Handles user session

#### LoginWindow.xaml/.cs
**Purpose**: User authentication interface
- Username/password authentication
- Integration with user management
- Session initialization
- Error handling for authentication failures

#### DashboardPage.xaml/.cs
**Purpose**: Main dashboard and overview
- Business metrics display
- Quick access to common functions
- Low stock alerts
- Theme switching capability
- Real-time data updates

#### NewSalePage.xaml/.cs
**Purpose**: Sales transaction interface
- Product selection and pricing
- Customer management
- Invoice generation
- Payment processing
- Stock validation

#### InvoicesPage.xaml/.cs
**Purpose**: Invoice management
- Invoice listing and filtering
- Invoice details and editing
- Payment status tracking
- Customer statements
- Invoice printing

### ViewModels

#### LoginViewModel.cs
**Purpose**: Login business logic
- User authentication
- Password validation
- Error handling
- Login command implementation

#### MainWindowViewModel.cs
**Purpose**: Main window coordination
- Navigation management
- User session handling
- Menu command coordination
- Theme management

#### DashboardViewModel.cs
**Purpose**: Dashboard data management
- Business metrics calculation
- Low stock monitoring
- Real-time data updates
- Theme switching logic

#### NewSaleViewModel.cs
**Purpose**: Sales transaction logic
- Product management
- Customer handling
- Invoice creation
- Payment processing
- Stock updates

---

## Data Layer

### BestFlex.Persistence
**Purpose**: Entity Framework Core data access

**Key Files:**
- `Data/BestFlexDbContext.cs` - Main database context
- `Data/DesignTimeFactory.cs` - Design-time DbContext factory
- `Repositories/` - Repository implementations
- `Migrations/` - Database migrations

**Key Entities:**
- Users, Products, Invoices, Customers
- JournalEntries (accounting)
- AuditEntryEntity (audit trail)
- Stock records and transactions

**Database Configuration:**
- SQLite database (bestflex_local.db)
- Entity Framework Core migrations
- Absolute database path configuration

---

## Business Logic

### BestFlex.Application.Services
**Purpose**: Core business logic implementations

**Key Services:**
- `SalesService.cs` - Sales transaction processing
- `StockValidationService.cs` - Inventory validation
- Accounting services
- Invoice generation and management

### BestFlex.Infrastructure
**Purpose**: Infrastructure implementations

**Key Components:**
- `Diagnostics/` - System diagnostics and validation
- `Services/` - Infrastructure services
- `Auth/` - Authentication and authorization
- `Transactions/` - Transaction management

**Key Services:**
- `DatabaseIntegrityValidator.cs` - Data integrity validation
- `DependencyHealthService.cs` - Dependency health checks
- `AuditService.cs` - Audit logging
- `ForensicLogger.cs` - Forensic logging

---

## Key Features

### 1. User Management
- User authentication and authorization
- Role-based access control
- Password management
- Session management

### 2. Inventory Management
- Product catalog management
- Stock level monitoring
- Low stock alerts
- Purchase order processing
- Stock receiving

### 3. Sales Management
- Sales transaction processing
- Invoice generation
- Customer management
- Payment processing
- Sales reporting

### 4. Accounting
- Double-entry bookkeeping
- Journal entry management
- Financial reporting
- Audit trail

### 5. Reporting and Printing
- Invoice printing
- Customer statements
- Sales reports
- Custom templates

### 6. Data Integrity
- Database integrity validation
- Backup and restore
- Forensic logging
- Audit trail

---

## Configuration

### Application Configuration
- Environment variables support
- Kill switches for features
- Module-based architecture
- Safety policies

### Database Configuration
- SQLite database
- Entity Framework migrations
- Connection string management
- Backup and restore functionality

### UI Configuration
- Theme support (Light/Dark)
- Material Design components
- Responsive layout
- Accessibility features

---

## Development Notes

### Build Configuration
- .NET 8.0 target framework
- WPF application
- Debug and Release configurations
- NuGet package management

### Testing
- Unit tests in BestFlex.Tests
- Integration testing support
- Mock implementations for testing

### Deployment
- Self-contained deployment option
- Database migration support
- Configuration management
- Error handling and logging

---

## Security Features

### Authentication
- Password hashing with BCrypt
- Session management
- User role validation

### Data Protection
- Audit logging
- Forensic logging
- Data integrity validation
- Backup and restore

### Access Control
- Role-based permissions
- Feature toggles
- Module-based access control

---

## Performance Considerations

### Database Optimization
- Entity Framework optimization
- Connection pooling
- Query optimization
- Index management

### UI Performance
- Async/await patterns
- Virtualization for large lists
- Lazy loading
- Memory management

---

## Error Handling

### Global Error Handling
- UI exception translation
- Safe fallback views
- Error logging and reporting
- User-friendly error messages

### Data Validation
- Input validation
- Business rule validation
- Data integrity checks
- Constraint validation

---

## Extensibility

### Module System
- Pluggable architecture
- Module discovery
- Feature toggles
- Custom modules support

### Customization
- Theme system
- Template designer
- Custom reports
- Plugin architecture

---

This documentation provides a comprehensive overview of the BestFlex ERP system, covering all major components, architecture patterns, and implementation details. Any AI system given this documentation should have a complete understanding of the project structure, functionality, and implementation approach.
