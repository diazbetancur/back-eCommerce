using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddUserActivationStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                schema: "public",
                table: "Users",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Active");

            migrationBuilder.Sql(@"
UPDATE public.""Users""
SET ""Status"" = CASE
    WHEN ""IsActive"" THEN 'Active'
    ELSE 'Inactive'
END;
");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Status",
                schema: "public",
                table: "Users",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Status",
                schema: "public",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Status",
                schema: "public",
                table: "Users");
        }
    }
}
