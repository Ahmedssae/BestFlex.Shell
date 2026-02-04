using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using BestFlex.Domain.Entities;
using BestFlex.Persistence.Data;

namespace BestFlex.Persistence.Repositories
{
    public interface ICustomerRepository
    {
        Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Customer?> GetByTaxIdAsync(string taxId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<Customer>> GetActiveAsync(CancellationToken cancellationToken = default);
        Task<bool> TaxIdExistsAsync(string taxId, int? excludeId = null, CancellationToken cancellationToken = default);
        Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
        Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);
        Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default);
    }

    public class CustomerRepository : ICustomerRepository
    {
        private readonly BestFlexDbContext _context;

        public CustomerRepository(BestFlexDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            // Map to existing CustomerAccount entity for now
            var customerAccount = await _context.CustomerAccounts
                .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
            
            if (customerAccount == null)
                return null;

            return MapToCustomer(customerAccount);
        }

        public async Task<Customer?> GetByTaxIdAsync(string taxId, CancellationToken cancellationToken = default)
        {
            var customerAccount = await _context.CustomerAccounts
                .FirstOrDefaultAsync(c => c.Name == taxId, cancellationToken); // Using Name field as TaxId for now
            
            if (customerAccount == null)
                return null;

            return MapToCustomer(customerAccount);
        }

        public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var customerAccounts = await _context.CustomerAccounts
                .ToListAsync(cancellationToken);

            return customerAccounts.Select(MapToCustomer).ToList();
        }

        public async Task<IReadOnlyList<Customer>> GetActiveAsync(CancellationToken cancellationToken = default)
        {
            var customerAccounts = await _context.CustomerAccounts
                .Where(c => c.Balance >= 0) // Using Balance as active indicator for now
                .ToListAsync(cancellationToken);

            return customerAccounts.Select(MapToCustomer).ToList();
        }

        public async Task<bool> TaxIdExistsAsync(string taxId, int? excludeId = null, CancellationToken cancellationToken = default)
        {
            var query = _context.CustomerAccounts.Where(c => c.Name == taxId);
            
            if (excludeId.HasValue)
                query = query.Where(c => c.Id != excludeId.Value);

            return await query.AnyAsync(cancellationToken);
        }

        public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            var customerAccount = MapToCustomerAccount(customer);
            await _context.CustomerAccounts.AddAsync(customerAccount, cancellationToken);
        }

        public async Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            var customerAccount = MapToCustomerAccount(customer);
            _context.CustomerAccounts.Update(customerAccount);
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            var customerAccount = MapToCustomerAccount(customer);
            _context.CustomerAccounts.Remove(customerAccount);
            await Task.CompletedTask;
        }

        private static Customer MapToCustomer(CustomerAccount account)
        {
            return new Customer(account.Name, account.Name, account.Balance, 30) // Simplified mapping
            {
                // Note: In real implementation, we'd need to set the private Id field
                // For now, this creates a new customer instance
            };
        }

        private static CustomerAccount MapToCustomerAccount(Customer customer)
        {
            return new CustomerAccount
            {
                Id = customer.Id,
                Name = customer.Name,
                Balance = customer.CreditLimit, // Using Balance as CreditLimit for now
                Phone = customer.TaxId // Using Phone as TaxId for now
            };
        }
    }
}
