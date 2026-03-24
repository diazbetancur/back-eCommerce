using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class AddTenantAssetsModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE public.""LoyaltyTransactions""
ADD COLUMN IF NOT EXISTS ""AdjustedByUserId"" uuid;
");

            migrationBuilder.Sql(@"
ALTER TABLE public.""LoyaltyTransactions""
ADD COLUMN IF NOT EXISTS ""AdjustmentTicketNumber"" character varying(100);
");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.""TenantAssetQuotaSnapshots"" (
  ""TenantId"" uuid NOT NULL,
  ""PlanCodeSnapshot"" character varying(80) NOT NULL,
  ""MaxImageCount"" integer NOT NULL,
  ""MaxVideoCount"" integer NOT NULL,
  ""MaxTotalBytes"" bigint NOT NULL,
  ""AllowVideos"" boolean NOT NULL,
  ""AllowVisualModules"" boolean NOT NULL,
  ""CurrentImageCount"" integer NOT NULL,
  ""CurrentVideoCount"" integer NOT NULL,
  ""CurrentTotalBytes"" bigint NOT NULL,
  ""LastPlanSyncAt"" timestamp with time zone NOT NULL,
  ""LastRecalculatedAt"" timestamp with time zone NOT NULL,
  ""VersionStamp"" bigint NOT NULL,
  CONSTRAINT ""PK_TenantAssetQuotaSnapshots"" PRIMARY KEY (""TenantId"")
);
");

            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.""TenantAssets"" (
  ""Id"" uuid NOT NULL,
  ""TenantId"" uuid NOT NULL,
  ""AssetType"" integer NOT NULL,
  ""SourceType"" integer NOT NULL,
  ""Module"" character varying(80) NOT NULL,
  ""EntityType"" character varying(80) NULL,
  ""EntityId"" character varying(80) NULL,
  ""OriginalFileName"" character varying(260) NOT NULL,
  ""SafeFileName"" character varying(260) NOT NULL,
  ""StorageKey"" character varying(700) NULL,
  ""UrlOrPath"" text NOT NULL,
  ""SizeBytes"" bigint NOT NULL,
  ""Extension"" character varying(20) NOT NULL,
  ""ContentType"" character varying(120) NOT NULL,
  ""Provider"" character varying(30) NOT NULL,
  ""Visibility"" integer NOT NULL,
  ""LifecycleStatus"" integer NOT NULL,
  ""PhysicalDeletionRequired"" boolean NOT NULL,
  ""PhysicalDeletionExecuted"" boolean NOT NULL,
  ""PhysicalDeletionExecutedAt"" timestamp with time zone NULL,
  ""PhysicalDeletionAttempts"" integer NOT NULL,
  ""PhysicalDeletionLastError"" text NULL,
  ""UploadedByUserId"" character varying(100) NOT NULL,
  ""CreatedAt"" timestamp with time zone NOT NULL,
  ""DeletedAt"" timestamp with time zone NULL,
  CONSTRAINT ""PK_TenantAssets"" PRIMARY KEY (""Id"")
);
");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""IX_LoyaltyTransactions_AdjustedByUserId""
ON public.""LoyaltyTransactions"" (""AdjustedByUserId"");
");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""IX_LoyaltyTransactions_AdjustmentTicketNumber""
ON public.""LoyaltyTransactions"" (""AdjustmentTicketNumber"");
");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""IX_TenantAssets_Tenant_Module_Entity""
ON public.""TenantAssets"" (""TenantId"", ""Module"", ""EntityType"", ""EntityId"");
");

            migrationBuilder.Sql(@"
CREATE INDEX IF NOT EXISTS ""IX_TenantAssets_Tenant_Status""
ON public.""TenantAssets"" (""TenantId"", ""LifecycleStatus"");
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS public.\"TenantAssetQuotaSnapshots\";");
            migrationBuilder.Sql("DROP TABLE IF EXISTS public.\"TenantAssets\";");

            migrationBuilder.Sql("DROP INDEX IF EXISTS public.\"IX_LoyaltyTransactions_AdjustedByUserId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.\"IX_LoyaltyTransactions_AdjustmentTicketNumber\";");

            migrationBuilder.Sql("ALTER TABLE public.\"LoyaltyTransactions\" DROP COLUMN IF EXISTS \"AdjustedByUserId\";");
            migrationBuilder.Sql("ALTER TABLE public.\"LoyaltyTransactions\" DROP COLUMN IF EXISTS \"AdjustmentTicketNumber\";");
        }
    }
}
