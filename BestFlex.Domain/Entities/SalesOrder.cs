using System;
using System.Collections.Generic;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Domain.Entities
{
    public class SalesOrder
    {
        public int Id { get; private set; }
        public int CustomerId { get; private set; }
        public string OrderNumber { get; private set; }
        public DateTime OrderDate { get; private set; }
        public SalesOrderStatus Status { get; private set; }
        public decimal TotalAmount { get; private set; }
        public decimal TaxAmount { get; private set; }
        public decimal TotalAmountWithTax => TotalAmount + TaxAmount;
        public string Notes { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }
        public int? InvoiceId { get; private set; }

        private readonly List<SalesOrderLine> _lines = new();
        public IReadOnlyCollection<SalesOrderLine> Lines => _lines.AsReadOnly();

        protected SalesOrder() 
        { 
            OrderNumber = string.Empty;
            Notes = string.Empty;
            Status = SalesOrderStatus.Draft;
            CreatedAt = DateTime.UtcNow;
        }

        public SalesOrder(int customerId, string orderNumber, DateTime orderDate)
        {
            if (customerId <= 0)
                throw new DomainException("Valid customer ID is required");

            if (string.IsNullOrWhiteSpace(orderNumber))
                throw new DomainException("Order number is required");

            CustomerId = customerId;
            OrderNumber = orderNumber;
            OrderDate = orderDate;
            Status = SalesOrderStatus.Draft;
            TotalAmount = 0;
            TaxAmount = 0;
            Notes = string.Empty;
            CreatedAt = DateTime.UtcNow;
        }

        public void AddLine(int productId, decimal quantity, decimal unitPrice, decimal discount = 0)
        {
            if (productId <= 0)
                throw new DomainException("Valid product ID is required");

            if (quantity <= 0)
                throw new DomainException("Quantity must be positive");

            if (unitPrice < 0)
                throw new DomainException("Unit price cannot be negative");

            if (discount < 0 || discount > 100)
                throw new DomainException("Discount must be between 0 and 100");

            var line = new SalesOrderLine(Id, productId, quantity, unitPrice, discount);
            _lines.Add(line);
            RecalculateTotals();
            UpdatedAt = DateTime.UtcNow;
        }

        public void RemoveLine(int lineId)
        {
            var line = _lines.Find(l => l.Id == lineId);
            if (line != null)
            {
                _lines.Remove(line);
                RecalculateTotals();
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void UpdateLineQuantity(int lineId, decimal quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive");

            var line = _lines.Find(l => l.Id == lineId);
            if (line != null)
            {
                line.UpdateQuantity(quantity);
                RecalculateTotals();
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void UpdateLinePrice(int lineId, decimal unitPrice)
        {
            if (unitPrice < 0)
                throw new DomainException("Unit price cannot be negative");

            var line = _lines.Find(l => l.Id == lineId);
            if (line != null)
            {
                line.UpdateUnitPrice(unitPrice);
                RecalculateTotals();
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void UpdateLineDiscount(int lineId, decimal discount)
        {
            if (discount < 0 || discount > 100)
                throw new DomainException("Discount must be between 0 and 100");

            var line = _lines.Find(l => l.Id == lineId);
            if (line != null)
            {
                line.UpdateDiscount(discount);
                RecalculateTotals();
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void UpdateNotes(string notes)
        {
            Notes = notes ?? string.Empty;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Confirm()
        {
            if (Status != SalesOrderStatus.Draft)
                throw new DomainException("Only draft orders can be confirmed");

            if (!_lines.Any())
                throw new DomainException("Order must have at least one line to be confirmed");

            Status = SalesOrderStatus.Confirmed;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Post(DateTime postingDate, string orderNumber)
        {
            if (Status != SalesOrderStatus.Draft)
                throw new DomainException("Only draft orders can be posted");

            if (!_lines.Any())
                throw new DomainException("Order must have at least one line to be posted");

            if (string.IsNullOrWhiteSpace(orderNumber))
                throw new DomainException("Order number is required for posting");

            // Phase 5: Posting is irreversible - order becomes immutable
            Status = SalesOrderStatus.Confirmed; // Posted orders are marked as Confirmed
            OrderNumber = orderNumber;
            UpdatedAt = postingDate;
        }

        public void Cancel()
        {
            if (Status == SalesOrderStatus.Invoiced)
                throw new DomainException("Cannot cancel invoiced orders");

            Status = SalesOrderStatus.Cancelled;
            UpdatedAt = DateTime.UtcNow;
        }

        public void LinkToInvoice(int invoiceId)
        {
            if (Status != SalesOrderStatus.Confirmed)
                throw new DomainException("Only confirmed orders can be invoiced");

            InvoiceId = invoiceId;
            Status = SalesOrderStatus.Invoiced;
            UpdatedAt = DateTime.UtcNow;
        }

        private void RecalculateTotals()
        {
            TotalAmount = _lines.Sum(l => l.LineTotal);
            TaxAmount = TotalAmount * 0.1m; // Simplified 10% tax
        }

        public void ClearLines()
        {
            _lines.Clear();
            RecalculateTotals();
        }
    }

    public class SalesOrderLine
    {
        public int Id { get; private set; }
        public int SalesOrderId { get; private set; }
        public int ProductId { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal Discount { get; private set; }
        public decimal LineTotal => (Quantity * UnitPrice) * (1 - Discount / 100);

        protected SalesOrderLine() { }

        public SalesOrderLine(int salesOrderId, int productId, decimal quantity, decimal unitPrice, decimal discount)
        {
            SalesOrderId = salesOrderId;
            ProductId = productId;
            Quantity = quantity;
            UnitPrice = unitPrice;
            Discount = discount;
        }

        public void UpdateQuantity(decimal quantity)
        {
            if (quantity <= 0)
                throw new DomainException("Quantity must be positive");

            Quantity = quantity;
        }

        public void UpdateUnitPrice(decimal unitPrice)
        {
            if (unitPrice < 0)
                throw new DomainException("Unit price cannot be negative");

            UnitPrice = unitPrice;
        }

        public void UpdateDiscount(decimal discount)
        {
            if (discount < 0 || discount > 100)
                throw new DomainException("Discount must be between 0 and 100");

            Discount = discount;
        }
    }

    public enum SalesOrderStatus
    {
        Draft = 1,
        Confirmed = 2,
        Invoiced = 3,
        Cancelled = 4
    }
}
