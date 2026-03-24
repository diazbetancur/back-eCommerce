using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
    /// <inheritdoc />
    public partial class FixUserProfileForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_UserProfiles_UserAccount_Id'
    ) THEN
        ALTER TABLE public.""UserProfiles"" DROP CONSTRAINT ""FK_UserProfiles_UserAccount_Id"";
    END IF;
END $$;
");

            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS public.""UserAccount"";
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS public.""UserAccount"" (
    ""Id"" uuid NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""Email"" character varying(255) NOT NULL,
    ""IsActive"" boolean NOT NULL,
    ""PasswordHash"" character varying(500) NOT NULL,
    ""PasswordSalt"" character varying(500) NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_UserAccount"" PRIMARY KEY (""Id"")
);
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'FK_UserProfiles_UserAccount_Id'
    ) THEN
        ALTER TABLE public.""UserProfiles""
        ADD CONSTRAINT ""FK_UserProfiles_UserAccount_Id""
        FOREIGN KEY (""Id"")
        REFERENCES public.""UserAccount"" (""Id"")
        ON DELETE CASCADE;
    END IF;
END $$;
");
        }
    }
}
