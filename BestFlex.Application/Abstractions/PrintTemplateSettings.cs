namespace BestFlex.Application.Abstractions;

public class PrintTemplateSettings
{
    public string PageSize { get; set; } = "A4";   // "A4" or "A5"
    public float Margin { get; set; } = 20f;    // points (QuestPDF) / pixels-ish for WPF

    public bool ShowCode { get; set; } = true;
    public bool ShowName { get; set; } = true;
    public bool ShowQty { get; set; } = true;
    public bool ShowUnitPrice { get; set; } = true;
    public bool ShowLineTotal { get; set; } = true;

    public bool ShowDiscount { get; set; } = false;
    public float DiscountPercent { get; set; } = 0f;
    public bool ShowTax { get; set; } = false;
    public float TaxPercent { get; set; } = 0f;

    public string? FooterNote { get; set; }
}
