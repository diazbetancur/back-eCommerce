using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "ConversionRateUsed",
                schema: "public",
                table: "LoyaltyTransactions",
                type: "numeric(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiresAt",
                schema: "public",
                table: "LoyaltyTransactions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConversionRateUsed",
                schema: "public",
                table: "LoyaltyTransactions");

            migrationBuilder.DropColumn(
                name: "ExpiresAt",
                schema: "public",
                table: "LoyaltyTransactions");
        }
    }
}
