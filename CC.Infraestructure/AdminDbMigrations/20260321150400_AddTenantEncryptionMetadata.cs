using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.AdminDbMigrations
{
    /// <inheritdoc />
    public partial class AddTenantEncryptionMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EncryptionAlgorithm",
                schema: "admin",
                table: "Tenants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionKeyId",
                schema: "admin",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EncryptionVersion",
                schema: "admin",
                table: "Tenants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EncryptionAlgorithm",
                schema: "admin",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EncryptionKeyId",
                schema: "admin",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "EncryptionVersion",
                schema: "admin",
                table: "Tenants");
        }
    }
}
