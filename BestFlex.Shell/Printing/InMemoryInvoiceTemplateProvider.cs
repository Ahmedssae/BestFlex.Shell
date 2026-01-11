using System;
using System.Collections.Generic;

namespace BestFlex.Shell.Printing
{
    public sealed class InMemoryInvoiceTemplateProvider : IInvoiceTemplateProvider
    {
        // Default FlowDocument XAML with placeholders
        private const string DefaultXaml = @"
<FlowDocument xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
              PagePadding='40' ColumnWidth='999999'
              FontFamily='Segoe UI' FontSize='12'>
  <Section>
    <Grid>
      <Grid.ColumnDefinitions>
        <ColumnDefinition Width='*'/>
        <ColumnDefinition Width='200'/>
      </Grid.ColumnDefinitions>

      <StackPanel Grid.Column='0'>
        <Paragraph FontSize='22' FontWeight='Bold' Margin='0,0,0,2'>{{Company.Name}}</Paragraph>
        <Paragraph Margin='0,0,0,8' FontSize='11' Opacity='0.85'>
          <Run>Phone: {{Company.Phone}}</Run><LineBreak/>
          <Run>Address: {{Company.Address}}</Run>
        </Paragraph>
      </StackPanel>

      <!-- Optional logo spot -->
      <Border Grid.Column='1' HorizontalAlignment='Right' Width='180' Height='70' BorderBrush='#DDD' BorderThickness='1' CornerRadius='4' Padding='6'>
        <Paragraph TextAlignment='Center' Opacity='0.6'>Logo</Paragraph>
      </Border>
    </Grid>

    <Paragraph FontSize='18' FontWeight='SemiBold' Margin='0,10,0,6'>INVOICE</Paragraph>
    <Paragraph>
      <Bold>Invoice No:</Bold> {{Invoice.Number}}    <Bold>Date:</Bold> {{Invoice.Date}}<LineBreak/>
      <Bold>Customer:</Bold> {{Customer.Name}}
    </Paragraph>

    <!-- Lines table will be injected after this section -->
  </Section>

  <Section>
    <Paragraph Margin='10,16,0,0'>
      <Bold>Subtotal:</Bold> {{Totals.Subtotal}}    <Bold>Discount %:</Bold> {{Totals.DiscountPercent}}
      <Bold>Tax %:</Bold> {{Totals.TaxPercent}}    <Bold>Grand Total:</Bold> {{Totals.GrandTotal}}
    </Paragraph>
    <Paragraph Margin='12,24,0,0' Opacity='0.7'>{{Footer.Note}}</Paragraph>
  </Section>
</FlowDocument>";

        // companyId -> template (kept in-memory for v1)
        private readonly Dictionary<int, PrintTemplate> _templates = new();

        public PrintTemplate GetTemplateForCompany(int companyId)
        {
            if (_templates.TryGetValue(companyId, out var tpl) && tpl != null)
                return tpl;

            var def = new PrintTemplate
            {
                Name = "Default",
                Engine = "FlowDocument",
                Payload = DefaultXaml,
                IsDefault = true
            };
            _templates[companyId] = def;
            return def;
        }

        // v1 setter used by the designer page (persists for the runtime session)
        public void SetTemplateForCompany(int companyId, PrintTemplate template)
        {
            if (template is null) throw new ArgumentNullException(nameof(template));
            _templates[companyId] = template;
        }
    }
}
