# Fase 1 - Lote 2 DI Cleanup

- Fecha: 2026-04-13
- Alcance: limpieza pequena y segura de composicion DI
- Objetivo: reducir ambiguedad entre DI moderno y DI legacy sin romper runtime

## Mapa resumido de composicion actual

### Composition root activo

- `Api-eCommerce/Program.cs`

### Servicios modernos registrados en Program

- servicios admin
- servicios tenant/business
- tenancy, auth, middleware pipeline, swagger

### Servicios legacy transitorios aun activos en Program

- `DBContext` (legacy)
- `ExceptionControl`
- `Serilog.ILogger` para `ErrorHandlingMiddleware`

## Estado de DependencyInyectionHandler

- Estado actual: aislado (ya no invocado desde Program)
- Archivo: `Api-eCommerce/Handlers/DependencyInyectionHandler.cs`
- Marcado como `Obsolete` para dejar trazabilidad de retiro
- Rol actual: referencia transitoria para siguientes lotes, no composition root activo

## Registros duplicados/en conflicto detectados

1. Mezcla de registro DI moderno (Program) + bloque legacy monolitico (handler)

- Decision: eliminar invocacion del handler para evitar doble fuente de verdad.

2. Registros legacy sin consumidores directos en API

- `ICategoryService` (legacy), `IProductService` (legacy), repositorios legacy
- Decision: no registrar via handler en runtime actual.

## Decisiones tomadas

1. Opcion aplicada: Reducir (Opcion B)

- se movieron solo registros simples y necesarios para estabilidad runtime a Program.
- se corto invocacion global del handler.

2. Fuente de verdad para DI

- Program.cs queda como unica fuente de composicion runtime.

## Que quedo vivo temporalmente

- `DBContext` sigue vivo por dependencia de `ActivityLoggingMiddleware`.
- `ExceptionControl` y `Serilog.ILogger` siguen vivos por `ErrorHandlingMiddleware`.

## Que se intentara en el siguiente lote

- sacar `ActivityLoggingMiddleware` de `DBContext` legacy o cambiarlo a flujo moderno equivalente.
- evaluar retiro definitivo de `DependencyInyectionHandler` y registros legacy residuales.
- mantener cambios pequenos, sin tocar contratos HTTP ni logica de negocio.
