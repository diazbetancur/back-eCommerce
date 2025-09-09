using CC.Domain.Entities;
using CC.Domain.Interfaces.Repositories;

namespace CC.Infraestructure.Repositories
{
    public class ProductRepository : ERepositoryBase<Product>, IProductRepository
    {
        public ProductRepository(IQueryableUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }
}