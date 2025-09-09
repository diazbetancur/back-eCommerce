using CC.Aplication.Services;
using CC.Domain.Interfaces.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BannerController : ControllerBase
    {
        private readonly IBannerService _bannerService;

        public BannerController(IBannerService bannerService)
        {
            _bannerService = bannerService;
        }

        /// <summary>
        /// GET api/Banner
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            return Ok(await _bannerService
                .GetAllAsync(x =>
                    x.IsActive &&
                    (x.StartDate == null || x.StartDate <= DateTime.UtcNow) &&
                    (x.EndDate == null || x.EndDate >= DateTime.UtcNow))
                .ConfigureAwait(false));
        }
    }
}