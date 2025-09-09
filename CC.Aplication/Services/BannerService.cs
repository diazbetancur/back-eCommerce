using AutoMapper;
using CC.Domain.Dto;
using CC.Domain.Entities;
using CC.Domain.Interfaces.Repositories;
using CC.Domain.Interfaces.Services;

namespace CC.Aplication.Services
{
    public class BannerService : ServiceBase<Banner, BannerDto>, IBannerService
    {
        public BannerService(IBannerRepository repository, IMapper mapper) : base(repository, mapper)
        {
        }
    }
}