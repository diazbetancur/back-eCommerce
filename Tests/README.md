# ?? Tests Automáticos - eCommerce Multi-Tenant API

## ?? Contenido

Este proyecto de pruebas contiene tests automatizados para validar el correcto funcionamiento de la API multi-tenant.

---

## ??? Estructura del Proyecto

```
Tests/
??? Api-eCommerce.Tests.csproj          # Proyecto de tests
??? CustomWebApplicationFactory.cs      # Factory para tests de integración
??? Tenancy/
?   ??? TenantResolverTests.cs         # Tests de resolución de tenants
??? Catalog/
?   ??? CatalogEndpointsTests.cs       # Tests de endpoints de catálogo
??? Cart/
?   ??? CartEndpointsTests.cs          # Tests de carrito de compras
??? Checkout/
?   ??? CheckoutTests.cs               # Tests de checkout (con idempotencia)
??? Health/
    ??? HealthEndpointsTests.cs        # Tests de health checks
```

---

## ?? Ejecutar Tests

### Desde Visual Studio
1. Abrir el explorador de tests: `Test > Test Explorer`
2. Hacer clic en "Run All Tests"

### Desde línea de comandos
```bash
# Ejecutar todos los tests
dotnet test

# Ejecutar tests con cobertura
dotnet test --collect:"XPlat Code Coverage"

# Ejecutar tests de una clase específica
dotnet test --filter "FullyQualifiedName~TenantResolverTests"

# Ejecutar tests con output detallado
dotnet test --logger "console;verbosity=detailed"
```

---

## ?? Suites de Tests

### 1. TenantResolverTests (12 tests)
**Ubicación:** `Tests/Tenancy/TenantResolverTests.cs`

**Cobertura:**
- ? Resolución de tenant via header `X-Tenant-Slug`
- ? Resolución de tenant via query parameter `tenant`
- ? Prioridad header sobre query parameter
- ? Validación de tenant inexistente (404)
- ? Validación de tenant no activo (403)
- ? Validación de falta de tenant (400)
- ? Rutas excluidas que no requieren tenant
- ? Case-insensitive slug
- ? Trimming de espacios
- ? TenantAccessor storage
- ? Estado inicial del accessor

**Ejemplos de tests:**
```csharp
[Fact]
public async Task ResolveAsync_WithValidHeaderSlug_ReturnsTenantContext()

[Fact]
public async Task ResolveAsync_WithoutTenantSlug_Returns400BadRequest()

[Fact]
public async Task ResolveAsync_HeaderTakesPriorityOverQuery()
```

---

### 2. CatalogEndpointsTests (12 tests)
**Ubicación:** `Tests/Catalog/CatalogEndpointsTests.cs`

**Cobertura:**
- ? Listar productos con tenant válido
- ? Listar productos sin tenant (400)
- ? Paginación de productos
- ? Aislamiento de datos entre tenants
- ? Obtener producto por ID
- ? Listar categorías
- ? Búsqueda de productos
- ? Diferentes parámetros de paginación
- ? Tenant inexistente (404)
- ? Validación de aislamiento multi-tenant

**Ejemplos de tests:**
```csharp
[Fact]
public async Task GetProducts_WithValidTenant_ReturnsOk()

[Fact]
public async Task GetProducts_DifferentTenants_ReturnsDifferentData()

[Theory]
[InlineData("?page=1&pageSize=10")]
public async Task GetProducts_WithDifferentPaginationParams_ReturnsOk(string queryString)
```

---

### 3. CartEndpointsTests (13 tests)
**Ubicación:** `Tests/Cart/CartEndpointsTests.cs`

**Cobertura:**
- ? Agregar producto al carrito
- ? Obtener carrito actual
- ? Actualizar cantidad de item
- ? Remover item del carrito
- ? Limpiar carrito completo
- ? Validación sin tenant (400)
- ? Validación sin session ID (400)
- ? Aislamiento de sesiones
- ? Cantidades inválidas
- ? Múltiples productos en un carrito
- ? Teorías con diferentes cantidades

