using CC.Domain.Dto;
using CC.Domain.Entities;
using CC.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Api_eCommerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IConfiguration _configuration;

        public ProductController(IProductService productService, IFileStorageService fileStorageService, IConfiguration configuration)
        {
            _productService = productService;
            //_fileStorageService = fileStorageService;
            _configuration = configuration;
        }

        /// <summary>
        /// GET api/Category
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            return Ok(await _productService.GetAllAsync(x => !x.IsDeleted).ConfigureAwait(false));
        }
    }
}