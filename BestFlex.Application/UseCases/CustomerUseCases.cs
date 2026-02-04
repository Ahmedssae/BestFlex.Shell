using System;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Application.UseCases
{
    public interface ICreateCustomerUseCase
    {
        Task<int> ExecuteAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default);
    }

    public class CreateCustomerCommand
    {
        public string Name { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public int PaymentTermsDays { get; set; } = 30;
    }

    public class CreateCustomerUseCase : ICreateCustomerUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateCustomerUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<int> ExecuteAsync(CreateCustomerCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Create customer (domain validation happens here)
                var customer = new Customer(command.Name, command.TaxId, command.CreditLimit, command.PaymentTermsDays);

                // In Phase 3A, we would save via repository here
                // For now, just commit the transaction to demonstrate ACID behavior
                await _unitOfWork.CommitAsync();

                return customer.Id;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface IUpdateCustomerUseCase
    {
        Task ExecuteAsync(UpdateCustomerCommand command, CancellationToken cancellationToken = default);
    }

    public class UpdateCustomerCommand
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal CreditLimit { get; set; }
        public int PaymentTermsDays { get; set; }
    }

    public class UpdateCustomerUseCase : IUpdateCustomerUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateCustomerUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(UpdateCustomerCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // In Phase 3A, we would load from repository here
                // For now, just demonstrate the transaction pattern with domain validation
                
                // Create a customer instance to test domain rules
                var customer = new Customer(command.Name, "TEST", command.CreditLimit, command.PaymentTermsDays);
                
                // Update customer (domain validation happens here)
                customer.UpdateName(command.Name);
                customer.UpdateCreditLimit(command.CreditLimit);

                // Commit transaction
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface IChangeCreditLimitUseCase
    {
        Task ExecuteAsync(ChangeCreditLimitCommand command, CancellationToken cancellationToken = default);
    }

    public class ChangeCreditLimitCommand
    {
        public int CustomerId { get; set; }
        public decimal NewCreditLimit { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class ChangeCreditLimitUseCase : IChangeCreditLimitUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public ChangeCreditLimitUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(ChangeCreditLimitCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Create a customer instance to test domain rules
                var customer = new Customer("Test", "TEST", command.NewCreditLimit, 30);
                
                // Check if credit limit is negative (domain validation happens here)
                if (command.NewCreditLimit < 0)
                    throw new DomainException("Credit limit cannot be negative");

                // Update credit limit (domain validation happens here)
                customer.UpdateCreditLimit(command.NewCreditLimit);

                // Commit transaction
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface IDeactivateCustomerUseCase
    {
        Task ExecuteAsync(DeactivateCustomerCommand command, CancellationToken cancellationToken = default);
    }

    public class DeactivateCustomerCommand
    {
        public int CustomerId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class DeactivateCustomerUseCase : IDeactivateCustomerUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeactivateCustomerUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(DeactivateCustomerCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Create a customer instance to test domain rules
                var customer = new Customer("Test", "TEST", 1000, 30);
                
                // Deactivate customer (domain validation happens here)
                customer.Deactivate();

                // Commit transaction
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }
}