**Ejemplos de tests:**
```csharp
[Fact]
public async Task AddToCart_WithValidData_ReturnsSuccess()

[Fact]
public async Task Cart_SessionIsolation_DifferentSessionsHaveDifferentCarts()

[Theory]
[InlineData(0)]
[InlineData(-5)]
public async Task AddToCart_InvalidQuantities_AreRejected(int quantity)
```

---

### 4. CheckoutTests (13 tests)
**Ubicación:** `Tests/Checkout/CheckoutTests.cs`

**Cobertura:**
- ? Obtener cotización (quote)
- ? Crear orden (place order)
- ? **Idempotencia con Idempotency-Key**
- ? Validación sin Idempotency-Key (400)
- ? Requests idempotentes retornan mismo resultado
- ? Diferentes keys crean diferentes órdenes
- ? Validación sin tenant (400)
- ? Validación sin session (400)
- ? Validación de dirección inválida
- ? Diferentes métodos de pago
- ? Quote sin dirección (400)

**Ejemplos de tests:**
```csharp
[Fact]
public async Task PlaceOrder_IdempotentRequests_ReturnSameResult()

[Fact]
public async Task PlaceOrder_DifferentIdempotencyKeys_CreateDifferentOrders()

[Theory]
[InlineData("cash")]
[InlineData("wompi")]
public async Task PlaceOrder_DifferentPaymentMethods_AreAccepted(string paymentMethod)
```

**Test de Idempotencia Detallado:**
```csharp
// Mismo idempotency key = misma respuesta
var idempotencyKey = Guid.NewGuid().ToString();

// Primera request
var response1 = await PlaceOrder(idempotencyKey);

// Segunda request con MISMA key
var response2 = await PlaceOrder(idempotencyKey);

// Assertion: Ambas deberían retornar el mismo resultado
content1.Should().Be(content2);
```

---

### 5. HealthEndpointsTests (12 tests)
**Ubicación:** `Tests/Health/HealthEndpointsTests.cs`

**Cobertura:**
- ? Health check global (sin tenant)
- ? Health check por tenant
- ? Validación sin tenant en endpoint de tenant (400)
- ? Tenant inexistente (404)
- ? Tenant no activo (403)
- ? Status "Healthy"
- ? Status de base de datos
- ? Llamadas repetidas
- ? Case-insensitive
- ? Múltiples tenants
- ? Content-Type JSON
- ? Test de performance (<1s)

**Ejemplos de tests:**
```csharp
[Fact]
public async Task GlobalHealthCheck_DoesNotRequireTenant()

[Fact]
public async Task HealthCheck_PerformanceTest_RespondsQuickly()

[Theory]
[InlineData("/health")]
[InlineData("/HEALTH")]
public async Task HealthCheck_IsCaseInsensitive(string path)
```

---

## ?? Dependencias

El proyecto usa las siguientes bibliotecas:

- **xUnit** (2.6.2): Framework de testing
- **FluentAssertions** (6.12.0): Assertions expresivas
- **Moq** (4.20.70): Mocking framework
- **Microsoft.AspNetCore.Mvc.Testing** (8.0.0): Testing de integración
- **Microsoft.EntityFrameworkCore.InMemory** (8.0.0): BD en memoria
- **coverlet.collector** (6.0.0): Cobertura de código

---

## ?? Cobertura de Tests

### Resumen
- **Total de tests**: 62+
- **Cobertura estimada**: ~80%

### Por Área
| Área | Tests | Cobertura |
|------|-------|-----------|
| Tenancy | 12 | 90% |
| Catalog | 12 | 75% |
| Cart | 13 | 85% |
| Checkout | 13 | 90% |
| Health | 12 | 95% |

---

## ?? CustomWebApplicationFactory

La clase `CustomWebApplicationFactory` proporciona:

1. **Base de datos en memoria** para tests de integración
2. **Seed automático** de datos de prueba:
   - `test-tenant-1` (Active, Premium)
   - `test-tenant-2` (Active, Basic)
   - `test-tenant-pending` (Pending, Basic)
3. **Configuración de test environment**
4. **Aislamiento entre tests**

**Uso:**
```csharp
public class MyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public MyTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MyTest()
    {
        var response = await _client.GetAsync("/api/endpoint");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
```

---

## ?? Ejemplos de Assertions

