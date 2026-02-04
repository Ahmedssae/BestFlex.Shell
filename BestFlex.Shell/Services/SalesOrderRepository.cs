using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;

namespace BestFlex.Shell.Services
{
    public class SalesOrderRepository : ISalesOrderRepository
    {
        private readonly BestFlexDbContext _context;
        private readonly ILogger<SalesOrderRepository> _logger;

        public SalesOrderRepository(BestFlexDbContext context, ILogger<SalesOrderRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<SalesOrder?> SaveDraftAsync(SalesOrder draft)
        {
            try
            {
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
                    .FirstOrDefaultAsync(so => so.Id == id && so.Status == SalesOrderStatus.Draft);

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
                    .FirstOrDefaultAsync(so => so.Id == draft.Id && so.Status == SalesOrderStatus.Draft);

                if (existing == null)
                {
                    _logger.LogWarning("Draft not found for update: {Id}", draft.Id);
                    return null;
                }

                // Update the entity - this is simplified since the domain entity has private setters
                // In a real implementation, you'd use domain methods or reflection
                _context.Entry(existing).CurrentValues.SetValues(draft);
                
                // Handle lines - remove existing and add new
                _context.SalesOrderLines.RemoveRange(existing.Lines);
                
                foreach (var line in draft.Lines)
                {
                    _context.SalesOrderLines.Add(line);
                }

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
                    .FirstOrDefaultAsync(so => so.Id == id && so.Status == SalesOrderStatus.Draft);

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
                    .Where(so => so.Status == SalesOrderStatus.Draft)
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
    }
}
