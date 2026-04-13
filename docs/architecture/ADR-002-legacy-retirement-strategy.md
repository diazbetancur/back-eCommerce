# ADR-002 - Estrategia de retiro progresivo de legacy

- Estado: Propuesto (Fase 0)
- Fecha: 2026-04-13
- Decisor: Equipo backend

## Contexto

El backend esta en etapa de retomada. La estrategia no es convivencia larga con legacy; la estrategia es retiro temprano, progresivo y controlado.

Existe base moderna suficiente para iniciar esta salida sin reescritura total:

- middleware de tenant
- AdminDb/TenantDb
- base de pruebas

## Problema

El legacy sin clasificacion ni fecha de salida tiende a permanecer indefinidamente y contamina nuevas implementaciones.

## Decision

Todo componente legacy identificado se clasifica obligatoriamente en:

- eliminar
- reemplazar
- absorber
- conservar temporalmente

No se permite estado indefinido.

## Que se considera legacy

Se considera legacy cualquier componente que cumpla una o mas condiciones:

1. pertenece al pipeline antiguo (DependencyInyectionHandler, DBContext, ServiceBase/RepositoryBase)
2. duplica comportamiento moderno
3. rompe reglas de dependencia
4. resuelve tenant o acceso a datos fuera de frontera definida
5. introduce dependencia no alineada con net8

## Politica de eliminacion/reemplazo/absorcion

- Eliminar: no tiene uso real vigente o valor funcional.
- Reemplazar: tiene uso real, pero existe alternativa moderna definida.
- Absorber: logica valida esta en capa incorrecta; se mueve y limpia.
- Conservar temporalmente: solo por riesgo operativo alto y con salida definida.

## Regla de no permanencia indefinida

Todo componente conservado temporalmente debe tener:

- owner tecnico
- fecha limite de salida
- criterio de retiro verificable

Si falta alguno, el componente queda en incumplimiento.

## Excepciones permitidas

Solo por criticidad operativa demostrable y con ADR de excepcion. Validez maxima: 2 sprints y revalidacion obligatoria.

## Criterios para considerar componente retirado

1. no esta registrado en DI de runtime
2. no tiene referencias activas en codigo productivo
3. no participa en rutas activas
4. build y pruebas criticas pasan sin el
5. inventario actualizado con estado retirado

## Estrategia de transicion corta y controlada

1. Fase 0:

- inventario y clasificacion
- ADRs aprobados
- guardrails y checks base activos

2. Fase siguiente:

- retiro/refactor por lotes pequenos
- validacion tecnica por lote
- trazabilidad de decisiones

3. No corresponde a esta fase:

- refactor funcional completo de features
- rediseno total de dominio
- ruptura de contratos activos del frontend
