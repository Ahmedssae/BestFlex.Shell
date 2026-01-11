namespace BestFlex.Application.Abstractions;

public class CompanySettings
{
    public string Name { get; set; } = "BestFlex";
    public string Address { get; set; } = "";
    public string Phone { get; set; } = "";
    public string TaxNo { get; set; } = "";
    // Optional: absolute or relative path to a PNG/JPG on disk
    public string? LogoPath { get; set; }
}
