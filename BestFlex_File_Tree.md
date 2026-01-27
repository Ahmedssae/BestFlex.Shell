# BestFlex Project - Complete File Tree (Windows Explorer Format)

```
📁 BestFlex/
├── 📄 .gitattributes
├── 📄 .gitignore
├── 📄 BestFlex.Shell.sln
├── 📄 bestflex_local.db
├── 📁 .git/
├── 📁 .github/
├── 📁 .vs/
├── 📁 .vscode/
│   ├── 📄 launch.json
│   └── 📄 tasks.json
├── 📁 BestFlex/
│   ├── 📄 BestFlex.csproj
│   ├── 📁 Properties/
│   │   └── 📄 launchSettings.json
│   ├── 📁 bin/
│   │   ├── 📁 Debug/
│   │   │   ├── 📁 net8.0/
│   │   │   └── 📁 net8.0-windows/
│   │   └── 📁 Release/
│   │       └── 📁 net8.0-windows/
│   └── 📁 obj/
│       ├── 📄 BestFlex.csproj.nuget.dgspec.json
│       ├── 📄 BestFlex.csproj.nuget.g.props
│       ├── 📄 BestFlex.csproj.nuget.g.targets
│       ├── 📄 project.assets.json
│       ├── 📄 project.nuget.cache
│       ├── 📁 Debug/
│       │   ├── 📁 net8.0/
│       │   └── 📁 net8.0-windows/
│       └── 📁 Release/
│           └── 📁 net8.0-windows/
├── 📁 BestFlex.Application/
│   ├── 📄 BestFlex.Application.csproj
│   ├── 📄 AuditEntry.cs
│   ├── 📄 Class1.cs
│   ├── 📄 UserFriendlyException.cs
│   ├── 📁 Abstractions/
│   │   ├── 📄 BackupRestore.cs
│   │   ├── 📄 CompanySettings.cs
│   │   ├── 📄 DataIntegrity.cs
│   │   ├── 📄 InvoicePrintData.cs
│   │   ├── 📄 ModulePolicy.cs
│   │   ├── 📄 Modules.cs
│   │   ├── 📄 Permission.cs
│   │   ├── 📄 PrintTemplateSettings.cs
│   │   ├── 📄 ReflectionExceptionUnwrapper.cs
│   │   ├── 📄 Safety.cs
│   │   ├── 📄 SystemEvent.cs
│   │   ├── 📄 UserFriendlyException.cs
│   │   ├── 📁 Inventory/
│   │   │   └── 📄 IPurchaseReceiveHandler.cs
│   │   ├── 📁 Statements/
│   │   │   └── 📄 ICustomerStatementService.cs
│   │   ├── 📄 IAccountingService.cs
│   │   ├── 📄 IAuditService.cs
│   │   ├── 📄 IAuthorizationService.cs
│   │   ├── 📄 ICacheService.cs
│   │   ├── 📄 ICurrentUserService.cs
│   │   ├── 📄 ICustomerReadService.cs
│   │   ├── 📄 IDependencyHealthService.cs
│   │   ├── 📄 IErrorService.cs
│   │   ├── 📄 IExecutionLockService.cs
│   │   ├── 📄 IFeatureService.cs
│   │   ├── 📄 IInvoicePdfExporter.cs
│   │   ├── 📄 IInvoicePrintService.cs
│   │   ├── 📄 IInvoiceQueryService.cs
│   │   ├── 📄 INavigationService.cs
│   │   ├── 📄 IPermissionService.cs
│   │   ├── 📄 IPrintingAvailabilityService.cs
│   │   ├── 📄 IProductReadService.cs
│   │   ├── 📄 IStockValidationService.cs
│   │   ├── 📄 IUnitOfWork.cs
│   │   └── 📄 IUserRepository.cs
│   ├── 📁 Contracts/
│   │   └── 📁 Sales/
│   │       └── 📄 NewSaleDto.cs
│   ├── 📁 Mapping/
│   │   └── 📄 ForensicToSystemSeverityMapper.cs
│   ├── 📁 Services/
│   │   ├── 📄 SalesService.cs
│   │   └── 📄 StockValidationService.cs
│   ├── 📁 bin/
│   │   └── 📁 Debug/
│   │       └── 📁 net8.0/
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Application.Abstractions/
│   ├── 📄 BestFlex.Application.Abstractions.csproj
│   ├── 📁 bin/
│   │   └── 📁 Debug/
│   │       └── 📁 net8.0/
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Application.Services/
│   ├── 📄 BestFlex.Application.Services.csproj
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Domain/
│   ├── 📄 BestFlex.Domain.csproj
│   ├── 📁 Entities/
│   │   ├── 📄 AuditEntryEntity.cs
│   │   ├── 📄 CustomerEntity.cs
│   │   ├── 📄 InvoiceEntity.cs
│   │   ├── 📄 InvoiceLineEntity.cs
│   │   ├── 📄 JournalEntryEntity.cs
│   │   ├── 📄 JournalLineEntity.cs
│   │   ├── 📄 ProductEntity.cs
│   │   ├── 📄 StockTransactionEntity.cs
│   │   └── 📄 Users.cs
│   ├── 📁 ValueObjects/
│   │   ├── 📄 ForensicEventType.cs
│   │   ├── 📄 InvoiceStatus.cs
│   │   ├── 📄 PaymentStatus.cs
│   │   ├── 📄 ProductCategory.cs
│   │   └── 📄 UserRole.cs
│   ├── 📄 ForensicEvent.cs
│   ├── 📄 IForensicLogger.cs
│   ├── 📁 bin/
│   │   └── 📁 Debug/
│   │       └── 📁 net8.0/
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Infrastructure/
│   ├── 📄 BestFlex.Infrastructure.csproj
│   ├── 📁 Auth/
│   │   ├── 📄 AuthorizationService.cs
│   │   ├── 📄 CurrentUserService.cs
│   │   └── 📄 PermissionService.cs
│   ├── 📁 Commands/
│   │   ├── 📄 AsyncRelayCommand.cs
│   │   ├── 📄 RelayCommand.cs
│   │   └── 📄 RelayCommandGeneric.cs
│   ├── 📁 Diagnostics/
│   │   ├── 📄 DatabaseIntegrityValidator.cs
│   │   ├── 📄 DependencyHealthService.cs
│   │   ├── 📄 EnvironmentContext.cs
│   │   ├── 📄 ForensicLogger.cs
│   │   ├── 📄 KillSwitchService.cs
│   │   ├── 📄 PersistentSystemEventSink.cs
│   │   ├── 📄 ReadOnlyModeService.cs
│   │   ├── 📄 RestoreSimulationService.cs
│   │   ├── 📄 SqliteBackupService.cs
│   │   └── 📄 SystemSafetyPolicy.cs
│   ├── 📁 Services/
│   │   ├── 📄 AuditService.cs
│   │   ├── 📄 CacheService.cs
│   │   ├── 📄 ErrorService.cs
│   │   ├── 📄 ExecutionLockService.cs
│   │   ├── 📄 FeatureService.cs
│   │   ├── 📄 InvoicePdfExporter.cs
│   │   ├── 📄 InvoicePrintService.cs
│   │   ├── 📄 InvoiceQueryService.cs
│   │   ├── 📄 ProductReadService.cs
│   │   └── 📄 UserRepository.cs
│   ├── 📁 Transactions/
│   │   └── 📄 UnitOfWork.cs
│   ├── 📁 bin/
│   │   └── 📁 Debug/
│   │       └── 📁 net8.0/
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Localization/
│   ├── 📄 BestFlex.Localization.csproj
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Modules.Common/
│   ├── 📄 BestFlex.Modules.Common.csproj
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Modules.Warehouse/
│   ├── 📄 BestFlex.Modules.Warehouse.csproj
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Persistence/
│   ├── 📄 BestFlex.Persistence.csproj
│   ├── 📁 Data/
│   │   ├── 📄 BestFlexDbContext.cs
│   │   └── 📄 DesignTimeFactory.cs
│   ├── 📁 Migrations/
│   │   ├── 📄 20240101000000_InitialCreate.cs
│   │   ├── 📄 20240101000000_InitialCreate.Designer.cs
│   │   ├── 📄 BestFlexDbContextModelSnapshot.cs
│   │   └── 📄 ApplicationDbContextModelSnapshot.cs
│   ├── 📁 Repositories/
│   │   ├── 📄 CustomerRepository.cs
│   │   ├── 📄 InvoiceRepository.cs
│   │   ├── 📄 ProductRepository.cs
│   │   └── 📄 UserRepository.cs
│   ├── 📁 bin/
│   │   └── 📁 Debug/
│   │       └── 📁 net8.0/
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Printing/
│   ├── 📄 BestFlex.Printing.csproj
│   ├── 📁 LatoFont/
│   │   ├── 📄 Lato-Black.ttf
│   │   ├── 📄 Lato-BlackItalic.ttf
│   │   ├── 📄 Lato-Bold.ttf
│   │   ├── 📄 Lato-BoldItalic.ttf
│   │   ├── 📄 Lato-ExtraBold.ttf
│   │   ├── 📄 Lato-ExtraBoldItalic.ttf
│   │   ├── 📄 Lato-ExtraLight.ttf
│   │   ├── 📄 Lato-ExtraLightItalic.ttf
│   │   ├── 📄 Lato-Italic.ttf
│   │   ├── 📄 Lato-Light.ttf
│   │   ├── 📄 Lato-LightItalic.ttf
│   │   ├── 📄 Lato-Medium.ttf
│   │   ├── 📄 Lato-MediumItalic.ttf
│   │   ├── 📄 Lato-Regular.ttf
│   │   ├── 📄 Lato-SemiBold.ttf
│   │   ├── 📄 Lato-SemiBoldItalic.ttf
│   │   ├── 📄 Lato-Thin.ttf
│   │   ├── 📄 Lato-ThinItalic.ttf
│   │   └── 📄 OFL.txt
│   ├── 📁 bin/
│   │   └── 📁 Debug/
│   │       └── 📁 net8.0/
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0/
├── 📁 BestFlex.Shell/
│   ├── 📄 BestFlex.Shell.csproj
│   ├── 📄 App.xaml
│   ├── 📄 App.xaml.cs
│   ├── 📄 MainWindow.xaml
│   ├── 📄 MainWindow.xaml.cs
│   ├── 📄 LoginWindow.xaml
│   ├── 📄 LoginWindow.xaml.cs
│   ├── 📁 Windows/
│   │   ├── 📄 AccountStatementWindow.xaml
│   │   ├── 📄 AccountStatementWindow.xaml.cs
│   │   ├── 📄 GrnPreviewWindow.xaml
│   │   ├── 📄 GrnPreviewWindow.xaml.cs
│   │   ├── 📄 InvoicePreviewWindow.xaml
│   │   ├── 📄 InvoicePreviewWindow.xaml.cs
│   │   ├── 📄 LowStockWindow.xaml
│   │   ├── 📄 LowStockWindow.xaml.cs
│   │   ├── 📄 QuickAddCustomerWindow.xaml
│   │   ├── 📄 QuickAddCustomerWindow.xaml.cs
│   │   ├── 📄 QuickAddProductWindow.xaml
│   │   ├── 📄 QuickAddProductWindow.xaml.cs
│   │   ├── 📄 StatementPreviewWindow.xaml
│   │   ├── 📄 StatementPreviewWindow.xaml.cs
│   │   ├── 📄 UnpaidInvoicesWindow.xaml
│   │   └── 📄 UnpaidInvoicesWindow.xaml.cs
│   ├── 📁 Pages/
│   │   ├── 📄 DashboardPage.xaml
│   │   ├── 📄 DashboardPage.xaml.cs
│   │   ├── 📄 InvoicesPage.xaml
│   │   ├── 📄 InvoicesPage.xaml.cs
│   │   ├── 📄 NewSalePage.xaml
│   │   ├── 📄 NewSalePage.xaml.cs
│   │   ├── 📄 TemplateDesignerPage.xaml
│   │   └── 📄 TemplateDesignerPage.xaml.cs
│   ├── 📁 Views/
│   │   ├── 📄 SafeFallbackView.xaml
│   │   ├── 📄 SafeFallbackView.xaml.cs
│   │   └── 📁 Pages/
│   │       ├── 📁 Inventory/
│   │       │   ├── 📄 ProductsPage.xaml
│   │       │   ├── 📄 ProductsPage.xaml.cs
│   │       │   ├── 📄 ReceiveStockPage.xaml
│   │       │   └── 📄 ReceiveStockPage.xaml.cs
│   │       └── 📁 Sales/
│   │           ├── 📄 CustomerStatementsPage.xaml
│   │           └── 📄 CustomerStatementsPage.xaml.cs
│   ├── 📁 ViewModels/
│   │   ├── 📄 ChangePasswordViewModel.cs
│   │   ├── 📄 DashboardViewModel.cs
│   │   ├── 📄 InvoiceListViewModel.cs
│   │   ├── 📄 LoginViewModel.cs
│   │   ├── 📄 MainWindowViewModel.cs
│   │   └── 📄 NewSaleViewModel.cs
│   ├── 📁 Services/
│   │   ├── 📄 FeatureAwareNavigationService.cs
│   │   └── 📄 NavigationService.cs
│   ├── 📁 Theme/
│   │   ├── 📄 Theme.xaml
│   │   └── 📄 Theme.Dark.xaml
│   ├── 📁 UI/
│   │   └── 📁 Toasts/
│   │       ├── 📄 ToastWindow.xaml
│   │       └── 📄 ToastWindow.xaml.cs
│   ├── 📁 Infrastructure/
│   │   ├── 📁 Commands/
│   │   │   ├── 📄 AsyncRelayCommand.cs
│   │   │   ├── 📄 RelayCommand.cs
│   │   │   └── 📄 RelayCommandGeneric.cs
│   │   ├── 📁 Services/
│   │   │   ├── 📄 CacheService.cs
│   │   │   ├── 📄 ErrorService.cs
│   │   │   └── 📄 ExecutionLockService.cs
│   │   └── 📁 Diagnostics/
│   │       ├── 📄 DatabaseIntegrityValidator.cs
│   │       ├── 📄 DependencyHealthService.cs
│   │       ├── 📄 EnvironmentContext.cs
│   │       ├── 📄 ForensicLogger.cs
│   │       ├── 📄 KillSwitchService.cs
│   │       ├── 📄 PersistentSystemEventSink.cs
│   │       ├── 📄 ReadOnlyModeService.cs
│   │       ├── 📄 RestoreSimulationService.cs
│   │       ├── 📄 SqliteBackupService.cs
│   │       └── 📄 SystemSafetyPolicy.cs
│   ├── 📁 Printing/
│   │   ├── 📄 QuickPrintPreviewWindow.xaml
│   │   └── 📄 QuickPrintPreviewWindow.xaml.cs
│   ├── 📁 InvoiceTemplates/
│   │   └── 📄 DefaultInvoiceTemplate.xaml
│   ├── 📁 Navigation/
│   │   ├── 📄 NavigationService.cs
│   │   └── 📄 PageRegistry.cs
│   ├── 📄 ChangePasswordWindow.xaml
│   ├── 📄 ChangePasswordWindow.xaml.cs
│   ├── 📄 InvoiceDetailsWindow.xaml
│   ├── 📄 InvoiceDetailsWindow.xaml.cs
│   ├── 📄 InvoiceListWindow.xaml
│   ├── 📄 InvoiceListWindow.xaml.cs
│   ├── 📄 NewSaleWindow.xaml
│   ├── 📄 NewSaleWindow.xaml.cs
│   ├── 📄 PrintPreviewWindow.xaml
│   ├── 📄 PrintPreviewWindow.xaml.cs
│   ├── 📄 SettingsWindow.xaml
│   ├── 📄 SettingsWindow.xaml.cs
│   ├── 📁 bin/
│   │   └── 📁 Debug/
│   │       └── 📁 net8.0-windows/
│   └── 📁 obj/
│       └── 📁 Debug/
│           └── 📁 net8.0-windows/
└── 📁 BestFlex.Tests/
    ├── 📄 BestFlex.Tests.csproj
    └── 📁 obj/
        └── 📁 Debug/
            └── 📁 net8.0/
```

## File Summary

### Total Files Count: ~150+ files
- **C# Files**: ~80 files (.cs)
- **XAML Files**: ~30 files (.xaml)
- **Project Files**: ~12 files (.csproj)
- **Configuration Files**: ~8 files (.json, .config, etc.)
- **Font Files**: ~15 files (.ttf)
- **Other**: ~5 files (.md, .txt, etc.)

### Key File Categories:
1. **Application Entry**: App.xaml/.cs
2. **Main Windows**: MainWindow.xaml/.cs, LoginWindow.xaml/.cs
3. **Business Pages**: Dashboard, NewSale, Invoices, etc.
4. **Modal Windows**: 10+ specialized windows for various functions
5. **ViewModels**: MVVM pattern implementation
6. **Services**: Navigation, business logic, infrastructure
7. **Data Layer**: Entity Framework, repositories, migrations
8. **Infrastructure**: Authentication, diagnostics, utilities
9. **Themes**: Light and dark theme support
10. **Printing**: Invoice templates and preview functionality

This file tree represents the complete structure of the BestFlex ERP system exactly as it appears in Windows Explorer, including all files, folders, and their hierarchical relationships.
