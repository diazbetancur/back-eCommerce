using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TenantEntity = CC.Domain.Tenancy.Tenant;

namespace CC.Infraestructure.AdminDb.Configurations
{
    /// <summary>
    /// Configuración Fluent API para la entidad Tenant
    /// </summary>
    public class TenantConfiguration : IEntityTypeConfiguration<TenantEntity>
    {
        public void Configure(EntityTypeBuilder<TenantEntity> builder)
        {
            builder.ToTable("Tenants", schema: "admin");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Slug)
                .IsRequired()
                .HasMaxLength(50);

            // Índice único para el Slug
            builder.HasIndex(t => t.Slug)
                .IsUnique()
                .HasDatabaseName("IX_Tenants_Slug");

            builder.Property(t => t.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(t => t.Plan)
                .HasMaxLength(50);

            builder.Property(t => t.DbName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(t => t.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            builder.Property(t => t.FeatureFlagsJson)
                .HasColumnType("jsonb"); // PostgreSQL JSONB

            builder.Property(t => t.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(t => t.UpdatedAt);

            builder.Property(t => t.LastError);

            // Relación con TenantProvisioning
            builder.HasMany(t => t.ProvisioningHistory)
                .WithOne(tp => tp.Tenant)
                .HasForeignKey(tp => tp.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
