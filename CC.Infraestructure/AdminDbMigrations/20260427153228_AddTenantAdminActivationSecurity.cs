using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.AdminDbMigrations
{
    /// <inheritdoc />
    public partial class AddTenantAdminActivationSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PrimaryAdminEmail",
                schema: "admin",
                table: "Tenants",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PrimaryAdminUserId",
                schema: "admin",
                table: "Tenants",
                type: "uuid",
                nullable: true);

            migrationBuilder.Sql(@"
UPDATE admin.""Tenants""
SET ""Status"" = 2
WHERE ""Status"" IN (0, 2);
");

            migrationBuilder.CreateTable(
                name: "UserSecurityToken",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Purpose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConsumedIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    ConsumedUserAgent = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSecurityToken", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSecurityToken_ExpiresAt",
                schema: "admin",
                table: "UserSecurityToken",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_UserSecurityToken_TenantId_Purpose",
                schema: "admin",
                table: "UserSecurityToken",
                columns: new[] { "TenantId", "Purpose" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSecurityToken_TokenHash",
                schema: "admin",
                table: "UserSecurityToken",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSecurityToken_UserId_Purpose_UsedAt_RevokedAt",
                schema: "admin",
                table: "UserSecurityToken",
                columns: new[] { "UserId", "Purpose", "UsedAt", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSecurityToken",
                schema: "admin");

            migrationBuilder.DropColumn(
                name: "PrimaryAdminEmail",
                schema: "admin",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PrimaryAdminUserId",
                schema: "admin",
                table: "Tenants");
        }
    }
}
