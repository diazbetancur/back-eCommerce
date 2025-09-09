using CC.Domain.Entities;
using CC.Domain.Interfaces.Repositories;

namespace CC.Infraestructure.Repositories
{
    public class BannerRepository : ERepositoryBase<Banner>, IBannerRepository
    {
        public BannerRepository(IQueryableUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }
    }
}