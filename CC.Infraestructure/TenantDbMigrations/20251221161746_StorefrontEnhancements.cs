using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class StorefrontEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Brand",
                schema: "public",
                table: "Products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CompareAtPrice",
                schema: "public",
                table: "Products",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFeatured",
                schema: "public",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MainImageUrl",
                schema: "public",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                schema: "public",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                schema: "public",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShortDescription",
                schema: "public",
                table: "Products",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                schema: "public",
                table: "Products",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "public",
                table: "Products",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                schema: "public",
                table: "Products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrackInventory",
                schema: "public",
                table: "Products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                schema: "public",
                table: "Products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                schema: "public",
                table: "Categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "public",
                table: "Categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "public",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                schema: "public",
                table: "Categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                schema: "public",
                table: "Categories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                schema: "public",
                table: "Categories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInMenu",
                schema: "public",
                table: "Categories",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "public",
                table: "Categories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "Banners",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Subtitle = table.Column<string>(type: "text", nullable: true),
                    ImageUrlDesktop = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ImageUrlMobile = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ButtonText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DisplayOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Banners", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsActive",
                schema: "public",
                table: "Products",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsFeatured",
                schema: "public",
                table: "Products",
                column: "IsFeatured");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Name",
                schema: "public",
                table: "Products",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Sku",
                schema: "public",
                table: "Products",
                column: "Sku");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Slug",
                schema: "public",
                table: "Products",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_Tags",
                schema: "public",
                table: "Products",
                column: "Tags");

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ProductId",
                schema: "public",
                table: "ProductImages",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductCategories_CategoryId",
                schema: "public",
                table: "ProductCategories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_DisplayOrder",
                schema: "public",
                table: "Categories",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_ParentId",
                schema: "public",
                table: "Categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_Slug",
                schema: "public",
                table: "Categories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Banners_DisplayOrder",
                schema: "public",
                table: "Banners",
                column: "DisplayOrder");

            migrationBuilder.CreateIndex(
                name: "IX_Banners_IsActive",
                schema: "public",
                table: "Banners",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Banners_Position",
                schema: "public",
                table: "Banners",
                column: "Position");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_Categories_ParentId",
                schema: "public",
                table: "Categories",
                column: "ParentId",
                principalSchema: "public",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCategories_Categories_CategoryId",
                schema: "public",
                table: "ProductCategories",
                column: "CategoryId",
                principalSchema: "public",
                principalTable: "Categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductCategories_Products_ProductId",
                schema: "public",
                table: "ProductCategories",
                column: "ProductId",
                principalSchema: "public",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Products_ProductId",
                schema: "public",
                table: "ProductImages",
                column: "ProductId",
                principalSchema: "public",
                principalTable: "Products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_Categories_ParentId",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductCategories_Categories_CategoryId",
                schema: "public",
                table: "ProductCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductCategories_Products_ProductId",
                schema: "public",
                table: "ProductCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_Products_ProductId",
                schema: "public",
                table: "ProductImages");

            migrationBuilder.DropTable(
                name: "Banners",
                schema: "public");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsActive",
                schema: "public",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsFeatured",
                schema: "public",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Name",
                schema: "public",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Sku",
                schema: "public",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Slug",
                schema: "public",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_Tags",
                schema: "public",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ProductId",
                schema: "public",
                table: "ProductImages");

            migrationBuilder.DropIndex(
                name: "IX_ProductCategories_CategoryId",
                schema: "public",
                table: "ProductCategories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_DisplayOrder",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_ParentId",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropIndex(
                name: "IX_Categories_Slug",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Brand",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "CompareAtPrice",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "IsFeatured",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MainImageUrl",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ShortDescription",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Sku",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Tags",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "TrackInventory",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                schema: "public",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ParentId",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ShowInMenu",
                schema: "public",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "public",
                table: "Categories");
        }
    }
}
