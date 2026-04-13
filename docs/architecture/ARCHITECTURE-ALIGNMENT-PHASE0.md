# Alineacion Arquitectonica Fase 0 - Retiro Temprano de Legacy (.NET 8)

## Contexto de esta fase

Este backend esta en etapa de retomada. El objetivo de esta fase no es sostener convivencia larga con legacy ni hacer reescritura total. El objetivo es dejar una base alineada y gobernable para ejecutar retiro progresivo pero decidido de componentes legacy, sin romper contratos activos usados por frontend.

### Regla central

Todo componente legacy identificado debe quedar en un estado explicito:

- eliminar
- reemplazar
- absorber
- conservar temporalmente

No se admite estado indefinido.

### Alcance estricto

Incluye en Fase 0:

- diagnostico operativo del estado actual
- inventario inicial moderno/hibrido/legacy
- clasificacion y decision por componente
- criterios de decision de retiro
- guardrails minimos
- checks iniciales de CI/CD
- ADR-001 y ADR-002
- estrategia de retiro temprano de legacy

No incluye en Fase 0:

- refactor funcional completo de features
- reescritura masiva
- rediseno total de dominio
- cambios funcionales de negocio
- ruptura de contratos activos del frontend

## 1. Diagnostico operativo de Fase 0

El backend no debe seguir creciendo sin alineacion por tres razones operativas:

1. Coexisten tres pipelines de ejecucion, con reglas en conflicto:

- moderno multi-tenant
- legacy generico
- API con acceso de datos directo

2. Hay acoplamiento estructural activo:

- Application depende de Infrastructure
- API ejecuta consultas/queries que deben vivir fuera de HTTP

3. La convivencia actual no es neutral:

- incrementa riesgo de regresion
- aumenta costo de cambio por feature
- retrasa onboarding y multiplica decisiones tacticas

Por que conviene retiro temprano de legacy en este caso:

- el sistema esta en etapa de retomada, no en etapa de congelamiento operativo
- existe base moderna reutilizable (AdminDb/TenantDb + middleware de tenant)
- postergar retiro consolida deuda y encarece la siguiente fase

Partes rescatables:

- resolucion de tenant en borde HTTP
- separacion AdminDb/TenantDb
- servicios ya orientados a casos de uso en varios modulos

Partes de arrastre tecnico:

- registro legacy via DependencyInyectionHandler
- DBContext legacy y repositorios base genericos
- endpoints/controllers con DbContext factory directo
- contratos/tipos transversales duplicados

Riesgo de mantener convivencia larga:

- deuda estructural normalizada
- baja velocidad real de entrega
- mayor riesgo multi-tenant por reglas distribuidas
- fase de retiro posterior mas costosa y de mayor impacto

## 2. Inventario inicial de zonas sensibles

