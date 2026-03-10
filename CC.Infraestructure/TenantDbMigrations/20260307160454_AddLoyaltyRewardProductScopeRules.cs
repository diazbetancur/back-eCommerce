using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyRewardProductScopeRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AppliesToAllEligibleProducts",
                schema: "public",
                table: "LoyaltyRewards",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SingleProductSelectionRule",
                schema: "public",
                table: "LoyaltyRewards",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoyaltyRewardProducts",
                schema: "public",
                columns: table => new
                {
                    RewardId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyRewardProducts", x => new { x.RewardId, x.ProductId });
                    table.ForeignKey(
                        name: "FK_LoyaltyRewardProducts_LoyaltyRewards_RewardId",
                        column: x => x.RewardId,
                        principalSchema: "public",
                        principalTable: "LoyaltyRewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewards_AppliesToAllEligibleProducts",
                schema: "public",
                table: "LoyaltyRewards",
                column: "AppliesToAllEligibleProducts");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewardProducts_ProductId",
                schema: "public",
                table: "LoyaltyRewardProducts",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewardProducts_RewardId",
                schema: "public",
                table: "LoyaltyRewardProducts",
                column: "RewardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyRewardProducts",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_LoyaltyRewards_AppliesToAllEligibleProducts",
                schema: "public",
                table: "LoyaltyRewards");

            migrationBuilder.DropColumn(
                name: "AppliesToAllEligibleProducts",
                schema: "public",
                table: "LoyaltyRewards");

            migrationBuilder.DropColumn(
                name: "SingleProductSelectionRule",
                schema: "public",
                table: "LoyaltyRewards");
        }
    }
}
