# Fase 1 - Ejecucion de Lotes Tecnicos

## Lote 01

- Fecha: 2026-04-13
- Objetivo del lote:
  - validar uso real de Microsoft.AspNet.WebApi.Core
  - ejecutar retiro seguro si no habia dependencia funcional real
  - revisar duplicidad de PagedResult<T>
  - unificar solo si el cambio era pequeno y seguro

## Hallazgos

1. Microsoft.AspNet.WebApi.Core tenia un unico acoplamiento tecnico directo:

- Api-eCommerce/Handlers/ErrorHandlingMiddleware.cs usaba System.Web.Http solo para validar el tipo HttpResponseException.
- No se encontraron throws de HttpResponseException en el repo.

2. PagedResult<T> estaba duplicado:

- CC.Aplication/Catalog/ProductService.cs
- CC.Infraestructure/Tenant/Extensions/QueryableExtensions.cs

3. La unificacion era segura en este lote porque:

- ambas implementaciones tenian el mismo proposito y la misma forma funcional.
- se pudo extraer un tipo canonico compartido sin tocar contratos HTTP ni logica de endpoints.

## Archivos revisados

- Api-eCommerce/Api-eCommerce.csproj
- Api-eCommerce/Handlers/ErrorHandlingMiddleware.cs
- CC.Aplication/Catalog/ProductService.cs
- CC.Infraestructure/Tenant/Extensions/QueryableExtensions.cs
- Api-eCommerce/Controllers/ProductController.cs
- docs/architecture/ARCHITECTURE-ALIGNMENT-PHASE0.md

## Cambios aplicados

1. Retiro de dependencia legacy de paquete:

- eliminado PackageReference Microsoft.AspNet.WebApi.Core en Api-eCommerce/Api-eCommerce.csproj.

2. Limpieza del unico acoplamiento tecnico asociado:

- removido using System.Web.Http de Api-eCommerce/Handlers/ErrorHandlingMiddleware.cs.
- removidas validaciones por HttpResponseException que no tenian uso real.

3. Unificacion de PagedResult<T>:

- agregado tipo canonico en CC.Domain/Dto/PagedResult.cs.
- removida clase duplicada de CC.Aplication/Catalog/ProductService.cs.
- removida clase duplicada de CC.Infraestructure/Tenant/Extensions/QueryableExtensions.cs.
- QueryableExtensions ahora usa CC.Domain.Dto.PagedResult<T>.

## Cambios descartados por riesgo

- No se cambio logica funcional de endpoints.
- No se refactorizaron archivos grandes de API.
- No se tocaron contratos HTTP activos.
- No se modifico flujo de multi-tenancy.
- No se forzo manejo nuevo de UnauthorizedAccessException en ErrorHandlingMiddleware para evitar ampliar alcance funcional en este lote.

## Pendientes marcados para siguiente lote

- Revisar estandar unico de manejo de errores 401/403 en middleware y endpoints (pendiente para siguiente lote de Fase 1).
- Auditar clases/utilidades no usadas en CC.Infraestructure/Tenant/Extensions para limpieza adicional (pendiente para siguiente lote de Fase 1).
- Continuar sellado de dependencias Application -> Infrastructure por modulo piloto (pendiente de Fase 1, lote posterior).

## Lote 02

- Fecha: 2026-04-13
- Objetivo del lote:
  - inspeccionar uso real de DependencyInyectionHandler
  - clarificar composicion DI entre Program.cs y pipeline legacy
  - reducir ambiguedad de registros en runtime sin romper el backend

## Archivos revisados

- Api-eCommerce/Program.cs
- Api-eCommerce/Handlers/DependencyInyectionHandler.cs
- Api-eCommerce/Handlers/ActivityLoggingMiddleware.cs
- Api-eCommerce/Controllers/ProductController.cs
- docs/architecture/ARCHITECTURE-ALIGNMENT-PHASE0.md

## Hallazgos DI legacy/moderno

1. Program.cs era composition root moderno, pero seguia delegando un bloque legacy completo a DependencyInyectionHandler.
2. No se encontraron consumidores activos en API para interfaces legacy registradas en el handler (`CC.Domain.Interfaces.Services.*` y repositorios base).
3. Si se retiraba el handler sin mas cambios, se perdian tres dependencias de runtime aun usadas:

- `DBContext` (usado por ActivityLoggingMiddleware)
- `ExceptionControl`
- `Serilog.ILogger` (usado por ErrorHandlingMiddleware)

## Cambios aplicados

1. Program.cs ahora registra explicitamente los servicios legacy minimos necesarios para runtime:

- `AddDbContext<DBContext>`
- `AddSingleton<ExceptionControl>`
- `AddSingleton<Serilog.ILogger>`

2. Se elimino la invocacion de `DependencyInyectionHandler.DepencyInyectionConfig(...)` para reducir ambiguedad en la composicion.
3. `DependencyInyectionHandler` quedo marcado como legacy transitorio con atributo `Obsolete`, dejando claro que no es la fuente activa de DI.

## Cambios no aplicados por riesgo

- No se eliminaron clases legacy (`ServiceBase`, `ERepositoryBase`, `DBContext`) porque todavia existe uso de `DBContext` en middleware de actividad.
- No se retiro ActivityLoggingMiddleware ni se cambio su logica funcional.
- No se eliminaron artefactos de migraciones legacy en este lote.

## Pendientes para siguiente lote

- Migrar o retirar dependencia de `DBContext` en ActivityLoggingMiddleware para poder desactivar el pipeline legacy restante (pendiente para siguiente lote de Fase 1).
- Revisar retiro progresivo de registros legacy que hoy no tienen consumidores activos, con validacion por pruebas (pendiente para siguiente lote de Fase 1).
- Continuar sellado Application -> Infrastructure por modulos piloto (pendiente para Fase 1, lote posterior).
