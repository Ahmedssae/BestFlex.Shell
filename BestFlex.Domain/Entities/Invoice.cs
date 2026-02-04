using System;
using System.Collections.Generic;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Domain.Entities
{
    public class Invoice
    {
        public int Id { get; private set; }
        public int SalesOrderId { get; private set; }
        public string InvoiceNumber { get; private set; }
        public DateTime InvoiceDate { get; private set; }
        public DateTime DueDate { get; private set; }
        public InvoiceStatus Status { get; private set; }
        public decimal Subtotal { get; private set; }
        public decimal TaxAmount { get; private set; }
        public decimal TotalAmount => Subtotal + TaxAmount;
        public string Currency { get; private set; }
        public string Notes { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? PostedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        private readonly List<InvoiceLine> _lines = new();
        public IReadOnlyCollection<InvoiceLine> Lines => _lines.AsReadOnly();

        protected Invoice() 
        { 
            InvoiceNumber = string.Empty;
            Currency = "USD";
            Notes = string.Empty;
            Status = InvoiceStatus.Draft;
            CreatedAt = DateTime.UtcNow;
        }

        public Invoice(int salesOrderId, string invoiceNumber, DateTime invoiceDate, DateTime dueDate, string currency = "USD")
        {
            if (salesOrderId <= 0)
                throw new DomainException("Valid sales order ID is required");

            if (string.IsNullOrWhiteSpace(invoiceNumber))
                throw new DomainException("Invoice number is required");

            SalesOrderId = salesOrderId;
            InvoiceNumber = invoiceNumber;
            InvoiceDate = invoiceDate;
            DueDate = dueDate;
            Status = InvoiceStatus.Draft;
            Subtotal = 0;
            TaxAmount = 0;
            Currency = currency;
            Notes = string.Empty;
            CreatedAt = DateTime.UtcNow;
        }

        public void AddLine(int productId, string productDescription, decimal quantity, decimal unitPrice, decimal taxRate)
        {
            if (productId <= 0)
                throw new DomainException("Valid product ID is required");

            if (quantity <= 0)
                throw new DomainException("Quantity must be positive");

            if (unitPrice < 0)
                throw new DomainException("Unit price cannot be negative");

            if (Status != InvoiceStatus.Draft)
                throw new InvoiceAlreadyPostedException("Cannot modify posted invoices");

            var line = new InvoiceLine(Id, productId, productDescription, quantity, unitPrice, taxRate);
            _lines.Add(line);
            RecalculateTotals();
            UpdatedAt = DateTime.UtcNow;
        }

        public void RemoveLine(int lineId)
        {
            if (Status != InvoiceStatus.Draft)
                throw new InvoiceAlreadyPostedException("Cannot modify posted invoices");

            var line = _lines.Find(l => l.Id == lineId);
            if (line != null)
            {
                _lines.Remove(line);
                RecalculateTotals();
                UpdatedAt = DateTime.UtcNow;
            }
        }

        public void UpdateNotes(string notes)
        {
            if (Status != InvoiceStatus.Draft)
                throw new InvoiceAlreadyPostedException("Cannot modify posted invoices");

            Notes = notes ?? string.Empty;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Post()
        {
            if (Status != InvoiceStatus.Draft)
                throw new InvoiceAlreadyPostedException("Invoice is already posted");

            if (!_lines.Any())
                throw new DomainException("Invoice must have at least one line to be posted");

            Status = InvoiceStatus.Posted;
            PostedAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        public void Void()
        {
            if (Status == InvoiceStatus.Void)
                throw new DomainException("Invoice is already void");

            Status = InvoiceStatus.Void;
            UpdatedAt = DateTime.UtcNow;
        }

        private void RecalculateTotals()
        {
            Subtotal = _lines.Sum(l => l.LineTotal);
            TaxAmount = _lines.Sum(l => l.TaxAmount);
        }
    }

    public class InvoiceLine
    {
        public int Id { get; private set; }
        public int InvoiceId { get; private set; }
        public int ProductId { get; private set; }
        public string ProductDescription { get; private set; }
        public decimal Quantity { get; private set; }
        public decimal UnitPrice { get; private set; }
        public decimal TaxRate { get; private set; }
        public decimal LineTotal => Quantity * UnitPrice;
        public decimal TaxAmount => LineTotal * TaxRate;
        public decimal TotalWithTax => LineTotal + TaxAmount;

        protected InvoiceLine() 
        { 
            ProductDescription = string.Empty;
        }

        public InvoiceLine(int invoiceId, int productId, string productDescription, decimal quantity, decimal unitPrice, decimal taxRate)
        {
            InvoiceId = invoiceId;
            ProductId = productId;
            ProductDescription = productDescription ?? string.Empty;
            Quantity = quantity;
            UnitPrice = unitPrice;
            TaxRate = taxRate;
        }
    }

    public enum InvoiceStatus
    {
        Draft = 1,
        Posted = 2,
        Paid = 3,
        Void = 4,
        Overdue = 5
    }
}
