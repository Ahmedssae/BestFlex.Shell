using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BestFlex.Domain.Entities;

namespace BestFlex.Shell.Services
{
    public interface ISalesOrderRepository
    {
        Task<SalesOrder?> SaveDraftAsync(SalesOrder draft);
        Task<SalesOrder?> LoadDraftAsync(int id);
        Task<SalesOrder?> UpdateDraftAsync(SalesOrder draft);
        Task<bool> DeleteDraftAsync(int id);
        Task<List<SalesOrder>> GetDraftsAsync();
        Task<SalesOrder?> GetByOrderNumberAsync(string orderNumber);
    }
}