| Componente                                                                                            | Rol actual                                        | Clasificacion | Riesgo     | Decision                | Razon tecnica breve                                                |
| ----------------------------------------------------------------------------------------------------- | ------------------------------------------------- | ------------- | ---------- | ----------------------- | ------------------------------------------------------------------ |
| Api-eCommerce/Handlers/DependencyInyectionHandler.cs                                                  | Registro DI legacy y DBContext legacy             | Legacy        | Alto       | Reemplazar              | Sigue activo en runtime y duplica composicion moderna.             |
| Api-eCommerce/Program.cs (llamada a DepencyInyectionConfig)                                           | Composition root con mezcla moderna + legacy      | Hibrido       | Alto       | Absorber                | Debe quedar un solo composition root moderno.                      |
| CC.Infraestructure/Configurations/DBContext.cs                                                        | Contexto EF legacy generico                       | Legacy        | Alto       | Reemplazar              | Duplica rol frente a AdminDb/TenantDb y mantiene pipeline antiguo. |
| CC.Aplication/Services/ServiceBase.cs                                                                 | Base CRUD generica en Application                 | Legacy        | Medio/Alto | Eliminar                | Refuerza patron generico heredado, no casos de uso.                |
| CC.Infraestructure/Repositories/ERepositoryBase.cs                                                    | Base repositorio generico                         | Legacy        | Medio/Alto | Eliminar                | Mantiene dependencia de pipeline legacy.                           |
| Api-eCommerce/Endpoints/TenantAdminEndpoints.cs                                                       | Minimal API con queries directas                  | Hibrido       | Alto       | Absorber                | Logica funcional y consultas deben salir de capa HTTP.             |
| Api-eCommerce/Controllers/StorefrontController.cs                                                     | Controller publico con acceso directo a Tenant DB | Hibrido       | Alto       | Absorber                | Misma regla: API delgada, logica fuera del controller.             |
| Api-eCommerce/Endpoints/PublicTenantConfig.cs                                                         | Endpoint con AdminDb + TenantDb en API            | Hibrido       | Alto       | Absorber                | Mantener contrato HTTP, mover orquestacion a Application.          |
| Api-eCommerce/Middleware/TenantResolutionMiddleware.cs                                                | Resolucion de tenant en borde                     | Moderno       | Medio      | Conservar temporalmente | Patron valido para transicion; endurecer con checks.               |
| CC.Infraestructure/Tenant/TenantDbContextFactory.cs                                                   | Fabrica de tenant DbContext por request           | Moderno       | Medio      | Conservar temporalmente | Util en transicion, luego encapsular tras puertos.                 |
| CC.Infraestructure/AdminDb/AdminDbContext.cs                                                          | Persistencia admin central de tenants             | Moderno       | Medio      | Conservar temporalmente | Base actual del modelo multi-tenant admin.                         |
| CC.Aplication/CC.Aplication.csproj (ProjectReference a Infrastructure)                                | Dependencia de capa rota                          | Hibrido       | Alto       | Reemplazar              | Rompe regla de dependencia de capas.                               |
| CC.Domain/CC.Domain.csproj (EF/Identity/AutoMapper)                                                   | Domain contaminado con concerns tecnicos          | Hibrido       | Medio/Alto | Reemplazar              | Domain debe quedar libre de framework concerns.                    |
| Api-eCommerce/Features/IFeatureService.cs y CC.Aplication/Services/FeatureService.cs                  | Contratos funcionales duplicados                  | Hibrido       | Medio      | Eliminar                | Debe existir contrato canonico unico.                              |
| CC.Aplication/Catalog/ProductService.cs y CC.Infraestructure/Tenant/Extensions/QueryableExtensions.cs | PagedResult<T> duplicado                          | Hibrido       | Medio      | Eliminar                | Duplicidad transversal y ambiguedad.                               |
| CC.Domain/Entities/User.cs y CC.Infraestructure/Tenant/Entities/User.cs                               | Modelos homonimos de mismo concepto               | Hibrido       | Alto       | Reemplazar              | Requiere modelo canonico por contexto y mapping temporal.          |
| Api-eCommerce/Api-eCommerce.csproj (Microsoft.AspNet.WebApi.Core)                                     | Dependencia legacy en net8                        | Legacy        | Medio      | Eliminar                | Arrastre tecnico y warning de compatibilidad.                      |
| Registros DI duplicados entre Program y handler legacy                                                | Registro cruzado de servicios                     | Hibrido       | Alto       | Eliminar                | Riesgo de comportamiento no determinista en resolucion DI.         |

## 3. Criterios de decision sobre legacy

Marco de decision por componente:

1. Uso real en runtime

- si no hay uso real verificable en runtime/tests criticos: eliminar
- si hay uso real: evaluar reemplazar o absorber

2. Valor funcional vigente

- si no aporta comportamiento vigente: eliminar
- si aporta valor: no borrar a ciegas, plan de reemplazo/absorcion

3. Duplicidad con flujo moderno

- si duplica capacidad moderna: reemplazar o eliminar

4. Impacto en alineacion de capas

- si rompe frontera de capas: priorizar retiro temprano

5. Riesgo multi-tenancy y seguridad

- si resuelve tenant fuera de borde o mezcla contextos sin control: prioridad alta

6. Costo de salida

- bajo costo + bajo riesgo: eliminar en lote temprano
- alto costo + alto riesgo: conservar temporalmente con fecha de salida

7. Impacto en contratos activos frontend

- si afecta contrato activo: absorber internamente manteniendo contrato HTTP

8. Confusion estructural

- tipos/contratos duplicados o homonimos: consolidar canonico

Regla operativa final:

- Eliminar: sin valor vigente o sin uso real
- Reemplazar: sigue en uso, pero debe migrar a alternativa moderna
- Absorber: logica valida en capa incorrecta, mover y limpiar
- Conservar temporalmente: solo por riesgo operativo alto + fecha de salida obligatoria

## 4. ADR-001 completo: Arquitectura objetivo y reglas de dependencia

### ADR-001 - Arquitectura objetivo y reglas de dependencia

- Estado: Propuesto (Fase 0)
- Fecha: 2026-04-13
- Decisor: Equipo backend

#### Contexto

La solucion opera en arquitectura hibrida con dependencias de capa rotas y acceso de datos en capa HTTP. La etapa actual requiere saneamiento estructural antes de continuar crecimiento funcional importante.

