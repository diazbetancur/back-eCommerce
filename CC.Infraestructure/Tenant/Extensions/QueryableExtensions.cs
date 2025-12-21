using Microsoft.EntityFrameworkCore;

namespace CC.Infraestructure.Tenant.Extensions
{
  /// <summary>
  /// Extension methods for IQueryable to simplify common operations
  /// </summary>
  public static class QueryableExtensions
  {
    /// <summary>
    /// Apply pagination to a query
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken ct = default) where T : class
    {
      var totalCount = await query.CountAsync(ct);

      var items = await query
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync(ct);

      return new PagedResult<T>
      {
        Items = items,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize,
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
      };
    }

    /// <summary>
    /// Apply pagination with ordering to a query
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T, TKey>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        System.Linq.Expressions.Expression<Func<T, TKey>> orderBy,
        bool descending = false,
        CancellationToken ct = default) where T : class
    {
      var totalCount = await query.CountAsync(ct);

      var orderedQuery = descending
          ? query.OrderByDescending(orderBy)
          : query.OrderBy(orderBy);

      var items = await orderedQuery
          .Skip((page - 1) * pageSize)
          .Take(pageSize)
          .ToListAsync(ct);

      return new PagedResult<T>
      {
        Items = items,
        TotalCount = totalCount,
        Page = page,
        PageSize = pageSize,
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
      };
    }

    /// <summary>
    /// Get first or throw with custom exception
    /// </summary>
    public static async Task<T> FirstOrThrowAsync<T>(
        this IQueryable<T> query,
        string errorMessage = "Entity not found",
        CancellationToken ct = default) where T : class
    {
      var entity = await query.FirstOrDefaultAsync(ct);

      if (entity == null)
      {
        throw new InvalidOperationException(errorMessage);
      }

      return entity;
    }

    /// <summary>
    /// Get first or throw with custom exception
    /// </summary>
    public static async Task<T> FirstOrThrowAsync<T>(
        this IQueryable<T> query,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate,
        string errorMessage = "Entity not found",
        CancellationToken ct = default) where T : class
    {
      var entity = await query.FirstOrDefaultAsync(predicate, ct);

      if (entity == null)
      {
        throw new InvalidOperationException(errorMessage);
      }

      return entity;
    }

    /// <summary>
    /// Apply optional where condition
    /// </summary>
    public static IQueryable<T> WhereIf<T>(
        this IQueryable<T> query,
        bool condition,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate)
    {
      return condition ? query.Where(predicate) : query;
    }

    /// <summary>
    /// Apply optional string filter
    /// </summary>
    public static IQueryable<T> WhereIfNotEmpty<T>(
        this IQueryable<T> query,
        string? value,
        System.Linq.Expressions.Expression<Func<T, bool>> predicate)
    {
      return string.IsNullOrWhiteSpace(value) ? query : query.Where(predicate);
    }
  }

  /// <summary>
  /// Standard paginated result container
  /// </summary>
  public class PagedResult<T>
  {
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
  }
}
