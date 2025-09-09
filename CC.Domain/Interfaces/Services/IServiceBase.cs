using System.Linq.Expressions;

namespace CC.Domain.Interfaces.Services
{
    public interface IServiceBase<TEntity, TEntityDto>
           where TEntity : class
           where TEntityDto : class
    {
        Task<TEntityDto> AddAsync(TEntityDto entityDto);

        Task AddRangeAsync(IEnumerable<TEntityDto> entities);

        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> filter = null);

        Task DeleteAsync(TEntityDto entity);

        Task DeleteRangeAsync(IEnumerable<TEntityDto> entities);

        Task ExecuteSqlCommandAsync(string query, params object[] parameters);

        Task<IEnumerable<TEntityDto>> ExecuteStoredProcedureAsync(string query, params object[] parameters);

        Task<TEntityDto> FindByIdAsync(dynamic id);

        Task<TEntityDto> FindByAlternateKeyAsync(Expression<Func<TEntity, bool>> alternateKey, string includeProperties = "");

        Task<IEnumerable<TEntityDto>> GetAllAsync(Expression<Func<TEntity, bool>> filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null, string includeProperties = "");

        Task<IEnumerable<TEntityDto>> GetAllPagedAsync(int take, int skip, Expression<Func<TEntity, bool>> filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>> orderBy = null, string includeProperties = "");

        Task UpdateAsync(TEntityDto entity);

        Task UpdateRangeAsync(IEnumerable<TEntityDto> entities);

        Task TruncateTableAsync();
    }
}