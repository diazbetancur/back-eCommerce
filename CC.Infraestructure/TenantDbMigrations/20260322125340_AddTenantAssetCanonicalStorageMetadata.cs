using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddTenantAssetCanonicalStorageMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.""TenantAssets""
ADD COLUMN IF NOT EXISTS ""PublicUrl"" character varying(1000);
");

            migrationBuilder.Sql(@"
ALTER TABLE public.""TenantAssets""
ADD COLUMN IF NOT EXISTS ""StorageBucket"" character varying(120);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE public.\"TenantAssets\" DROP COLUMN IF EXISTS \"PublicUrl\";");
            migrationBuilder.Sql("ALTER TABLE public.\"TenantAssets\" DROP COLUMN IF EXISTS \"StorageBucket\";");
        }
    }
}
