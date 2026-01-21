using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;
using BestFlex.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BestFlex.Infrastructure.Services
{
    public class AccountingService : IAccountingService
    {
        private readonly BestFlexDbContext _db;
        private readonly IAccountRepository _accountRepository;
        private readonly ILogger<AccountingService> _logger;

        public AccountingService(
            BestFlexDbContext db,
            IAccountRepository accountRepository,
            ILogger<AccountingService> logger)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _accountRepository = accountRepository ?? throw new ArgumentNullException(nameof(accountRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task PostInvoiceAsync(SellingInvoice invoice, CancellationToken cancellationToken = default)
        {
            if (invoice == null) throw new ArgumentNullException(nameof(invoice));

            _logger.LogInformation("Posting accounting entries for invoice {InvoiceId}", invoice.Id);

            using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                // Check if invoice already posted
                var existingEntry = await _db.JournalEntries
                    .FirstOrDefaultAsync(j => j.ReferenceType == ReferenceType.Invoice && j.ReferenceId == invoice.Id, cancellationToken);

                if (existingEntry != null)
                {
                    throw new InvalidOperationException($"Invoice {invoice.Id} has already been posted to accounting");
                }

                // Get required accounts
                var accounts = await _accountRepository.GetRequiredAccountsAsync(cancellationToken);
                var receivablesAccount = accounts.FirstOrDefault(a => a.AccountType == AccountType.Asset && a.Code == "1200"); // Accounts Receivable
                var salesRevenueAccount = accounts.FirstOrDefault(a => a.AccountType == AccountType.Revenue && a.Code == "4000"); // Sales Revenue

                if (receivablesAccount == null)
                    throw new InvalidOperationException("Accounts Receivable account not found");
                if (salesRevenueAccount == null)
                    throw new InvalidOperationException("Sales Revenue account not found");

                // Calculate totals
                var totalAmount = invoice.SellingInvoiceItems.Sum(item => item.Quantity * item.UnitPrice);

                // Create journal entry
                var journalEntry = new JournalEntry
                {
                    EntryDate = invoice.IssuedAt,
                    ReferenceType = ReferenceType.Invoice,
                    ReferenceId = invoice.Id,
                    TotalDebit = totalAmount,
                    TotalCredit = totalAmount,
                    CreatedBy = "System"
                };

                // Create journal lines
                var journalLines = new List<JournalLine>
                {
                    new JournalLine
                    {
                        AccountId = receivablesAccount.Id,
                        Debit = totalAmount,
                        Credit = 0,
                        Description = $"Invoice {invoice.Id} - {invoice.CustomerAccountId}"
                    },
                    new JournalLine
                    {
                        AccountId = salesRevenueAccount.Id,
                        Debit = 0,
                        Credit = totalAmount,
                        Description = $"Sales Revenue - Invoice {invoice.Id}"
                    }
                };

                // Validate balanced entry
                if (!await ValidateBalancedEntry(journalLines, cancellationToken))
                {
                    throw new InvalidOperationException("Journal entry is not balanced");
                }

                // Save journal entry and lines
                _db.JournalEntries.Add(journalEntry);
                await _db.SaveChangesAsync(cancellationToken);

                journalEntry.JournalLines = journalLines;
                _db.JournalLines.AddRange(journalLines);
                await _db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Successfully posted accounting entries for invoice {InvoiceId}, total amount: {TotalAmount}", 
                    invoice.Id, totalAmount);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        public async Task<bool> ValidateBalancedEntry(IEnumerable<JournalLine> journalLines, CancellationToken cancellationToken = default)
        {
            if (journalLines == null) throw new ArgumentNullException(nameof(journalLines));

            var totalDebit = journalLines.Sum(l => l.Debit);
            var totalCredit = journalLines.Sum(l => l.Credit);

            var isBalanced = Math.Abs(totalDebit - totalCredit) < 0.01m;

            _logger.LogDebug("Validating journal entry: Debit={TotalDebit}, Credit={TotalCredit}, Balanced={IsBalanced}", 
                totalDebit, totalCredit, isBalanced);

            return isBalanced;
        }
    }
}
