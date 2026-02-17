using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.AdminDbMigrations
{
    /// <inheritdoc />
    public partial class AddAdminRolesPermissionsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSystemRole",
                schema: "admin",
                table: "AdminRoles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AdminPermissions",
                schema: "admin",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Resource = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsSystemPermission = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminPermissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AdminRolePermissions",
                schema: "admin",
                columns: table => new
                {
                    AdminRoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminPermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AssignedByUserId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdminRolePermissions", x => new { x.AdminRoleId, x.AdminPermissionId });
                    table.ForeignKey(
                        name: "FK_AdminRolePermissions_AdminPermissions_AdminPermissionId",
                        column: x => x.AdminPermissionId,
                        principalSchema: "admin",
                        principalTable: "AdminPermissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AdminRolePermissions_AdminRoles_AdminRoleId",
                        column: x => x.AdminRoleId,
                        principalSchema: "admin",
                        principalTable: "AdminRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AdminPermissions_Name",
                schema: "admin",
                table: "AdminPermissions",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AdminRolePermissions_AdminPermissionId",
                schema: "admin",
                table: "AdminRolePermissions",
                column: "AdminPermissionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdminRolePermissions",
                schema: "admin");

            migrationBuilder.DropTable(
                name: "AdminPermissions",
                schema: "admin");

            migrationBuilder.DropColumn(
                name: "IsSystemRole",
                schema: "admin",
                table: "AdminRoles");
        }
    }
}
