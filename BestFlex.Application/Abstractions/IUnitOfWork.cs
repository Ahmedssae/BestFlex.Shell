using System;
using System.Threading.Tasks;

namespace BestFlex.Application.Abstractions
{
    /// <summary>
    /// Unit of Work interface for managing database transactions
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        /// <summary>
        /// Begins a new database transaction
        /// </summary>
        Task BeginAsync();

        /// <summary>
        /// Commits the current transaction
        /// </summary>
        Task CommitAsync();

        /// <summary>
        /// Rolls back the current transaction
        /// </summary>
        Task RollbackAsync();
    }

    /// <summary>
    /// Transactional command interface for wrapping operations in transactions
    /// </summary>
    public interface ITransactionalCommand
    {
        /// <summary>
        /// Executes the command within a transaction
        /// </summary>
        /// <param name="command">The command to execute</param>
        /// <returns>Task representing the operation</returns>
        Task<object?> ExecuteAsync(object command);
    }
}
