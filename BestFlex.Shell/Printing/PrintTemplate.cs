namespace BestFlex.Shell.Printing
{
    public sealed class PrintTemplate
    {
        public string Name { get; set; } = "Default";
        // "FlowDocument" for now (QuestPDF later):
        public string Engine { get; set; } = "FlowDocument";
        // Store XAML (or JSON later); per-company override is easy to add
        public string Payload { get; set; } = "";
        public bool IsDefault { get; set; } = true;
    }
}
