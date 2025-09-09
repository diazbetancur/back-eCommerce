using CC.Domain.Entities;
using CC.Domain.Interfaces.Repositories;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Configurations
{
    public class DBContext : IdentityDbContext<User, Role, Guid>, IQueryableUnitOfWork
    {
        public DBContext(DbContextOptions<DBContext> options)
        : base(options)
        {
        }

        public DbSet<UserActivityLog> UserActivityLogs { get; set; }
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductCategory> ProductCategories { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<ProductProperty> ProductProperties { get; set; }
        public DbSet<ProductImage> ProductImages { get; set; }
        public DbSet<Banner> Banners { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>().Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            modelBuilder.Entity<User>().Property(p => p.FirstName).HasMaxLength(20);
            modelBuilder.Entity<User>().Property(p => p.LastName).HasMaxLength(20);

            modelBuilder.Entity<Role>().HasKey(c => c.Id);
            modelBuilder.Entity<Role>().Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");

            modelBuilder.Entity<Product>().HasKey(c => c.Id);
            modelBuilder.Entity<Product>().Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            modelBuilder.Entity<Product>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");
            modelBuilder.Entity<Product>().Property(p => p.DynamicAttributes).HasColumnType("jsonb");

            modelBuilder.Entity<ProductCategory>().HasKey(c => c.Id);
            modelBuilder.Entity<ProductCategory>().Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            modelBuilder.Entity<ProductCategory>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Category>().HasKey(c => c.Id);
            modelBuilder.Entity<Category>().Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            modelBuilder.Entity<Category>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<ProductProperty>().HasKey(c => c.Id);
            modelBuilder.Entity<ProductProperty>().Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            modelBuilder.Entity<ProductProperty>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<ProductImage>().HasKey(c => c.Id);
            modelBuilder.Entity<ProductImage>().Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            modelBuilder.Entity<ProductImage>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.Entity<Banner>().HasKey(c => c.Id);
            modelBuilder.Entity<Banner>().Property(e => e.Id).HasDefaultValueSql("gen_random_uuid()");
            modelBuilder.Entity<Banner>().Property(e => e.DateCreated).HasDefaultValueSql("CURRENT_TIMESTAMP");

            modelBuilder.HasDefaultSchema("Management");
            DisableCascadingDelete(modelBuilder);
        }

        private void DisableCascadingDelete(ModelBuilder modelBuilder)
        {
            var relationship = modelBuilder.Model.GetEntityTypes()
                .Where(e => !e.ClrType.Namespace.StartsWith("Microsoft.AspNetCore.Identity"))
                .SelectMany(e => e.GetForeignKeys());

            foreach (var r in relationship)
            {
                r.DeleteBehavior = DeleteBehavior.Restrict;
            }
        }

        public void Commit()
        {
            try
            {
                SaveChanges();
            }
            catch (DbUpdateConcurrencyException ex)
            {
                ex.Entries.Single().Reload();
            }
        }

        public async Task CommitAsync()
        {
            try
            {
                await SaveChangesAsync().ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await ex.Entries.Single().ReloadAsync().ConfigureAwait(false);
            }
        }

        public void DetachLocal<TEntity>(TEntity entity, EntityState state) where TEntity : class
        {
            if (entity is null)
            {
                return;
            }

            var local = Set<TEntity>().Local.ToList();

            if (local?.Any() ?? false)
            {
                local.ForEach(item =>
                {
                    Entry(item).State = EntityState.Detached;
                });
            }

            Entry(entity).State = state;
        }

        public DbContext GetContext()
        {
            return this;
        }

        public DbSet<TEntity> GetSet<TEntity>() where TEntity : class
        {
            return Set<TEntity>();
        }
    }
}