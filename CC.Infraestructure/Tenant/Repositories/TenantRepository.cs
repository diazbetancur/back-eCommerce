using System.Linq.Expressions;
using CC.Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Tenant.Repositories
{
  /// <summary>
  /// Generic repository implementation for multi-tenant entities.
  /// Uses the TenantDbContext for data access.
  /// </summary>
  /// <typeparam name="TEntity">Entity type</typeparam>
  public class TenantRepository<TEntity> : ITenantRepository<TEntity> where TEntity : class
  {
    protected readonly TenantDbContext _context;
    protected readonly DbSet<TEntity> _dbSet;

    public TenantRepository(TenantDbContext context)
    {
      _context = context ?? throw new ArgumentNullException(nameof(context));
      _dbSet = context.Set<TEntity>();
    }

    #region Query Operations

    /// <inheritdoc />
    public IQueryable<TEntity> Query => _dbSet;

    /// <inheritdoc />
    public IQueryable<TEntity> QueryNoTracking => _dbSet.AsNoTracking();

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default)
    {
      return await _dbSet.FindAsync(new[] { id }, ct);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken ct = default)
    {
      return await _dbSet.FindAsync(keyValues, ct);
    }

    /// <inheritdoc />
    public virtual async Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default)
    {
      return await _dbSet.FirstOrDefaultAsync(predicate, ct);
    }

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default)
    {
      IQueryable<TEntity> query = _dbSet.AsNoTracking();

      if (predicate != null)
      {
        query = query.Where(predicate);
      }

      return await query.ToListAsync(ct);
    }

    /// <inheritdoc />
    public virtual async Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default)
    {
      IQueryable<TEntity> query = _dbSet.AsNoTracking();

      if (predicate != null)
      {
        query = query.Where(predicate);
      }

      var totalCount = await query.CountAsync(ct);

      if (orderBy != null)
      {
        query = orderBy(query);
      }

      var items = await query
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync(ct);

      return (items, totalCount);
    }

    /// <inheritdoc />
    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default)
    {
      return predicate == null
          ? await _dbSet.AnyAsync(ct)
          : await _dbSet.AnyAsync(predicate, ct);
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default)
    {
      return predicate == null
          ? await _dbSet.CountAsync(ct)
          : await _dbSet.CountAsync(predicate, ct);
    }

    #endregion

    #region Command Operations

    /// <inheritdoc />
    public virtual void Add(TEntity entity)
    {
      _dbSet.Add(entity);
    }

    /// <inheritdoc />
    public virtual void AddRange(IEnumerable<TEntity> entities)
    {
      _dbSet.AddRange(entities);
    }

    /// <inheritdoc />
    public virtual void Update(TEntity entity)
    {
      _dbSet.Update(entity);
    }

    /// <inheritdoc />
    public virtual void UpdateRange(IEnumerable<TEntity> entities)
    {
      _dbSet.UpdateRange(entities);
    }

    /// <inheritdoc />
    public virtual void Remove(TEntity entity)
    {
      _dbSet.Remove(entity);
    }

    /// <inheritdoc />
    public virtual void RemoveRange(IEnumerable<TEntity> entities)
    {
      _dbSet.RemoveRange(entities);
    }

    #endregion

    #region Specification Pattern

    /// <inheritdoc />
    public virtual async Task<List<TEntity>> FindAsync(
        ISpecification<TEntity> specification,
        CancellationToken ct = default)
    {
      return await ApplySpecification(specification).ToListAsync(ct);
    }

    /// <inheritdoc />
    public virtual async Task<int> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken ct = default)
    {
      return await ApplySpecification(specification, true).CountAsync(ct);
    }

    private IQueryable<TEntity> ApplySpecification(
        ISpecification<TEntity> specification,
        bool countOnly = false)
    {
      return SpecificationEvaluator<TEntity>.GetQuery(
          _dbSet.AsQueryable(),
          specification,
          countOnly);
    }

    #endregion
  }

  /// <summary>
  /// Evaluates specifications and builds queries
  /// </summary>
  public static class SpecificationEvaluator<TEntity> where TEntity : class
  {
    public static IQueryable<TEntity> GetQuery(
        IQueryable<TEntity> inputQuery,
        ISpecification<TEntity> specification,
        bool countOnly = false)
    {
      var query = inputQuery;

      // Apply criteria
      if (specification.Criteria != null)
      {
        query = query.Where(specification.Criteria);
      }

      // Apply includes (for tracking queries)
      query = specification.Includes
          .Aggregate(query, (current, include) => current.Include(include));

      query = specification.IncludeStrings
          .Aggregate(query, (current, include) => current.Include(include));

      // Skip ordering, paging for count queries
      if (countOnly)
      {
        return query;
      }

      // Apply ordering
      if (specification.OrderBy != null)
      {
        query = query.OrderBy(specification.OrderBy);
      }
      else if (specification.OrderByDescending != null)
      {
        query = query.OrderByDescending(specification.OrderByDescending);
      }

      // Apply paging
      if (specification.IsPagingEnabled)
      {
        if (specification.Skip.HasValue)
        {
          query = query.Skip(specification.Skip.Value);
        }

        if (specification.Take.HasValue)
        {
          query = query.Take(specification.Take.Value);
        }
      }

      return query;
    }
  }
}
