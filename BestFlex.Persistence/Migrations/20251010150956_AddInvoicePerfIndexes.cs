using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BestFlex.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePerfIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InvoiceNoSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompanyId = table.Column<int>(type: "INTEGER", nullable: false),
                    YearMonth = table.Column<string>(type: "TEXT", maxLength: 6, nullable: false),
                    Next = table.Column<int>(type: "INTEGER", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceNoSequences", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SellingInvoices_IssuedAt",
                table: "SellingInvoices",
                column: "IssuedAt");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceNoSequences_CompanyId_YearMonth",
                table: "InvoiceNoSequences",
                columns: new[] { "CompanyId", "YearMonth" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InvoiceNoSequences");

            migrationBuilder.DropIndex(
                name: "IX_SellingInvoices_IssuedAt",
                table: "SellingInvoices");
        }
    }
}