#### Problema

Sin reglas de dependencia aplicables, el sistema seguira degradandose y la eliminacion de legacy sera mas riesgosa y costosa.

#### Decision

Adoptar arquitectura objetivo pragmatica con transicion corta y guardrails obligatorios.

#### Capas objetivo

- Domain: reglas de negocio, sin dependencias de infraestructura ni HTTP.
- Application: casos de uso, contratos de caso de uso, puertos y politicas de negocio.
- Infrastructure: implementaciones de puertos, EF, almacenamiento y proveedores externos.
- API: adaptador HTTP, authn/authz, binding y composition root.

#### Reglas de dependencia

1. Domain no depende de Application, Infrastructure ni API.
2. Application depende de Domain y abstracciones; no de implementaciones de Infrastructure.
3. Infrastructure implementa puertos definidos para casos de uso.
4. API puede componer servicios, pero no alojar logica de negocio ni consultas de datos.

#### Regla de DTOs

1. DTO HTTP vive en API.
2. DTO de caso de uso vive en Application.
3. Domain no contiene DTOs de transporte.

#### Regla de acceso a DbContext

1. Solo Infrastructure accede a DbContext/DbSet/queries EF.
2. API no inyecta DbContext/TenantDbContextFactory para logica funcional.
3. Application no usa DbContext concreto ni tipos de Infrastructure.

#### Regla multi-tenant

1. Tenant se resuelve en middleware de borde.
2. Tenant viaja por ITenantContext/ITenantAccessor scoped.
3. Prohibido resolver tenant dentro de controllers/endpoints/repositorios de negocio.

#### Consecuencias

- Positivas: menos acoplamiento, mejor testabilidad, menor riesgo de regresion.
- Coste: refactor incremental de composicion y extraccion de logica HTTP.
- Alcance: sin reescritura total ni cambios funcionales de negocio en Fase 0.

#### Excepciones permitidas

Solo con ADR de excepcion que incluya:

- motivo tecnico concreto
- riesgo acotado
- owner
- fecha de expiracion (maximo 2 sprints)

Sin ADR aprobado, no hay excepcion.

## 5. ADR-002 completo: Estrategia de retiro progresivo de legacy

### ADR-002 - Estrategia de retiro progresivo de legacy

- Estado: Propuesto (Fase 0)
- Fecha: 2026-04-13
- Decisor: Equipo backend

#### Contexto

El backend esta en etapa de retomada y no debe sostener convivencia indefinida con pipeline legacy. Existe base moderna suficiente para iniciar retiro temprano de legacy de forma segura.

#### Problema

El legacy sin clasificacion ni fecha de salida se vuelve permanente y contamina cada cambio nuevo.

#### Decision

Aplicar clasificacion obligatoria por componente legacy: eliminar, reemplazar, absorber o conservar temporalmente, con salida definida.

#### Definicion de legacy

Se considera legacy todo componente que cumpla una o mas condiciones:

1. pertenece al pipeline antiguo (DependencyInyectionHandler, DBContext, ServiceBase/RepositoryBase)
2. duplica comportamiento moderno
3. rompe reglas de dependencia
4. resuelve tenant o acceso a datos fuera de frontera definida
5. introduce dependencia no alineada con net8

#### Politica de eliminacion/reemplazo/absorcion

- Eliminar: sin uso real o sin valor vigente.
- Reemplazar: uso real vigente, pero con alternativa moderna definida.
- Absorber: logica valida en capa incorrecta; se mueve con limpieza.
- Conservar temporalmente: solo por riesgo operativo alto y con fecha de salida.

#### Regla de no permanencia indefinida

Todo componente en conservar temporalmente debe tener:

- owner tecnico
- fecha limite de salida
- criterio de retiro verificable

Si falta alguno, el componente queda en incumplimiento.

#### Excepciones permitidas

Solo por criticidad operativa demostrable y con ADR de excepcion. Validez maxima: 2 sprints y revalidacion obligatoria.

#### Criterios para considerar un componente retirado

1. no esta registrado en DI de runtime
2. no tiene referencias activas de produccion
3. no participa en rutas activas
4. build + tests criticos pasan sin el
5. inventario actualizado con estado "retirado"

#### Estrategia de transicion corta y controlada

1. Fase 0: inventario, clasificacion, ADRs, guardrails y checks.
2. Fase siguiente: retiro por lotes pequenos con validacion por lote.
3. No corresponde a esta fase: migrar features completas, redisenar dominio completo, o cambiar contratos frontend activos.

## 6. Guardrails minimos

