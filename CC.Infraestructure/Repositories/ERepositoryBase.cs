using CC.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace CC.Infraestructure.Repositories
{
    public class ERepositoryBase<TEntity> : IERepositoryBase<TEntity> where TEntity : class
    {
        private readonly IQueryableUnitOfWork unitOfWork;

        public ERepositoryBase(IQueryableUnitOfWork unitOfWork)
        {
            this.unitOfWork = unitOfWork;
        }

        public async Task<TEntity> FindByIdAsync(dynamic id)
        {
            var entity = await unitOfWork.GetSet<TEntity>().FindAsync(id).ConfigureAwait(false);
            unitOfWork.DetachLocal<TEntity>(entity, EntityState.Detached);
            return entity;
        }

        public async Task<TEntity> FindByAlternateKeyAsync(Expression<Func<TEntity, bool>> alternateKey, string includeProperties = "")
        {
            var entity = unitOfWork.GetSet<TEntity>().AsNoTracking();

            includeProperties.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList().ForEach(property =>
            {
                entity = entity.Include(property.Trim());
            });

            return await entity.FirstOrDefaultAsync(alternateKey).ConfigureAwait(false);
        }

        public async Task<IEnumerable<TEntity>> GetAllAsync(
            Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
            string includeProperties = "")
        {
            return await BuildQuery(filter, orderBy, includeProperties).AsNoTracking().ToListAsync().ConfigureAwait(false);
        }

        public async Task<IEnumerable<TEntity>> GetAllPagedAsync(
            int take,
            int skip,
            Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
            string includeProperties = "")
        {
            return await BuildQuery(filter, orderBy, includeProperties).Skip(skip).Take(take).ToListAsync().ConfigureAwait(false);
        }

        public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> filter = null)
        {
            var item = unitOfWork.GetSet<TEntity>().AsNoTracking();
            return filter == null ? await item.AnyAsync().ConfigureAwait(false) : await item.AnyAsync(filter).ConfigureAwait(false);
        }

        public async Task<TEntity> AddAsync(TEntity entity)
        {
            ValidateEntity(entity);

            try
            {
                await unitOfWork.GetSet<TEntity>().AddAsync(entity).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.CommitAsync().ConfigureAwait(false);
            }
            return entity;
        }

        public async Task AddRangeAsync(IEnumerable<TEntity> entities)
        {
            ValidateRangeEntities(entities);
            try
            {
                await unitOfWork.GetSet<TEntity>().AddRangeAsync(entities).ConfigureAwait(false);
            }
            finally
            {
                await unitOfWork.CommitAsync().ConfigureAwait(false);
            }
        }

        public async Task UpdateAsync(TEntity entity)
        {
            ValidateEntity(entity);
            try
            {
                DbSet<TEntity> dbSet = unitOfWork.GetSet<TEntity>();
                unitOfWork.DetachLocal<TEntity>(entity, EntityState.Modified);
                dbSet.Update(entity);
            }
            finally
            {
                await unitOfWork.CommitAsync().ConfigureAwait(false);
            }
        }

        public async Task UpdateRangeAsync(IEnumerable<TEntity> entities)
        {
            ValidateRangeEntities(entities);
            try
            {
                unitOfWork.GetSet<TEntity>().UpdateRange(entities);
            }
            finally
            {
                await unitOfWork.CommitAsync().ConfigureAwait(false);
            }
        }

        public async Task DeleteAsync(TEntity entity)
        {
            ValidateEntity(entity);
            try
            {
                unitOfWork.GetSet<TEntity>().Remove(entity);
            }
            finally
            {
                await unitOfWork.CommitAsync().ConfigureAwait(false);
            }
        }

        public async Task DeleteRangeAsync(IEnumerable<TEntity> entities)
        {
            ValidateRangeEntities(entities);
            try
            {
                unitOfWork.GetSet<TEntity>().RemoveRange(entities);
            }
            finally
            {
                await unitOfWork.CommitAsync().ConfigureAwait(false);
            }
        }

        public async Task<IEnumerable<TEntity>> ExecuteStoredProcedureAsync(string query, params object[] parameters)
        {
            var item = unitOfWork.GetSet<TEntity>();
            return await item.FromSqlRaw(query, parameters).ToListAsync().ConfigureAwait(false);
        }

        public async Task ExecuteSqlCommandAsync(string query, params object[] parameters)
        {
            await unitOfWork.GetContext().Database.ExecuteSqlRawAsync(query, parameters).ConfigureAwait(false);
        }

        #region PrivateMethods

        private IQueryable<TEntity> BuildQuery(
            Expression<Func<TEntity, bool>> filter = null,
            Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null,
            string includeProperties = "")
        {
            IQueryable<TEntity> query = unitOfWork.GetSet<TEntity>().AsNoTracking();

            if (filter != null)
            {
                query = query.Where(filter);
            }

            includeProperties.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList().ForEach(property =>
            {
                query = query.Include(property);
            });

            if (orderBy != null)
            {
                return orderBy(query);
            }

            return query;
        }

        private static void ValidateEntity(TEntity entity)
        {
            if (entity == null)
            {
                throw new ArgumentNullException(nameof(entity), "El objeto entidad no puede ser nulo");
            }
        }

        private static void ValidateRangeEntities(IEnumerable<TEntity> entities)
        {
            if (!entities?.Any() ?? true)
            {
                throw new ArgumentNullException(nameof(entities), "no se envió una lista de entidades a insertar");
            }
        }

        #endregion PrivateMethods

        #region Dispose

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                unitOfWork.Dispose();
            }
        }

        #endregion Dispose
    }
}