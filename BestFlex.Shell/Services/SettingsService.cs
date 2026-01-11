using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using BestFlex.Application.Abstractions;

namespace BestFlex.Shell.Services
{
    public class SettingsService
    {
        private readonly string _configPath;
        public SettingsService() => _configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        public CompanySettings ReadCompany()
        {
            var root = LoadRoot();
            var company = root["Company"] as JsonObject ?? new JsonObject();
            return new CompanySettings
            {
                Name = company?["Name"]?.GetValue<string>() ?? "BestFlex",
                Address = company?["Address"]?.GetValue<string>() ?? "",
                Phone = company?["Phone"]?.GetValue<string>() ?? "",
                TaxNo = company?["TaxNo"]?.GetValue<string>() ?? "",
                LogoPath = company?["LogoPath"]?.GetValue<string>()
            };
        }

        public PrintTemplateSettings ReadPrint()
        {
            var root = LoadRoot();
            var p = root["Print"] as JsonObject ?? new JsonObject();
            return new PrintTemplateSettings
            {
                PageSize = p?["PageSize"]?.GetValue<string>() ?? "A4",
                Margin = p?["Margin"]?.GetValue<float?>() ?? 20f,
                ShowCode = p?["ShowCode"]?.GetValue<bool?>() ?? true,
                ShowName = p?["ShowName"]?.GetValue<bool?>() ?? true,
                ShowQty = p?["ShowQty"]?.GetValue<bool?>() ?? true,
                ShowUnitPrice = p?["ShowUnitPrice"]?.GetValue<bool?>() ?? true,
                ShowLineTotal = p?["ShowLineTotal"]?.GetValue<bool?>() ?? true,
                ShowDiscount = p?["ShowDiscount"]?.GetValue<bool?>() ?? false,
                DiscountPercent = p?["DiscountPercent"]?.GetValue<float?>() ?? 0f,
                ShowTax = p?["ShowTax"]?.GetValue<bool?>() ?? false,
                TaxPercent = p?["TaxPercent"]?.GetValue<float?>() ?? 0f,
                FooterNote = p?["FooterNote"]?.GetValue<string>()
            };
        }

        public void WriteCompany(CompanySettings s)
        {
            var root = LoadRoot();
            root["Company"] = new JsonObject
            {
                ["Name"] = s.Name ?? "BestFlex",
                ["Address"] = s.Address ?? "",
                ["Phone"] = s.Phone ?? "",
                ["TaxNo"] = s.TaxNo ?? "",
                ["LogoPath"] = s.LogoPath ?? ""
            };
            SaveRoot(root);
        }

        public void WritePrint(PrintTemplateSettings s)
        {
            var root = LoadRoot();
            root["Print"] = new JsonObject
            {
                ["PageSize"] = s.PageSize ?? "A4",
                ["Margin"] = s.Margin,
                ["ShowCode"] = s.ShowCode,
                ["ShowName"] = s.ShowName,
                ["ShowQty"] = s.ShowQty,
                ["ShowUnitPrice"] = s.ShowUnitPrice,
                ["ShowLineTotal"] = s.ShowLineTotal,
                ["ShowDiscount"] = s.ShowDiscount,
                ["DiscountPercent"] = s.DiscountPercent,
                ["ShowTax"] = s.ShowTax,
                ["TaxPercent"] = s.TaxPercent,
                ["FooterNote"] = s.FooterNote ?? ""
            };
            SaveRoot(root);
        }

        private JsonObject LoadRoot()
        {
            if (!File.Exists(_configPath)) return new JsonObject();
            var json = File.ReadAllText(_configPath);
            return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
        }
        private void SaveRoot(JsonObject root)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_configPath, root.ToJsonString(options));
        }
    }
}
