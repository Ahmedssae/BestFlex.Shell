using System;
using System.Threading.Tasks;

namespace BestFlex.Shell.Services
{
    public interface IPostingService
    {
        Task<PostingResult> PostOrderAsync(int salesOrderId);
    }

    public class PostingValidationException : Exception
    {
        public PostingValidationException(string message) : base(message) { }
        public PostingValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
}
