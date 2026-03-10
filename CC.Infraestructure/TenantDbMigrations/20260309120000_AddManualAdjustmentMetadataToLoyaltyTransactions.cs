using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
  public partial class AddManualAdjustmentMetadataToLoyaltyTransactions : Migration
  {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.AddColumn<Guid>(
          name: "AdjustedByUserId",
          table: "LoyaltyTransactions",
          type: "uuid",
          nullable: true);

      migrationBuilder.AddColumn<string>(
          name: "AdjustmentTicketNumber",
          table: "LoyaltyTransactions",
          type: "character varying(100)",
          maxLength: 100,
          nullable: true);

      migrationBuilder.CreateIndex(
          name: "IX_LoyaltyTransactions_AdjustedByUserId",
          table: "LoyaltyTransactions",
          column: "AdjustedByUserId");

      migrationBuilder.CreateIndex(
          name: "IX_LoyaltyTransactions_AdjustmentTicketNumber",
          table: "LoyaltyTransactions",
          column: "AdjustmentTicketNumber");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "IX_LoyaltyTransactions_AdjustedByUserId",
          table: "LoyaltyTransactions");

      migrationBuilder.DropIndex(
          name: "IX_LoyaltyTransactions_AdjustmentTicketNumber",
          table: "LoyaltyTransactions");

      migrationBuilder.DropColumn(
          name: "AdjustedByUserId",
          table: "LoyaltyTransactions");

      migrationBuilder.DropColumn(
          name: "AdjustmentTicketNumber",
          table: "LoyaltyTransactions");
    }
  }
}
