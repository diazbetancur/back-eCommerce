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
    public class CategoryController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly IFileStorageService _fileStorageService;
        private readonly IConfiguration _configuration;

        public CategoryController(ICategoryService categoryService, IFileStorageService fileStorageService, IConfiguration configuration)
        {
            _categoryService = categoryService;
            _fileStorageService = fileStorageService;
            _configuration = configuration;
        }

        /// <summary>
        /// GET api/Category
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            return Ok(await _categoryService.GetAllAsync(x => x.IsActive).ConfigureAwait(false));
        }

        /// <summary>
        /// POST api/Category
        /// </summary>
        /// <param name="CategoryRequestDto"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] CategoryRequestDto request)
        {
            var bucketName = _configuration["GoogleStorage:BucketName"];
            var imageUrl = await _fileStorageService.UploadFileAsync(
                bucketName,
                request.ImageName,
                request.ImageBytes,
                "image/png"
            ).ConfigureAwait(false);

            if (string.IsNullOrEmpty(imageUrl))
            {
                return BadRequest("No se logro cargar la imagen, por favor intentarlo nuevamente.");
            }
            var categoryDto = new CategoryDto
            {
                Name = request.Name,
                Description = request.Description,
                Icon = imageUrl,
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive,
                ImageName = request.ImageName
            };
            var result = await _categoryService.AddAsync(categoryDto).ConfigureAwait(false);
            return Ok(result);
        }

        /// <summary>
        /// PUT api/Category
        /// </summary>
        /// <param name="CategoryRequestDto"></param>
        /// <returns></returns>
        [HttpPut]
        public async Task<IActionResult> Put([FromBody] CategoryRequestDto request)
        {
            var bucketName = _configuration["GoogleStorage:BucketName"];
            string imageUrl = null;
            if (request.ImageBytes != null && request.ImageBytes.Length > 0)
            {
                imageUrl = await _fileStorageService.UpdateFileAsync(
                    bucketName,
                    request.ImageName,
                    request.ImageBytes,
                    "image/png"
                ).ConfigureAwait(false);
            }
            if (string.IsNullOrEmpty(imageUrl))
            {
                return BadRequest("No se logro actualizar la imagen, por favor intentarlo nuevamente.");
            }
            var categoryDto = new CategoryDto
            {
                Id = request.Id,
                Name = request.Name,
                Description = request.Description,
                Icon = imageUrl,
                DisplayOrder = request.DisplayOrder,
                IsActive = request.IsActive
            };
            await _categoryService.UpdateAsync(categoryDto).ConfigureAwait(false);
            return Ok(categoryDto);
        }

        /// <summary>
        /// DELETE api/Category/{id}
        /// </summary>
        /// <param name="id"></param>"
        /// <rturns></returns>
        ///
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var category = await _categoryService.FindByIdAsync(id).ConfigureAwait(false);
            if (category == null)
            {
                return NotFound("Categoria no encontrada.");
            }
            var bucketName = _configuration["GoogleStorage:BucketName"];
            await _fileStorageService.DeleteFileAsync(bucketName, category.ImageName).ConfigureAwait(false);
            try
            {
                await _categoryService.DeleteAsync(category).ConfigureAwait(false);
            }
            catch
            {
                category.IsActive = false;
                await _categoryService.UpdateAsync(category).ConfigureAwait(false);
            }
            return Ok("Categoria eliminada correctamente.");
        }
    }
}