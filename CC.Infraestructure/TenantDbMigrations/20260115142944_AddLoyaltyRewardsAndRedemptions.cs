using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddLoyaltyRewardsAndRedemptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LoyaltyRewards",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    PointsCost = table.Column<int>(type: "integer", nullable: false),
                    RewardType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    DiscountValue = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: true),
                    ValidityDays = table.Column<int>(type: "integer", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyRewards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyRedemptions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    LoyaltyAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    RewardId = table.Column<Guid>(type: "uuid", nullable: false),
                    PointsSpent = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: true),
                    CouponCode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    RedeemedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdminNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyRedemptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LoyaltyRedemptions_LoyaltyAccounts_LoyaltyAccountId",
                        column: x => x.LoyaltyAccountId,
                        principalSchema: "public",
                        principalTable: "LoyaltyAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LoyaltyRedemptions_LoyaltyRewards_RewardId",
                        column: x => x.RewardId,
                        principalSchema: "public",
                        principalTable: "LoyaltyRewards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRedemptions_AccountId",
                schema: "public",
                table: "LoyaltyRedemptions",
                column: "LoyaltyAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRedemptions_CouponCode",
                schema: "public",
                table: "LoyaltyRedemptions",
                column: "CouponCode");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRedemptions_RedeemedAt",
                schema: "public",
                table: "LoyaltyRedemptions",
                column: "RedeemedAt");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRedemptions_RewardId",
                schema: "public",
                table: "LoyaltyRedemptions",
                column: "RewardId");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRedemptions_Status",
                schema: "public",
                table: "LoyaltyRedemptions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewards_DisplayOrder",
                schema: "public",
                table: "LoyaltyRewards",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewards_IsActive",
                schema: "public",
                table: "LoyaltyRewards",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_LoyaltyRewards_RewardType",
                schema: "public",
                table: "LoyaltyRewards",
                column: "RewardType");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoyaltyRedemptions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "LoyaltyRewards",
                schema: "public");
        }
    }
}