| Nombre                           | Que detecta                                               | Por que existe                                    | Severidad sugerida                        | Desde cuando aplica                         |
| -------------------------------- | --------------------------------------------------------- | ------------------------------------------------- | ----------------------------------------- | ------------------------------------------- |
| Sellado de Application           | Nuevos using/referencias de Infrastructure en Application | Frenar acoplamiento estructural nuevo             | Bloqueo                                   | Dia 1 de Fase 0                             |
| Congelamiento de legacy          | Nuevas features en zonas legacy marcadas                  | Evitar crecimiento de deuda en pipeline a retirar | Bloqueo                                   | Dia 1 de Fase 0                             |
| API sin acceso de datos directo  | Uso de DbContext/TenantDbContextFactory en HTTP           | Delgazar API y centralizar logica                 | Alerta -> Bloqueo                         | Alerta en Fase 0, Bloqueo en fase siguiente |
| ADR obligatorio para excepciones | Saltos de regla sin trazabilidad                          | Evitar bypass informal                            | Bloqueo                                   | Dia 1 de Fase 0                             |
| Control de archivos gigantes     | Archivos >500/>800 lineas                                 | Reducir god files                                 | Alerta >500 / Bloqueo >800 (nuevo codigo) | Dia 1 de Fase 0                             |
| No nuevos registros en DI legacy | AddScoped/AddTransient en pipeline legacy                 | Evitar reactivacion del legacy                    | Bloqueo                                   | Dia 1 de Fase 0                             |
| Control de tipos duplicados      | Nuevas definiciones de tipos transversales duplicados     | Evitar ambiguedad estructural                     | Alerta                                    | Dia 1 de Fase 0                             |
| Tenant solo en borde             | Resolucion de tenant fuera de middleware/zona permitida   | Proteger aislamiento multi-tenant                 | Alerta -> Bloqueo                         | Alerta en Fase 0, Bloqueo en fase siguiente |

## 7. Checks propuestos para CI/CD

| Check                    | Objetivo                                   | Implementacion simple                          | Que bloquea                        | Que solo alerta                    | Riesgo de falsos positivos | Adopcion gradual                          |
| ------------------------ | ------------------------------------------ | ---------------------------------------------- | ---------------------------------- | ---------------------------------- | -------------------------- | ----------------------------------------- |
| Dependencias de capa     | Evitar Application -> Infrastructure nuevo | Script en CI sobre diff + validacion de csproj | Nuevas referencias prohibidas      | Deuda historica no tocada          | Bajo                       | Bloqueo inmediato para codigo nuevo       |
| Congelamiento de legacy  | Evitar features nuevas en legacy           | Regla por rutas legacy en PR                   | Nuevas clases/metodos funcionales  | Comentarios/docs                   | Medio                      | Semana 1 alerta, semana 2 bloqueo         |
| DbContext en API         | Detectar acceso datos directo en HTTP      | Escaneo por patrones en Controllers/Endpoints  | Nuevos usos en lineas cambiadas    | Casos existentes heredados         | Medio                      | Empezar sobre archivos modificados        |
| Tamano de archivo        | Evitar crecimiento de god files            | Conteo de lineas en archivos cambiados         | >800 lineas en nuevo codigo        | 500-800 lineas                     | Bajo                       | Aplicar solo a codigo nuevo/modificado    |
| Duplicados transversales | Evitar duplicidad de contratos base        | Lista de nombres canonicos + escaneo           | Nuevos duplicados criticos         | Casos dudosos                      | Medio                      | Empezar con IFeatureService y PagedResult |
| Warning budget           | No empeorar salud de build                 | Parse de salida build y comparacion baseline   | Incremento de warnings vs baseline | Warnings existentes sin incremento | Medio                      | Baseline en Fase 0, endurecer luego       |
| ADR de excepciones       | Trazabilidad de excepciones                | Check de archivo ADR cuando hay bypass         | Excepcion sin ADR                  | ADR vencido                        | Bajo                       | Bloqueo inmediato                         |

## 8. Checklist tecnico paso a paso

1. Ejecutar build baseline y registrar warnings/estado.
2. Revisar composition root actual en Program.
3. Ubicar pipeline legacy activo (handler + DBContext + base repos).
4. Ubicar puntos hibridos (API con DbContext/factory directo).
5. Levantar inventario inicial moderno/hibrido/legacy.
6. Clasificar cada componente: eliminar/reemplazar/absorber/conservar temporalmente.
7. Crear carpeta docs/architecture si no existe.
8. Guardar ADR-001.
9. Guardar ADR-002.
10. Definir owners y fecha de salida para cada conservacion temporal.
11. Implementar guardrails minimos en CI/CD.
12. Activar bloqueos inmediatos (dependencias de capa y no features nuevas en legacy).
13. Etiquetar zonas legacy en backlog tecnico.
14. Dejar evidencia de estado actual (inventario + baseline build + excepciones).
15. Comunicar alcance de fase al equipo.
16. No corresponde a esta fase: refactor funcional de negocio, rediseno total de dominio, o ruptura de contratos frontend.

