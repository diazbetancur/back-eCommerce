# ADR-001 - Arquitectura objetivo y reglas de dependencia

- Estado: Propuesto (Fase 0)
- Fecha: 2026-04-13
- Decisor: Equipo backend

## Contexto

La solucion opera con arquitectura hibrida en transicion:

- Application depende de Infrastructure
- API contiene acceso de datos directo
- conviven componentes modernos y legacy sin frontera estricta

En esta etapa de retomada, el objetivo es alinear base tecnica y retirar legacy temprano, sin reescritura total.

## Problema

Sin reglas de dependencia y gobernanza aplicables, el sistema seguira degradandose y cada feature futura sera mas costosa y riesgosa.

## Decision

Adoptar arquitectura objetivo pragmatica en capas, con reglas de dependencia obligatorias y control de excepciones por ADR.

## Capas objetivo

- Domain: reglas de negocio puras, sin concerns de infraestructura ni HTTP.
- Application: casos de uso, contratos de caso de uso, puertos y politicas funcionales.
- Infrastructure: implementaciones de puertos, persistencia EF y proveedores externos.
- API: adaptador HTTP, authn/authz, binding y composition root.

## Reglas de dependencia

1. Domain no depende de Application, Infrastructure ni API.
2. Application depende de Domain y abstracciones; no de implementaciones de Infrastructure.
3. Infrastructure implementa puertos para casos de uso; no define logica funcional de negocio.
4. API puede componer servicios, pero no alojar reglas de negocio ni consultas de datos.

## Regla de DTOs

1. DTO HTTP en API.
2. DTO de caso de uso en Application.
3. Domain no contiene DTOs de transporte.

## Regla de acceso a DbContext

1. Solo Infrastructure accede a DbContext/DbSet/queries EF.
2. API no inyecta DbContext ni TenantDbContextFactory para logica funcional.
3. Application no usa tipos concretos de Infrastructure para persistencia.

## Regla multi-tenant

1. Tenant se resuelve en middleware de borde.
2. Tenant viaja por ITenantContext/ITenantAccessor scoped.
3. Prohibido resolver tenant dentro de controllers/endpoints/repositorios de negocio.

## Consecuencias

Positivas:

- menor acoplamiento
- mayor testabilidad
- menor riesgo de regresion
- mejor trazabilidad de responsabilidades

Costes:

- refactor incremental de composicion y extraccion de logica HTTP
- ajustes progresivos de contratos internos

## Excepciones permitidas

Solo con ADR de excepcion aprobado que incluya:

- motivo tecnico concreto
- riesgo acotado
- owner
- fecha de expiracion (maximo 2 sprints)

Sin ADR aprobado no hay excepcion.
