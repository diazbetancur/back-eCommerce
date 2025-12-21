using CC.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Api_eCommerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IConfiguration _configuration;

        public ProductController(IProductService productService, IConfiguration configuration)
        {
            _productService = productService;
            _configuration = configuration;
        }

        /// <summary>
        /// GET api/Product
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var include = "ProductCategories.Category,ProductImages";
            return Ok(await _productService.GetAllAsync(x => !x.IsDeleted, includeProperties: include).ConfigureAwait(false));
        }
    }
}