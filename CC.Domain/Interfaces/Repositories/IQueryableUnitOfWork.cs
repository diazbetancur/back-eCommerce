using Microsoft.EntityFrameworkCore;

namespace CC.Domain.Interfaces.Repositories
{
    public interface IQueryableUnitOfWork : IDisposable
    {
        void Commit();

        Task CommitAsync();

        void DetachLocal<TEntity>(TEntity entity, EntityState state) where TEntity : class;

        DbContext GetContext();

        DbSet<TEntity> GetSet<TEntity>() where TEntity : class;
    }
}