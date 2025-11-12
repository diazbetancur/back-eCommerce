using CC.Domain.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CC.Infraestructure.AdminDb.Configurations
{
    /// <summary>
    /// Configuración Fluent API para la entidad TenantProvisioning
    /// </summary>
    public class TenantProvisioningConfiguration : IEntityTypeConfiguration<TenantProvisioning>
    {
        public void Configure(EntityTypeBuilder<TenantProvisioning> builder)
        {
            builder.ToTable("TenantProvisionings", schema: "admin");

            builder.HasKey(tp => tp.Id);

            builder.Property(tp => tp.TenantId)
                .IsRequired();

            builder.Property(tp => tp.Step)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(tp => tp.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue("Pending");

            builder.Property(tp => tp.Message);

            builder.Property(tp => tp.StartedAt)
                .IsRequired()
                .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(tp => tp.CompletedAt);

            builder.Property(tp => tp.ErrorMessage);

            // Índice para búsquedas por TenantId
            builder.HasIndex(tp => tp.TenantId)
                .HasDatabaseName("IX_TenantProvisionings_TenantId");

            // Índice compuesto para búsquedas por TenantId y Step
            builder.HasIndex(tp => new { tp.TenantId, tp.Step })
                .HasDatabaseName("IX_TenantProvisionings_TenantId_Step");
        }
    }
}
