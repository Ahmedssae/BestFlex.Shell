using System;
using System.Threading.Tasks;

namespace BestFlex.Shell
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("BestFlex ERP - Customer Management Demo");
            Console.WriteLine("==========================================");
            Console.WriteLine();

            try
            {
                await CustomerManagementDemo.RunDemo();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Fatal error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
