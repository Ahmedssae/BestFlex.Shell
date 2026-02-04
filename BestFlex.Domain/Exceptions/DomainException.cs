using System;

namespace BestFlex.Domain.Exceptions
{
    public class DomainException : Exception
    {
        public DomainException(string message) : base(message) { }
        public DomainException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class BusinessRuleViolationException : DomainException
    {
        public BusinessRuleViolationException(string message) : base(message) { }
        public BusinessRuleViolationException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class InsufficientStockException : BusinessRuleViolationException
    {
        public InsufficientStockException(string message) : base(message) { }
    }

    public class CreditLimitExceededException : BusinessRuleViolationException
    {
        public CreditLimitExceededException(string message) : base(message) { }
    }

    public class PeriodClosedException : BusinessRuleViolationException
    {
        public PeriodClosedException(string message) : base(message) { }
    }

    public class InvoiceAlreadyPostedException : BusinessRuleViolationException
    {
        public InvoiceAlreadyPostedException(string message) : base(message) { }
    }

    public class UnbalancedJournalEntryException : BusinessRuleViolationException
    {
        public UnbalancedJournalEntryException(string message) : base(message) { }
    }
}
