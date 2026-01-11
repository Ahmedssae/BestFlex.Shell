using System.Text;
using BestFlex.Shell.Printing;

namespace BestFlex.Shell.Printing
{
    /// <summary>
    /// Minimal provider: returns a sane FlowDocument-XAML payload the engine can parse.
    /// Safe fallback until you wire DB-backed templates.
    /// </summary>
    public sealed class DefaultInvoiceTemplateProvider : IInvoiceTemplateProvider
    {
        public PrintTemplate GetTemplateForCompany(int companyId)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<FlowDocument xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" PagePadding=\"40\" FontFamily=\"Segoe UI\" FontSize=\"12\">");
            sb.AppendLine("  <Paragraph FontSize=\"20\" FontWeight=\"SemiBold\">Invoice</Paragraph>");
            sb.AppendLine("  <Paragraph>Company: {{Company.Name}}</Paragraph>");
            sb.AppendLine("  <Paragraph>Phone: {{Company.Phone}}</Paragraph>");
            sb.AppendLine("  <Paragraph>Address: {{Company.Address}}</Paragraph>");
            sb.AppendLine("  <Paragraph>Customer: {{Customer.Name}}</Paragraph>");
            sb.AppendLine("  <Paragraph>Number: {{Invoice.Number}}   Date: {{Invoice.Date}}</Paragraph>");
            sb.AppendLine("  <Paragraph/>");
            sb.AppendLine("  <Paragraph FontSize=\"12\">Subtotal: {{Totals.Subtotal}}  |  Discount %: {{Totals.DiscountPercent}}  |  Tax %: {{Totals.TaxPercent}}  |  Grand Total: {{Totals.GrandTotal}}</Paragraph>");
            sb.AppendLine("  <Paragraph>{{Footer.Note}}</Paragraph>");
            sb.AppendLine("</FlowDocument>");

            return new PrintTemplate
            {
                Name = "Default",
                Engine = "FlowDocument",
                Payload = sb.ToString(),
                IsDefault = true
            };
        }
    }
}
