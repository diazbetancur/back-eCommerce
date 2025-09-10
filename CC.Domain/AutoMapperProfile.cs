using AutoMapper;
using CC.Domain.Dto;
using CC.Domain.Entities;

namespace CC.Domain
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<UserDto, User>().ReverseMap();
            CreateMap<Product, ProductDto>().ReverseMap();
            CreateMap<ProductCategory, ProductCategoryDto>().ReverseMap();
            CreateMap<Category, CategoryDto>().ReverseMap();
            CreateMap<ProductProperty, ProductPropertyDto>().ReverseMap();
            CreateMap<ProductImage, ProductImageDto>().ReverseMap();
            CreateMap<Banner, BannerDto>().ReverseMap();
            CreateMap<Billing, BillingDto>().ReverseMap();
            CreateMap<ProductsBilling, ProductsBillingDto>().ReverseMap();
            CreateMap<LoyalityBilling, LoyalityBillingDto>().ReverseMap();
        }
    }
}