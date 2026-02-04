using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BestFlex.Application.Abstractions;
using BestFlex.Domain.Entities;
using BestFlex.Domain.Exceptions;

namespace BestFlex.Application.UseCases
{
    public interface ICreateProductUseCase
    {
        Task<int> ExecuteAsync(CreateProductCommand command, CancellationToken cancellationToken = default);
    }

    public class CreateProductCommand
    {
        public string SKU { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public decimal Price { get; set; }
    }

    public class CreateProductUseCase : ICreateProductUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public CreateProductUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task<int> ExecuteAsync(CreateProductCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Validate price not below cost
                if (command.Price < command.Cost)
                    throw new DomainException("Selling price cannot be below cost");

                // In Phase 3B, we would create product via repository here
                // For now, just commit the transaction to demonstrate ACID behavior
                await _unitOfWork.CommitAsync();

                // Return dummy ID for demonstration
                return 1;
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface IUpdateProductUseCase
    {
        Task ExecuteAsync(UpdateProductCommand command, CancellationToken cancellationToken = default);
    }

    public class UpdateProductCommand
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Cost { get; set; }
        public decimal Price { get; set; }
    }

    public class UpdateProductUseCase : IUpdateProductUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public UpdateProductUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(UpdateProductCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Validate price not below cost
                if (command.Price < command.Cost)
                    throw new DomainException("Selling price cannot be below cost");

                // In Phase 3B, we would update product via repository here
                // For now, just commit the transaction to demonstrate ACID behavior
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface IAddPriceTierUseCase
    {
        Task ExecuteAsync(AddPriceTierCommand command, CancellationToken cancellationToken = default);
    }

    public class AddPriceTierCommand
    {
        public int ProductId { get; set; }
        public decimal QuantityFrom { get; set; }
        public decimal QuantityTo { get; set; }
        public decimal Price { get; set; }
        public string Currency { get; set; } = "USD";
    }

    public class AddPriceTierUseCase : IAddPriceTierUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public AddPriceTierUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(AddPriceTierCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // Validate quantity range
                if (command.QuantityFrom >= command.QuantityTo)
                    throw new DomainException("QuantityFrom must be less than QuantityTo");

                // Validate price not negative
                if (command.Price < 0)
                    throw new DomainException("Price cannot be negative");

                // In Phase 3B, we would add price tier via repository here
                // For now, just commit the transaction to demonstrate ACID behavior
                await _unitOfWork.CommitAsync();
            }
            catch
            {
                await _unitOfWork.RollbackAsync();
                throw;
            }
        }
    }

    public interface IDeactivateProductUseCase
    {
        Task ExecuteAsync(DeactivateProductCommand command, CancellationToken cancellationToken = default);
    }

    public class DeactivateProductCommand
    {
        public int ProductId { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class DeactivateProductUseCase : IDeactivateProductUseCase
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeactivateProductUseCase(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        public async Task ExecuteAsync(DeactivateProductCommand command, CancellationToken cancellationToken = default)
        {
            await _unitOfWork.BeginAsync();

            try
            {
                // In Phase 3B, we would deactivate product via repository here
                // For now, just commit the transaction to demonstrate ACID behavior
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
