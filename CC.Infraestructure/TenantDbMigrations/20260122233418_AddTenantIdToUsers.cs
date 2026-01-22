using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddTenantIdToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TenantId",
                schema: "public",
                table: "Users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Users_Id_TenantId",
                schema: "public",
                table: "Users",
                columns: new[] { "Id", "TenantId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Id_TenantId",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                schema: "public",
                table: "Users");
        }
    }
}
