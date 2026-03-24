using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddPopupManagementModule : Migration
    {
        private const string SchemaName = "public";
        private const string TableName = "Popups";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: TableName,
                schema: SchemaName,
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TargetUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ButtonText = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Popups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Popups_EndDate",
                schema: SchemaName,
                table: TableName,
                column: "EndDate");

            migrationBuilder.CreateIndex(
                name: "IX_Popups_IsActive",
                schema: SchemaName,
                table: TableName,
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Popups_StartDate",
                schema: SchemaName,
                table: TableName,
                column: "StartDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: TableName,
                schema: SchemaName);
        }
    }
}