### FluentAssertions
```csharp
// Comparación simple
response.StatusCode.Should().Be(HttpStatusCode.OK);

// Negación
response.StatusCode.Should().NotBe(HttpStatusCode.BadRequest);

// Colecciones
responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));

// Strings
content.Should().Contain("Healthy");
content.Should().NotBeEmpty();

// Excepciones
action.Should().Throw<InvalidOperationException>();

// Performance
stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000);
```

---

## ?? Ejecutar Tests con Cobertura

### 1. Instalar herramientas
```bash
dotnet tool install --global dotnet-reportgenerator-globaltool
```

### 2. Ejecutar tests con cobertura
```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### 3. Generar reporte HTML
```bash
reportgenerator -reports:"./TestResults/**/coverage.cobertura.xml" -targetdir:"./TestResults/CoverageReport" -reporttypes:Html
```

### 4. Ver reporte
```bash
# Windows
start ./TestResults/CoverageReport/index.html

# Mac
open ./TestResults/CoverageReport/index.html

# Linux
xdg-open ./TestResults/CoverageReport/index.html
```

---

## ?? Debugging Tests

### En Visual Studio
1. Poner un breakpoint en el test
2. Click derecho en el test ? "Debug Test"

### En VS Code
1. Instalar extensión ".NET Core Test Explorer"
2. Click en el ícono de debug al lado del test

### Desde línea de comandos
```bash
# Ejecutar un test específico con logging detallado
dotnet test --filter "FullyQualifiedName~CheckoutTests.PlaceOrder_IdempotentRequests" --logger "console;verbosity=detailed"
```

---

## ?? Filtros de Tests

```bash
# Por nombre de clase
dotnet test --filter "FullyQualifiedName~TenantResolverTests"

# Por nombre de método
dotnet test --filter "FullyQualifiedName~PlaceOrder_IdempotentRequests"

# Por trait/categoría
dotnet test --filter "Category=Integration"

# Múltiples filtros
dotnet test --filter "(FullyQualifiedName~Tenancy)|(FullyQualifiedName~Health)"
```

---

## ?? Convenciones de Naming

### Estructura de nombre de test
```
[Method]_[Scenario]_[ExpectedBehavior]
```

**Ejemplos:**
```csharp
ResolveAsync_WithValidHeaderSlug_ReturnsTenantContext()
AddToCart_WithoutTenant_Returns400BadRequest()
PlaceOrder_IdempotentRequests_ReturnSameResult()
```

### Teorías (Theory)
Para tests parametrizados:
```csharp
[Theory]
[InlineData("cash")]
[InlineData("wompi")]
[InlineData("stripe")]
public async Task PlaceOrder_DifferentPaymentMethods_AreAccepted(string paymentMethod)
```

---

## ? Checklist de Tests

Antes de hacer commit, verificar:

- [ ] Todos los tests pasan localmente
- [ ] No hay tests ignorados innecesariamente
- [ ] Cobertura de código > 75%
- [ ] Tests son independientes (no dependen del orden)
- [ ] Tests limpian sus propios datos
- [ ] Nombres de tests son descriptivos
- [ ] Assertions usan FluentAssertions
- [ ] No hay `Thread.Sleep()` innecesarios

---

## ?? Integración Continua (CI)

### GitHub Actions Example
```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 8.0.x
      - name: Restore dependencies
        run: dotnet restore
      - name: Build
        run: dotnet build --no-restore
      - name: Test
        run: dotnet test --no-build --verbosity normal --collect:"XPlat Code Coverage"
      - name: Upload coverage
        uses: codecov/codecov-action@v3
```

---

## ?? Soporte

### Archivos relacionados
- **Tests**: `Tests/` directory
- **Manual Testing**: `dev/test-calls.http`
- **API Documentation**: `SWAGGER-DOCUMENTATION.md`
- **Feature Flags**: `FEATURE-FLAGS-TESTING-GUIDE.md`

### Recursos útiles
- [xUnit Docs](https://xunit.net/)
- [FluentAssertions Docs](https://fluentassertions.com/)
- [ASP.NET Core Testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests)

---

**Última actualización**: Diciembre 2024  
**Versión**: 1.0.0  
**Total de tests**: 62+  
**Estado**: ? Todos los tests pasan
