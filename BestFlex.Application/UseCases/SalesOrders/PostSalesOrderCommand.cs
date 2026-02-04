using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Application.UseCases.SalesOrders
{
    public class PostSalesOrderCommand : IRequest<PostSalesOrderResult>
    {
        public int DraftOrderId { get; set; }
    }

    public class PostSalesOrderResult
    {
        public bool Success { get; set; }
        public int? PostedOrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime PostingDate { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> BusinessErrors { get; set; } = new();
        public List<InventoryMovementDto> InventoryMovements { get; set; } = new();
        public JournalEntryDto JournalEntry { get; set; } = new();
        public decimal TotalAmount { get; set; }
        public decimal TotalTax { get; set; }
    }

    public class InventoryMovementDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public string MovementType { get; set; } = "OUT";
        public DateTime Timestamp { get; set; }
    }

    public class JournalEntryDto
    {
        public int Id { get; set; }
        public string EntryNumber { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public List<JournalLineDto> Lines { get; set; } = new();
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
    }

    public class JournalLineDto
    {
        public int AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class PostSalesOrderCommandHandler : IRequestHandler<PostSalesOrderCommand, PostSalesOrderResult>
    {
        private readonly ISalesOrderTransactionRepository _salesOrderRepository;
        private readonly IInventoryRepository _inventoryRepository;
        private readonly IJournalRepository _journalRepository;

        public PostSalesOrderCommandHandler(
            ISalesOrderTransactionRepository salesOrderRepository,
            IInventoryRepository inventoryRepository,
            IJournalRepository journalRepository)
        {
            _salesOrderRepository = salesOrderRepository ?? throw new ArgumentNullException(nameof(salesOrderRepository));
            _inventoryRepository = inventoryRepository ?? throw new ArgumentNullException(nameof(inventoryRepository));
            _journalRepository = journalRepository ?? throw new ArgumentNullException(nameof(journalRepository));
        }

        public async Task<PostSalesOrderResult> Handle(PostSalesOrderCommand request, CancellationToken cancellationToken)
        {
            var result = new PostSalesOrderResult
            {
                PostingDate = DateTime.UtcNow
            };

            // Phase 5: Strict validation before any database operations
            var validationResult = await ValidateSalesOrderForPosting(request.DraftOrderId, cancellationToken);
            if (!validationResult.IsValid)
            {
                result.ValidationErrors.AddRange(validationResult.Errors);
                return result;
            }

            var salesOrder = validationResult.SalesOrder!;

            // Phase 5: Transactional posting - ALL or NOTHING
            using var transaction = await _salesOrderRepository.BeginTransactionAsync(cancellationToken);
            try
            {
                // Step 1: Reserve and deduct inventory
                var inventoryResult = await ProcessInventoryMovements(salesOrder, cancellationToken);
                if (!inventoryResult.Success)
                {
                    result.BusinessErrors.AddRange(inventoryResult.Errors);
                    transaction.Dispose();
                    return result;
                }

                // Step 2: Create accounting journal entries
                var journalResult = await CreateAccountingEntries(salesOrder, inventoryResult.Movements, cancellationToken);
                if (!journalResult.Success)
                {
                    result.BusinessErrors.AddRange(journalResult.Errors);
                    transaction.Dispose();
                    return result;
                }

                // Step 3: Mark sales order as posted
                salesOrder.Post(DateTime.UtcNow, GenerateOrderNumber());
                await _salesOrderRepository.UpdateAsync(salesOrder, cancellationToken);

                // Step 4: Commit transaction
                transaction.Dispose();

                // Step 5: Build success result
                result.Success = true;
                result.PostedOrderId = salesOrder.Id;
                result.OrderNumber = salesOrder.OrderNumber;
                result.TotalAmount = salesOrder.TotalAmount;
                result.TotalTax = salesOrder.TaxAmount;
                result.InventoryMovements = inventoryResult.Movements;
                result.JournalEntry = journalResult.JournalEntry;

                return result;
            }
            catch (DomainException ex)
            {
                transaction.Dispose();
                result.BusinessErrors.Add(ex.Message);
                return result;
            }
            catch (Exception ex)
            {
                transaction.Dispose();
                result.BusinessErrors.Add("Posting failed due to a system error");
                // Log the full exception for debugging
                Console.WriteLine($"Posting error: {ex}");
                return result;
            }
        }

        private async Task<(bool IsValid, SalesOrder? SalesOrder, List<string> Errors)> ValidateSalesOrderForPosting(
            int orderId, CancellationToken cancellationToken)
        {
            var errors = new List<string>();

            // Load sales order
            var salesOrder = await _salesOrderRepository.GetByIdAsync(orderId, cancellationToken);
            if (salesOrder == null)
            {
                errors.Add("Sales order not found");
                return (false, null, errors);
            }

            // Validate status
            if (salesOrder.Status != SalesOrderStatus.Draft)
            {
                errors.Add($"Cannot post order in {salesOrder.Status} status. Only Draft orders can be posted.");
                return (false, salesOrder, errors);
            }

            // Validate lines
            if (!salesOrder.Lines.Any())
            {
                errors.Add("Cannot post order without line items");
                return (false, salesOrder, errors);
            }

            // Validate each line
            foreach (var line in salesOrder.Lines)
            {
                if (line.Quantity <= 0)
                {
                    errors.Add($"Line {line.Id}: Quantity must be greater than 0");
                }

                if (line.UnitPrice < 0)
                {
                    errors.Add($"Line {line.Id}: Unit price cannot be negative");
                }
            }

            return (errors.Count == 0, salesOrder, errors);
        }

        private async Task<(bool Success, List<InventoryMovementDto> Movements, List<string> Errors)> ProcessInventoryMovements(
            SalesOrder salesOrder, CancellationToken cancellationToken)
        {
            var movements = new List<InventoryMovementDto>();
            var errors = new List<string>();

            foreach (var line in salesOrder.Lines)
            {
                try
                {
                    // Check inventory availability
                    var availableStock = await _inventoryRepository.GetAvailableStockAsync(line.ProductId, cancellationToken);
                    if (availableStock < line.Quantity)
                    {
                        errors.Add($"Insufficient stock for product {line.ProductId}. Available: {availableStock}, Required: {line.Quantity}");
                        continue;
                    }

                    // Get cost using FIFO/AVCO
                    var unitCost = await _inventoryRepository.GetUnitCostAsync(line.ProductId, line.Quantity, cancellationToken);

                    // Create inventory movement
                    var movement = new InventoryMovementDto
                    {
                        ProductId = line.ProductId,
                        ProductName = $"Product {line.ProductId}", // Would load from product repository
                        Quantity = line.Quantity,
                        UnitCost = unitCost,
                        TotalCost = unitCost * line.Quantity,
                        MovementType = "OUT",
                        Timestamp = DateTime.UtcNow
                    };

                    // Deduct inventory
                    await _inventoryRepository.DeductStockAsync(line.ProductId, line.Quantity, unitCost, cancellationToken);

                    movements.Add(movement);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to process inventory for product {line.ProductId}: {ex.Message}");
                }
            }

            return (errors.Count == 0, movements, errors);
        }

        private async Task<(bool Success, JournalEntryDto JournalEntry, List<string> Errors)> CreateAccountingEntries(
            SalesOrder salesOrder, List<InventoryMovementDto> movements, CancellationToken cancellationToken)
        {
            var errors = new List<string>();
            var journalEntry = new JournalEntryDto
            {
                EntryNumber = await GenerateJournalEntryNumberAsync(cancellationToken),
                EntryDate = DateTime.UtcNow
            };

            try
            {
                // Sales Revenue Entry (Double Entry)
                // Debit: Accounts Receivable
                journalEntry.Lines.Add(new JournalLineDto
                {
                    AccountId = 1001, // Accounts Receivable
                    AccountName = "Accounts Receivable",
                    AccountType = "Asset",
                    DebitAmount = salesOrder.TotalAmount,
                    CreditAmount = 0,
                    Description = $"Sales Order {salesOrder.OrderNumber}"
                });

                // Credit: Sales Revenue
                journalEntry.Lines.Add(new JournalLineDto
                {
                    AccountId = 4001, // Sales Revenue
                    AccountName = "Sales Revenue",
                    AccountType = "Revenue",
                    DebitAmount = 0,
                    CreditAmount = salesOrder.TotalAmount - salesOrder.TaxAmount,
                    Description = $"Sales Order {salesOrder.OrderNumber}"
                });

                // Credit: Tax Payable
                if (salesOrder.TaxAmount > 0)
                {
                    journalEntry.Lines.Add(new JournalLineDto
                    {
                        AccountId = 2001, // Tax Payable
                        AccountName = "Tax Payable",
                        AccountType = "Liability",
                        DebitAmount = 0,
                        CreditAmount = salesOrder.TaxAmount,
                        Description = $"Tax on Sales Order {salesOrder.OrderNumber}"
                    });
                }

                // COGS Entry (Double Entry)
                var totalCogs = movements.Sum(m => m.TotalCost);
                if (totalCogs > 0)
                {
                    // Debit: Cost of Goods Sold
                    journalEntry.Lines.Add(new JournalLineDto
                    {
                        AccountId = 5001, // COGS
                        AccountName = "Cost of Goods Sold",
                        AccountType = "Expense",
                        DebitAmount = totalCogs,
                        CreditAmount = 0,
                        Description = $"COGS for Sales Order {salesOrder.OrderNumber}"
                    });

                    // Credit: Inventory Asset
                    journalEntry.Lines.Add(new JournalLineDto
                    {
                        AccountId = 1002, // Inventory
                        AccountName = "Inventory",
                        AccountType = "Asset",
                        DebitAmount = 0,
                        CreditAmount = totalCogs,
                        Description = $"Inventory reduction for Sales Order {salesOrder.OrderNumber}"
                    });
                }

                // Validate accounting balance
                var totalDebit = journalEntry.Lines.Sum(l => l.DebitAmount);
                var totalCredit = journalEntry.Lines.Sum(l => l.CreditAmount);

                if (Math.Abs(totalDebit - totalCredit) > 0.01m) // Allow for rounding
                {
                    errors.Add($"Accounting entries do not balance. Debit: {totalDebit}, Credit: {totalCredit}");
                    return (false, journalEntry, errors);
                }

                journalEntry.TotalDebit = totalDebit;
                journalEntry.TotalCredit = totalCredit;

                // Save journal entry
                var savedJournal = await _journalRepository.SaveJournalEntryAsync(journalEntry, cancellationToken);
                journalEntry.Id = savedJournal.Id;

                return (true, journalEntry, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to create accounting entries: {ex.Message}");
                return (false, journalEntry, errors);
            }
        }

        private async Task<string> GenerateJournalEntryNumberAsync(CancellationToken cancellationToken)
        {
            return $"JE-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        }

        private string GenerateOrderNumber()
        {
            return $"SO-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
        }
    }

    // Additional repository interfaces for Phase 5
    public interface IInventoryRepository
    {
        Task<decimal> GetAvailableStockAsync(int productId, CancellationToken cancellationToken);
        Task<decimal> GetUnitCostAsync(int productId, decimal quantity, CancellationToken cancellationToken);
        Task DeductStockAsync(int productId, decimal quantity, decimal unitCost, CancellationToken cancellationToken);
    }

    public interface IJournalRepository
    {
        Task<JournalEntryDto> SaveJournalEntryAsync(JournalEntryDto journalEntry, CancellationToken cancellationToken);
    }

    // Extended interface for Phase 5 transaction support
    public interface ISalesOrderTransactionRepository : ISalesOrderRepository
    {
        Task<IDisposable> BeginTransactionAsync(CancellationToken cancellationToken);
    }
}
