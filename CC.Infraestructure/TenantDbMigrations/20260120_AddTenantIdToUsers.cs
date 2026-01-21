using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CC.Infraestructure.TenantDbMigrations
{
  /// <summary>
  /// Migración para agregar validación de tenant ownership a usuarios
  /// </summary>
  public partial class AddTenantIdToUsers : Migration
  {
    protected override void Up(MigrationBuilder migrationBuilder)
    {
      // 1. Agregar columna TenantId (nullable temporalmente para datos existentes)
      migrationBuilder.AddColumn<Guid>(
          name: "TenantId",
          table: "Users",
          type: "uuid",
          nullable: true);

      // 2. Poblar TenantId para usuarios existentes
      // NOTA: Este script asume que el tenant_id se puede obtener del contexto
      // En producción, ejecutar manualmente: UPDATE "Users" SET "TenantId" = '{tenant-guid}'
      // Por ahora, usar un valor por defecto que debe ser actualizado manualmente
      migrationBuilder.Sql(@"
                -- Actualizar usuarios existentes con un TenantId temporal
                -- ⚠️ IMPORTANTE: Este script debe ser personalizado por tenant antes de ejecutar
                -- Reemplazar '00000000-0000-0000-0000-000000000000' con el ID real del tenant
                UPDATE ""Users"" 
                SET ""TenantId"" = '00000000-0000-0000-0000-000000000000'::uuid 
                WHERE ""TenantId"" IS NULL;
                
                -- Instrucciones para migración manual:
                -- 1. Obtener tenant ID desde tabla admin.""Tenants""
                -- 2. Ejecutar: UPDATE ""Users"" SET ""TenantId"" = '{tenant-id}' WHERE ""TenantId"" = '00000000-0000-0000-0000-000000000000';
            ");

      // 3. Hacer la columna NOT NULL después de poblar
      migrationBuilder.AlterColumn<Guid>(
          name: "TenantId",
          table: "Users",
          type: "uuid",
          nullable: false,
          oldClrType: typeof(Guid),
          oldType: "uuid",
          oldNullable: true);

      // 4. Crear índice compuesto para optimizar validación de ownership
      migrationBuilder.CreateIndex(
          name: "IX_Users_Id_TenantId",
          table: "Users",
          columns: new[] { "Id", "TenantId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
      migrationBuilder.DropIndex(
          name: "IX_Users_Id_TenantId",
          table: "Users");

      migrationBuilder.DropColumn(
          name: "TenantId",
          table: "Users");
    }
  }
}
