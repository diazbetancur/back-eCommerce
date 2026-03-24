using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddProductSaleAndTaxFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsOnSale",
                schema: "public",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTaxIncluded",
                schema: "public",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsOnSale",
                schema: "public",
                table: "Products",
                column: "IsOnSale");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsTaxIncluded",
                schema: "public",
                table: "Products",
                column: "IsTaxIncluded");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_IsOnSale",
                schema: "public",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsTaxIncluded",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsOnSale",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsTaxIncluded",
                schema: "public",
                table: "Products");
        }
    }
}
