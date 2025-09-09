using AutoMapper;
using CC.Domain.Dto;
using CC.Domain.Entities;
using CC.Domain.Interfaces.Repositories;
using CC.Domain.Interfaces.Services;

namespace CC.Aplication.Services
{
    public class CategoryService : ServiceBase<Category, CategoryDto>, ICategoryService
    {
        public CategoryService(ICategoryRepository repository, IMapper mapper) : base(repository, mapper)
        {
        }
    }
}