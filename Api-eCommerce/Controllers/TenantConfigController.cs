using CC.Aplication.Services;
using CC.Domain.Dto;
using CC.Infraestructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api_eCommerce.Controllers
{
  /// <summary>
  /// Configuración del tenant autenticado para el frontend
  /// </summary>
  [ApiController]
  [Route("api/tenant-config")]
  [Authorize]
  public class TenantConfigController : ControllerBase
  {
    private readonly IFeatureService _featureService;
    private readonly ITenantAccessor _tenantAccessor;
    private readonly ILogger<TenantConfigController> _logger;

    public TenantConfigController(
        IFeatureService featureService,
        ITenantAccessor tenantAccessor,
        ILogger<TenantConfigController> logger)
    {
      _featureService = featureService;
      _tenantAccessor = tenantAccessor;
      _logger = logger;
    }

    /// <summary>
    /// Obtiene la configuración y feature flags del tenant autenticado
    /// </summary>
    /// <remarks>
    /// Este endpoint devuelve los feature flags y configuración del tenant del usuario autenticado.
    /// Los feature flags se cachean por 15 minutos para optimizar rendimiento.
    /// 
    /// Ejemplo de respuesta:
    /// ```json
    /// {
    ///   "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    ///   "tenantSlug": "mi-tienda",
    ///   "tenantName": "Mi Tienda Online",
    ///   "features": {
    ///     "loyalty": true,
    ///     "multistore": true,
    ///     "paymentsWompiEnabled": true,
    ///     "allowGuestCheckout": true,
    ///     "showStock": true,
    ///     "enableReviews": true,
    ///     "enableAdvancedSearch": true,
    ///     "enableAnalytics": false
    ///   }
    /// }
    /// ```
    /// </remarks>
    /// <returns>Configuración del tenant con feature flags</returns>
    /// <response code="200">Configuración obtenida exitosamente</response>
    /// <response code="401">No autenticado</response>
    /// <response code="404">Tenant no encontrado</response>
    [HttpGet]
    [ProducesResponseType(typeof(TenantConfigResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantConfigResponse>> GetTenantConfig()
    {
      try
      {
        // Obtener información del tenant desde el accessor
        if (!_tenantAccessor.HasTenant || _tenantAccessor.TenantInfo == null)
        {
          _logger.LogWarning("Tenant not found in context for authenticated user");
          return NotFound(new { error = "Tenant not found" });
        }

        var tenantInfo = _tenantAccessor.TenantInfo;

        // Obtener feature flags desde el servicio (usa cache automático)
        var features = await _featureService.GetFeaturesAsync();

        var response = new TenantConfigResponse
        {
          TenantId = tenantInfo.Id,
          TenantSlug = tenantInfo.Slug,
          TenantName = tenantInfo.Slug, // Usar slug como nombre por ahora
          Features = new TenantFeaturesDto
          {
            // Loyalty = wishlist/favorites
            Loyalty = features.EnableWishlist,

            // Multistore = soporte de variantes
            Multistore = features.HasVariants,

            // Wompi payment gateway
            PaymentsWompiEnabled = features.Payments.WompiEnabled,

            // Guest checkout
            AllowGuestCheckout = features.AllowGuestCheckout,

            // Stock visibility
            ShowStock = features.ShowStock,

            // Reviews
            EnableReviews = features.EnableReviews,

            // Advanced search
            EnableAdvancedSearch = features.EnableAdvancedSearch,

            // Analytics
            EnableAnalytics = features.EnableAnalytics
          }
        };

        _logger.LogInformation(
            "Tenant config retrieved for {TenantSlug} (ID: {TenantId})",
            tenantInfo.Slug,
            tenantInfo.Id
        );

        return Ok(response);
      }
      catch (Exception ex)
      {
        _logger.LogError(
            ex,
            "Error retrieving tenant config"
        );
        return StatusCode(500, new { error = "Internal server error" });
      }
    }
  }
}
