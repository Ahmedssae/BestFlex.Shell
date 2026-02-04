using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BestFlex.Shell.Data;
using BestFlex.Shell.Models;

namespace BestFlex.Shell.Repositories
{
    public class SalesOrderRepository : ISalesOrderRepository
    {
        private readonly SalesOrderDbContext _context;
        private readonly ILogger<SalesOrderRepository> _logger;

        public SalesOrderRepository(SalesOrderDbContext context, ILogger<SalesOrderRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SalesOrder?> SaveDraftAsync(SalesOrder draft)
        {
            try
            {
                // Generate unique order number
                draft.OrderNumber = await GenerateOrderNumberAsync();
                draft.CreatedAt = DateTime.UtcNow;
                
                // Calculate totals
                CalculateTotals(draft);

                _context.SalesOrders.Add(draft);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Saved draft {OrderNumber}", draft.OrderNumber);
                return draft;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save draft");
                return null;
            }
        }

        public async Task<SalesOrder?> LoadDraftAsync(int id)
        {
            try
            {
                var draft = await _context.SalesOrders
                    .Include(so => so.Lines)
                    .FirstOrDefaultAsync(so => so.Id == id);

                if (draft != null)
                {
                    _logger.LogInformation("Loaded draft {OrderNumber}", draft.OrderNumber);
                }

                return draft;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load draft {Id}", id);
                return null;
            }
        }

        public async Task<SalesOrder?> UpdateDraftAsync(SalesOrder draft)
        {
            try
            {
                var existing = await _context.SalesOrders
                    .Include(so => so.Lines)
                    .FirstOrDefaultAsync(so => so.Id == draft.Id);

                if (existing == null)
                {
                    _logger.LogWarning("Draft not found for update: {Id}", draft.Id);
                    return null;
                }

                // Update header fields
                existing.CustomerName = draft.CustomerName;
                existing.OrderDate = draft.OrderDate;
                existing.Currency = draft.Currency;
                existing.UpdatedAt = DateTime.UtcNow;

                // Remove existing lines
                _context.SalesOrderLines.RemoveRange(existing.Lines);

                // Add new lines
                foreach (var line in draft.Lines)
                {
                    line.SalesOrderId = existing.Id;
                    line.CreatedAt = DateTime.UtcNow;
                    _context.SalesOrderLines.Add(line);
                }

                // Recalculate totals
                CalculateTotals(existing);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Updated draft {OrderNumber}", existing.OrderNumber);
                return existing;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update draft {Id}", draft.Id);
                return null;
            }
        }

        public async Task<bool> DeleteDraftAsync(int id)
        {
            try
            {
                var draft = await _context.SalesOrders
                    .Include(so => so.Lines)
                    .FirstOrDefaultAsync(so => so.Id == id);

                if (draft == null)
                {
                    _logger.LogWarning("Draft not found for deletion: {Id}", id);
                    return false;
                }

                _context.SalesOrderLines.RemoveRange(draft.Lines);
                _context.SalesOrders.Remove(draft);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Deleted draft {OrderNumber}", draft.OrderNumber);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete draft {Id}", id);
                return false;
            }
        }

        public async Task<List<SalesOrder>> GetDraftsAsync()
        {
            try
            {
                var drafts = await _context.SalesOrders
                    .Include(so => so.Lines)
                    .OrderByDescending(so => so.CreatedAt)
                    .ToListAsync();

                _logger.LogInformation("Loaded {Count} drafts", drafts.Count);
                return drafts;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load drafts");
                return new List<SalesOrder>();
            }
        }

        public async Task<SalesOrder?> GetByOrderNumberAsync(string orderNumber)
        {
            try
            {
                var order = await _context.SalesOrders
                    .Include(so => so.Lines)
                    .FirstOrDefaultAsync(so => so.OrderNumber == orderNumber);

                return order;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get order by number {OrderNumber}", orderNumber);
                return null;
            }
        }

        private Task<string> GenerateOrderNumberAsync()
        {
            // Simple order number generation - in real implementation this would be more sophisticated
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            return Task.FromResult($"SO-{timestamp}");
        }

        private void CalculateTotals(SalesOrder order)
        {
            order.Subtotal = order.Lines.Sum(l => l.LineTotal);
            order.Tax = order.Subtotal * 0.0m; // Tax hardcoded to zero for Phase 4
            order.GrandTotal = order.Subtotal + order.Tax;

            // Update line totals
            foreach (var line in order.Lines)
            {
                line.LineTotal = line.Quantity * line.UnitPrice;
            }
        }
    }
}
