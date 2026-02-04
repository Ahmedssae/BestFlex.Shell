using System.Collections.Generic;
using System.Threading.Tasks;

namespace BestFlex.Shell.Services
{
    public interface IInventoryReadService
    {
        Task<InventoryInfo> GetInventoryInfoAsync(int productId);
        Task<List<InventoryInfo>> GetInventoryInfoAsync(List<int> productIds);
    }

    public class InventoryInfo
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int AvailableQuantity { get; set; }
        public bool IsAvailable { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
