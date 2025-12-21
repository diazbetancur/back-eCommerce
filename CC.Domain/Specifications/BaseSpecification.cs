using System.Linq.Expressions;
using CC.Domain.Interfaces.Repositories;

namespace CC.Domain.Specifications
{
  /// <summary>
  /// Base class for implementing Specification pattern
  /// </summary>
  public abstract class BaseSpecification<T> : ISpecification<T>
  {
    public Expression<Func<T, bool>>? Criteria { get; protected set; }
    public List<Expression<Func<T, object>>> Includes { get; } = new();
    public List<string> IncludeStrings { get; } = new();
    public Expression<Func<T, object>>? OrderBy { get; protected set; }
    public Expression<Func<T, object>>? OrderByDescending { get; protected set; }
    public int? Take { get; protected set; }
    public int? Skip { get; protected set; }
    public bool IsPagingEnabled { get; protected set; }

    protected BaseSpecification() { }

    protected BaseSpecification(Expression<Func<T, bool>> criteria)
    {
      Criteria = criteria;
    }

    protected void AddInclude(Expression<Func<T, object>> includeExpression)
    {
      Includes.Add(includeExpression);
    }

    protected void AddInclude(string includeString)
    {
      IncludeStrings.Add(includeString);
    }

    protected void ApplyOrderBy(Expression<Func<T, object>> orderByExpression)
    {
      OrderBy = orderByExpression;
    }

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescendingExpression)
    {
      OrderByDescending = orderByDescendingExpression;
    }

    protected void ApplyPaging(int skip, int take)
    {
      Skip = skip;
      Take = take;
      IsPagingEnabled = true;
    }
  }

  #region Common Specifications

  /// <summary>
  /// Specification for active/enabled entities
  /// </summary>
  public class ActiveEntitySpecification<T> : BaseSpecification<T> where T : class
  {
    public ActiveEntitySpecification(Expression<Func<T, bool>> isActivePredicate)
        : base(isActivePredicate)
    {
    }
  }

  /// <summary>
  /// Specification for paginated results
  /// </summary>
  public class PaginatedSpecification<T> : BaseSpecification<T> where T : class
  {
    public PaginatedSpecification(int page, int pageSize)
    {
      ApplyPaging((page - 1) * pageSize, pageSize);
    }

    public PaginatedSpecification(int page, int pageSize, Expression<Func<T, bool>> criteria)
        : base(criteria)
    {
      ApplyPaging((page - 1) * pageSize, pageSize);
    }
  }

  #endregion
}
