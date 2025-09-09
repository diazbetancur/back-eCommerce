using AutoMapper;
using CC.Domain.Dto;
using CC.Domain.Entities;
using CC.Domain.Interfaces.Repositories;
using CC.Domain.Interfaces.Services;

namespace CC.Aplication.Services
{
    public class ProductService : ServiceBase<Product, ProductDto>, IProductService
    {
        public ProductService(IProductRepository repository, IMapper mapper) : base(repository, mapper)
        {
        }
    }
}