using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.AdminDbMigrations
{
    /// <inheritdoc />
    public partial class AddPlanStorageBytesAndLongLimitValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "LimitValue",
                schema: "admin",
                table: "PlanLimits",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    UPDATE admin.""PlanLimits""
    SET ""LimitValue"" = 5120
    WHERE ""PlanId"" = '11111111-0000-0000-0000-000000000001'::uuid
        AND ""LimitCode"" = 'max_storage_mb';

    UPDATE admin.""PlanLimits""
    SET ""LimitValue"" = 10240
    WHERE ""PlanId"" = '22222222-0000-0000-0000-000000000002'::uuid
        AND ""LimitCode"" = 'max_storage_mb';

    INSERT INTO admin.""PlanLimits"" (""Id"", ""PlanId"", ""LimitCode"", ""LimitValue"", ""Description"", ""CreatedAt"")
    VALUES (
        '31111111-0000-0000-0000-000000000001'::uuid,
        '11111111-0000-0000-0000-000000000001'::uuid,
        'max_storage_bytes',
        5368709120,
        '5 GB de almacenamiento (bytes)',
        NOW()
    )
    ON CONFLICT (""PlanId"", ""LimitCode"")
    DO UPDATE SET
        ""LimitValue"" = EXCLUDED.""LimitValue"",
        ""Description"" = EXCLUDED.""Description"";

    INSERT INTO admin.""PlanLimits"" (""Id"", ""PlanId"", ""LimitCode"", ""LimitValue"", ""Description"", ""CreatedAt"")
    VALUES (
        '32222222-0000-0000-0000-000000000002'::uuid,
        '22222222-0000-0000-0000-000000000002'::uuid,
        'max_storage_bytes',
        10737418240,
        '10 GB de almacenamiento (bytes)',
        NOW()
    )
    ON CONFLICT (""PlanId"", ""LimitCode"")
    DO UPDATE SET
        ""LimitValue"" = EXCLUDED.""LimitValue"",
        ""Description"" = EXCLUDED.""Description"";
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM admin.""PlanLimits""
WHERE ""LimitCode"" = 'max_storage_bytes'
    AND ""PlanId"" IN (
        '11111111-0000-0000-0000-000000000001'::uuid,
        '22222222-0000-0000-0000-000000000002'::uuid
    );
");

            migrationBuilder.AlterColumn<int>(
                name: "LimitValue",
                schema: "admin",
                table: "PlanLimits",
                type: "integer",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
