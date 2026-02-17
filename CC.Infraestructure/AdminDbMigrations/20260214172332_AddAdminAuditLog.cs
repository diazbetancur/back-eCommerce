using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.AdminDbMigrations
{
    /// <inheritdoc />
    public partial class AddAdminAuditLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AdminAuditLogs",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserEmail = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ResourceId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Details = table.Column<string>(type: "jsonb", nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdminAuditLogs_AdminUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalSchema: "admin",
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_Action",
                schema: "admin",
                table: "AdminAuditLogs",
                column: "Action");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_AdminUserId",
                schema: "admin",
                table: "AdminAuditLogs",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_AdminUserId_CreatedAt",
                schema: "admin",
                table: "AdminAuditLogs",
                columns: new[] { "AdminUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_CreatedAt",
                schema: "admin",
                table: "AdminAuditLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ResourceType",
                schema: "admin",
                table: "AdminAuditLogs",
                column: "ResourceType");

            migrationBuilder.CreateIndex(
                name: "IX_AdminAuditLogs_ResourceType_ResourceId",
                schema: "admin",
                table: "AdminAuditLogs",
                columns: new[] { "ResourceType", "ResourceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminAuditLogs",
                schema: "admin");
        }
    }
}
