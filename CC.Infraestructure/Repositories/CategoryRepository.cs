using CC.Domain.Entities;
using CC.Domain.Interfaces.Repositories;

namespace CC.Infraestructure.Repositories
{
    public class CategoryRepository : ERepositoryBase<Category>, ICategoryRepository
    {
        public CategoryRepository(IQueryableUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }
}