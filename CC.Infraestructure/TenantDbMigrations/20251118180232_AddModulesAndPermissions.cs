using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddModulesAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Users",
                schema: "public",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_UserRoles",
                schema: "public",
                table: "UserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Roles",
                schema: "public",
                table: "Roles");

            migrationBuilder.RenameTable(
                name: "Users",
                schema: "public",
                newName: "TenantUsers",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "UserRoles",
                schema: "public",
                newName: "TenantUserRoles",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "Roles",
                schema: "public",
                newName: "TenantRoles",
                newSchema: "public");

            migrationBuilder.RenameIndex(
                name: "IX_Users_Email",
                schema: "public",
                table: "TenantUsers",
                newName: "IX_TenantUsers_Email");

            migrationBuilder.RenameIndex(
                name: "IX_Roles_Name",
                schema: "public",
                table: "TenantRoles",
                newName: "IX_TenantRoles_Name");

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                schema: "public",
                table: "TenantUserRoles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "public",
                table: "TenantRoles",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "public",
                table: "TenantRoles",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenantUsers",
                schema: "public",
                table: "TenantUsers",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenantUserRoles",
                schema: "public",
                table: "TenantUserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_TenantRoles",
                schema: "public",
                table: "TenantRoles",
                column: "Id");

            migrationBuilder.CreateTable(
                name: "Modules",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IconName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Modules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleModulePermissions",
                schema: "public",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleId = table.Column<Guid>(type: "uuid", nullable: false),
                    CanView = table.Column<bool>(type: "boolean", nullable: false),
                    CanCreate = table.Column<bool>(type: "boolean", nullable: false),
                    CanUpdate = table.Column<bool>(type: "boolean", nullable: false),
                    CanDelete = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleModulePermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoleModulePermissions_Modules_ModuleId",
                        column: x => x.ModuleId,
                        principalSchema: "public",
                        principalTable: "Modules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleModulePermissions_TenantRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "public",
                        principalTable: "TenantRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantUserRoles_RoleId",
                schema: "public",
                table: "TenantUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Modules_Code",
                schema: "public",
                table: "Modules",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoleModulePermissions_ModuleId",
                schema: "public",
                table: "RoleModulePermissions",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "UQ_RoleModulePermissions_RoleId_ModuleId",
                schema: "public",
                table: "RoleModulePermissions",
                columns: new[] { "RoleId", "ModuleId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantUserRoles_TenantRoles_RoleId",
                schema: "public",
                table: "TenantUserRoles",
                column: "RoleId",
                principalSchema: "public",
                principalTable: "TenantRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TenantUserRoles_TenantUsers_UserId",
                schema: "public",
                table: "TenantUserRoles",
                column: "UserId",
                principalSchema: "public",
                principalTable: "TenantUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TenantUserRoles_TenantRoles_RoleId",
                schema: "public",
                table: "TenantUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_TenantUserRoles_TenantUsers_UserId",
                schema: "public",
                table: "TenantUserRoles");

            migrationBuilder.DropTable(
                name: "RoleModulePermissions",
                schema: "public");

            migrationBuilder.DropTable(
                name: "Modules",
                schema: "public");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenantUsers",
                schema: "public",
                table: "TenantUsers");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenantUserRoles",
                schema: "public",
                table: "TenantUserRoles");

            migrationBuilder.DropIndex(
                name: "IX_TenantUserRoles_RoleId",
                schema: "public",
                table: "TenantUserRoles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_TenantRoles",
                schema: "public",
                table: "TenantRoles");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                schema: "public",
                table: "TenantUserRoles");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "public",
                table: "TenantRoles");

            migrationBuilder.RenameTable(
                name: "TenantUsers",
                schema: "public",
                newName: "Users",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "TenantUserRoles",
                schema: "public",
                newName: "UserRoles",
                newSchema: "public");

            migrationBuilder.RenameTable(
                name: "TenantRoles",
                schema: "public",
                newName: "Roles",
                newSchema: "public");

            migrationBuilder.RenameIndex(
                name: "IX_TenantUsers_Email",
                schema: "public",
                table: "Users",
                newName: "IX_Users_Email");

            migrationBuilder.RenameIndex(
                name: "IX_TenantRoles_Name",
                schema: "public",
                table: "Roles",
                newName: "IX_Roles_Name");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "public",
                table: "Roles",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Users",
                schema: "public",
                table: "Users",
                column: "Id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_UserRoles",
                schema: "public",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_Roles",
                schema: "public",
                table: "Roles",
                column: "Id");
        }
    }
}
