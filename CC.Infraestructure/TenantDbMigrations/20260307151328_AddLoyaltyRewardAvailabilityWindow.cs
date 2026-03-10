using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyRewardAvailabilityWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableFrom",
                schema: "public",
                table: "LoyaltyRewards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvailableUntil",
                schema: "public",
                table: "LoyaltyRewards",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewards_AvailableFrom",
                schema: "public",
                table: "LoyaltyRewards",
                column: "AvailableFrom");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewards_AvailableUntil",
                schema: "public",
                table: "LoyaltyRewards",
                column: "AvailableUntil");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LoyaltyRewards_AvailableFrom",
                schema: "public",
                table: "LoyaltyRewards");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyRewards_AvailableUntil",
                schema: "public",
                table: "LoyaltyRewards");

            migrationBuilder.DropColumn(
                name: "AvailableFrom",
                schema: "public",
                table: "LoyaltyRewards");

            migrationBuilder.DropColumn(
                name: "AvailableUntil",
                schema: "public",
                table: "LoyaltyRewards");
        }
    }
}
