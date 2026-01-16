using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddStoresAndInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "public",
                table: "LoyaltyTransactions",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "public",
                table: "LoyaltyRewards",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "public",
                table: "LoyaltyRedemptions",
                newName: "DateCreated");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                schema: "public",
                table: "LoyaltyAccounts",
                newName: "DateCreated");

            migrationBuilder.AddColumn<Guid>(
                name: "StoreId",
                schema: "public",
                table: "Orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LoyaltyConfigurations",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConversionRate = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    PointsExpirationDays = table.Column<int>(type: "integer", nullable: true),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MinPurchaseForPoints = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyConfigurations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stores",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    City = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsDefault = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stores", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProductStoreStock",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: false),
                    ReservedStock = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductStoreStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductStoreStock_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "public",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductStoreStock_Stores_StoreId",
                        column: x => x.StoreId,
                        principalSchema: "public",
                        principalTable: "Stores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_StoreId",
                schema: "public",
                table: "Orders",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductStoreStock_ProductId",
                schema: "public",
                table: "ProductStoreStock",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductStoreStock_StoreId",
                schema: "public",
                table: "ProductStoreStock",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "UQ_ProductStoreStock_ProductStore",
                schema: "public",
                table: "ProductStoreStock",
                columns: new[] { "ProductId", "StoreId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stores_IsActive",
                schema: "public",
                table: "Stores",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Stores_IsDefault",
                schema: "public",
                table: "Stores",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "UQ_Stores_Code",
                schema: "public",
                table: "Stores",
                column: "Code",
                unique: true,
                filter: "\"Code\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Stores_StoreId",
                schema: "public",
                table: "Orders",
                column: "StoreId",
                principalSchema: "public",
                principalTable: "Stores",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Stores_StoreId",
                schema: "public",
                table: "Orders");

            migrationBuilder.DropTable(
                name: "LoyaltyConfigurations",
                schema: "public");

            migrationBuilder.DropTable(
                name: "ProductStoreStock",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Stores",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_Orders_StoreId",
                schema: "public",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StoreId",
                schema: "public",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                schema: "public",
                table: "LoyaltyTransactions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                schema: "public",
                table: "LoyaltyRewards",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                schema: "public",
                table: "LoyaltyRedemptions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "DateCreated",
                schema: "public",
                table: "LoyaltyAccounts",
                newName: "CreatedAt");
        }
    }
}
