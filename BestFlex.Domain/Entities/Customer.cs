using System;
using System.Collections.Generic;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Domain.Entities
{
    public class Customer
    {
        public int Id { get; private set; }
        public string Name { get; private set; }
        public string TaxId { get; private set; }
        public decimal CreditLimit { get; private set; }
        public int PaymentTermsDays { get; private set; }
        public CustomerStatus Status { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        private readonly List<CustomerAddress> _addresses = new();
        public IReadOnlyCollection<CustomerAddress> Addresses => _addresses.AsReadOnly();

        private readonly List<SalesOrder> _salesOrders = new();
        public IReadOnlyCollection<SalesOrder> SalesOrders => _salesOrders.AsReadOnly();

        protected Customer() 
        { 
            Name = string.Empty;
            TaxId = string.Empty;
            Status = CustomerStatus.Active;
            CreatedAt = DateTime.UtcNow;
        }

        public Customer(string name, string taxId, decimal creditLimit, int paymentTermsDays)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Customer name is required");
            
            if (creditLimit < 0)
                throw new DomainException("Credit limit cannot be negative");

            Name = name;
            TaxId = taxId;
            CreditLimit = creditLimit;
            PaymentTermsDays = paymentTermsDays;
            Status = CustomerStatus.Active;
            CreatedAt = DateTime.UtcNow;
        }

        public void UpdateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new DomainException("Customer name is required");
            
            Name = name;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateCreditLimit(decimal creditLimit)
        {
            if (creditLimit < 0)
                throw new DomainException("Credit limit cannot be negative");

            CreditLimit = creditLimit;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Deactivate()
        {
            Status = CustomerStatus.Inactive;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Activate()
        {
            Status = CustomerStatus.Active;
            UpdatedAt = DateTime.UtcNow;
        }

        public void AddAddress(CustomerAddress address)
        {
            if (address == null)
                throw new DomainException("Address is required");

            _addresses.Add(address);
            UpdatedAt = DateTime.UtcNow;
        }

        public void RemoveAddress(int addressId)
        {
            var address = _addresses.Find(a => a.Id == addressId);
            if (address != null)
            {
                _addresses.Remove(address);
                UpdatedAt = DateTime.UtcNow;
            }
        }
    }

    public class CustomerAddress
    {
        public int Id { get; private set; }
        public int CustomerId { get; private set; }
        public AddressType Type { get; private set; }
        public string AddressLine1 { get; private set; }
        public string? AddressLine2 { get; private set; }
        public string City { get; private set; }
        public string? State { get; private set; }
        public string PostalCode { get; private set; }
        public string Country { get; private set; }
        public bool IsDefault { get; private set; }

        protected CustomerAddress() 
        { 
            AddressLine1 = string.Empty;
            City = string.Empty;
            PostalCode = string.Empty;
            Country = string.Empty;
        }

        public CustomerAddress(int customerId, AddressType type, string addressLine1, string city, string postalCode, string country, bool isDefault = false)
        {
            CustomerId = customerId;
            Type = type;
            AddressLine1 = addressLine1 ?? throw new DomainException("Address line 1 is required");
            City = city ?? throw new DomainException("City is required");
            PostalCode = postalCode ?? throw new DomainException("Postal code is required");
            Country = country ?? throw new DomainException("Country is required");
            IsDefault = isDefault;
        }

        public void SetAsDefault()
        {
            IsDefault = true;
        }

        public void RemoveDefault()
        {
            IsDefault = false;
        }
    }

    public enum CustomerStatus
    {
        Active = 1,
        Inactive = 2,
        Suspended = 3
    }

    public enum AddressType
    {
        Billing = 1,
        Shipping = 2,
        Both = 3
    }
}
