using System.Linq.Expressions;

namespace CC.Domain.Interfaces.Repositories
{
  /// <summary>
  /// Generic repository interface for multi-tenant entities.
  /// Provides standard CRUD operations with async/await pattern.
  /// </summary>
  /// <typeparam name="TEntity">Entity type</typeparam>
  public interface ITenantRepository<TEntity> where TEntity : class
  {
    #region Query Operations

    /// <summary>
    /// Gets a queryable for advanced queries (use AsNoTracking() for read-only)
    /// </summary>
    IQueryable<TEntity> Query { get; }

    /// <summary>
    /// Gets a no-tracking queryable for read-only operations (better performance)
    /// </summary>
    IQueryable<TEntity> QueryNoTracking { get; }

    /// <summary>
    /// Find entity by primary key
    /// </summary>
    Task<TEntity?> GetByIdAsync(object id, CancellationToken ct = default);

    /// <summary>
    /// Find entity by composite key
    /// </summary>
    Task<TEntity?> GetByIdAsync(object[] keyValues, CancellationToken ct = default);

    /// <summary>
    /// Find single entity matching predicate
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken ct = default);

    /// <summary>
    /// Get all entities matching optional predicate
    /// </summary>
    Task<List<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get paginated results
    /// </summary>
    Task<(List<TEntity> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Expression<Func<TEntity, bool>>? predicate = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        CancellationToken ct = default);

    /// <summary>
    /// Check if any entity matches predicate
    /// </summary>
    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default);

    /// <summary>
    /// Count entities matching optional predicate
    /// </summary>
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        CancellationToken ct = default);

    #endregion

    #region Command Operations

    /// <summary>
    /// Add new entity (tracked, not persisted until SaveChanges)
    /// </summary>
    void Add(TEntity entity);

    /// <summary>
    /// Add multiple entities
    /// </summary>
    void AddRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Update existing entity
    /// </summary>
    void Update(TEntity entity);

    /// <summary>
    /// Update multiple entities
    /// </summary>
    void UpdateRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Remove entity
    /// </summary>
    void Remove(TEntity entity);

    /// <summary>
    /// Remove multiple entities
    /// </summary>
    void RemoveRange(IEnumerable<TEntity> entities);

    #endregion

    #region Specification Pattern (Optional)

    /// <summary>
    /// Find entities using a specification pattern
    /// </summary>
    Task<List<TEntity>> FindAsync(
        ISpecification<TEntity> specification,
        CancellationToken ct = default);

    /// <summary>
    /// Count entities matching specification
    /// </summary>
    Task<int> CountAsync(
        ISpecification<TEntity> specification,
        CancellationToken ct = default);

    #endregion
  }

  /// <summary>
  /// Specification pattern interface for complex queries
  /// </summary>
  public interface ISpecification<T>
  {
    Expression<Func<T, bool>>? Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    List<string> IncludeStrings { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    int? Take { get; }
    int? Skip { get; }
    bool IsPagingEnabled { get; }
  }
}