## 9. Definition of Done

Fase 0 termina solo si se cumple todo:

1. Inventario de zonas sensibles creado y versionado.
2. Todos los componentes legacy tienen decision explicita.
3. Ningun componente queda en estado indefinido.
4. ADR-001 y ADR-002 aprobados y guardados.
5. Guardrails minimos implementados en pipeline.
6. Bloqueos criticos activos:

- no nuevos using Infrastructure en Application
- no nuevas features en zonas legacy

7. Baseline de build y warnings registrado.
8. Excepciones abiertas documentadas con ADR y expiracion.
9. Lista de lotes candidatos para fase siguiente definida.
10. Alcance de fase comunicado y aceptado por el equipo.

## 10. Riesgos y antipatrones a evitar

1. Borrar legacy a ciegas

- Error: eliminar por intuicion sin evidencia de uso.
- Mitigacion: retiro con verificacion de referencias, rutas y tests.

2. Mezclar fase estructural con cambios de negocio

- Error: incluir features junto con saneamiento.
- Mitigacion: separar PRs y objetivos; cambios funcionales no entran aqui.

3. Convertir "absorber" en mover basura

- Error: mover codigo sin limpiar fronteras ni contratos.
- Mitigacion: absorber implica depurar, simplificar y reubicar correctamente.

4. Conservar temporalmente sin fecha

- Error: temporal que se vuelve permanente.
- Mitigacion: owner + fecha limite + criterio de salida obligatorios.

5. Endurecer CI de golpe sin adopcion gradual

- Error: friccion excesiva al inicio.
- Mitigacion: alerta primero en checks de mayor falso positivo.

6. Romper contratos activos del frontend

- Error: cambiar respuesta/shape durante fase estructural.
- Mitigacion: mantener contratos; mover logica internamente.

7. Usar excepciones como via rapida

- Error: saltar reglas por urgencia recurrente.
- Mitigacion: ADR obligatorio con expiracion corta.

## 11. Senal clara para iniciar la fase siguiente

Se puede iniciar la fase siguiente (retiro/refactor estructural real de legacy) solo cuando:

1. DoD de Fase 0 esta completo.
2. Inventario no tiene componentes sin decision.
3. Guardrails criticos corren estables por al menos un ciclo de sprint.
4. Excepciones activas tienen ADR y fecha vigente.
5. Existe primer lote de retiro definido con bajo riesgo y alcance acotado.
6. Build y pruebas criticas se mantienen estables con los checks activos.

Si alguna condicion falla, no se avanza. Primero se corrige Fase 0.

## Actualizacion de estado - Fase 1 (Lote 01, 2026-04-13)

- Estado de Microsoft.AspNet.WebApi.Core: eliminado de Api-eCommerce/Api-eCommerce.csproj.
- Evidencia de decision: el unico acoplamiento tecnico directo era System.Web.Http en ErrorHandlingMiddleware para chequeo de HttpResponseException sin uso real en el repo.
- Estado de PagedResult<T>: unificado en implementacion canonica compartida en CC.Domain/Dto/PagedResult.cs.
- Alcance real del lote: cambios tecnicos pequenos y seguros, sin cambios de contratos HTTP ni refactor funcional de endpoints.
- Pendiente siguiente lote de Fase 1: estandarizar manejo de 401/403 en middleware/endpoints sin ampliar alcance en este lote.

## Actualizacion de estado - Fase 1 (Lote 02, 2026-04-13)

- Estado del pipeline legacy en DI: reducido y aislado.
- DependencyInyectionHandler: ya no es invocado por Program.cs; queda marcado como legacy transitorio.
- Composition root activo: Program.cs queda como fuente principal de DI.
- Registros legacy mantenidos temporalmente en Program por estabilidad runtime:
  - DBContext (legacy, requerido por ActivityLoggingMiddleware)
  - ExceptionControl
  - Serilog.ILogger
- Alcance real del lote: limpieza de composicion DI sin tocar contratos HTTP ni logica de negocio.
- Pendiente siguiente lote de Fase 1: retirar dependencia de DBContext en ActivityLoggingMiddleware para habilitar apagado mas agresivo del pipeline legacy.
