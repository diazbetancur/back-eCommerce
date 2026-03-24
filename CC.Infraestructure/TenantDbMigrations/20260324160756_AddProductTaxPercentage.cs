using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddProductTaxPercentage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "TaxPercentage",
                schema: "public",
                table: "Products",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TaxPercentage",
                schema: "public",
                table: "Products");
        }
    }
}
