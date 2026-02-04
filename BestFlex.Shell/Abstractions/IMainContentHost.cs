namespace BestFlex.Shell.Abstractions
{
    /// <summary>
    /// Single content host contract for deterministic navigation
    /// </summary>
    public interface IMainContentHost
    {
        void Show(object view);
    }
}
