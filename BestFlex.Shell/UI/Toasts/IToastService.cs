namespace BestFlex.Shell.UI.Toasts
{
    public interface IToastService
    {
        void Show(string message, int milliseconds = 2200);
    }
}
