namespace BestFlex.Application.Abstractions;

public class InvoicePrintData
{
    public string CompanyName { get; set; } = "BestFlex";
    public string CompanyAddress { get; set; } = "";
    public string CompanyPhone { get; set; } = "";
    public string CompanyTaxNo { get; set; } = "";

    public string InvoiceNo { get; set; } = "";
    public DateTime IssuedAt { get; set; }
    public string Currency { get; set; } = "USD";

    public string CustomerName { get; set; } = "";
    public string CustomerAddress { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string? Description { get; set; }

    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public string? CompanyLogoPath { get; set; }   // <-- add this
                                                   // Template options
    public string PageSize { get; set; } = "A4";
    public float Margin { get; set; } = 20f;
    public bool ShowCode { get; set; } = true;
    public bool ShowName { get; set; } = true;
    public bool ShowQty { get; set; } = true;
    public bool ShowUnitPrice { get; set; } = true;
    public bool ShowLineTotal { get; set; } = true;
    public string? FooterNote { get; set; }

    // --- Totals breakdown (optional; 0 means "not used") ---
    public decimal DiscountAmount { get; set; }   // e.g., 12.50
    public float DiscountPercent { get; set; }  // e.g., 10 => 10%
    public decimal TaxAmount { get; set; }        // e.g., 2.50
    public float TaxPercent { get; set; }       // e.g., 5 => 5%

    public List<InvoicePrintLine> Lines { get; } = new();
}

public class InvoicePrintLine
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Qty { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
