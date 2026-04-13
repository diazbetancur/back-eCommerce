# Arquitectura Actual y Plan de Alineacion Incremental (.NET 8)

> Nota de control (retomada del backend): para la ejecucion operativa de Fase 0 con enfoque de retiro temprano de legacy, usar como fuente principal `docs/architecture/ARCHITECTURE-ALIGNMENT-PHASE0.md` y sus ADRs asociados. Este documento se conserva como marco general, pero las decisiones de convivencia con legacy deben interpretarse como transicion corta y controlada, no como coexistencia prolongada.

## 1. Resumen ejecutivo

La solucion no opera hoy como una Clean Architecture consistente. Opera como una **arquitectura hibrida en transicion** con tres flujos activos:

1. Flujo moderno multi-tenant: `API -> Application -> Infrastructure (AdminDb/TenantDb)`.
2. Flujo legacy generico: `API -> DependencyInyectionHandler -> ServiceBase/RepositoryBase/DBContext`.
3. Flujo API gruesa: `API -> DbContext directo` en controllers y minimal APIs.

El sistema compila, esta en produccion y tiene base de pruebas. Por eso, la estrategia correcta **no es reescritura** ni congelar delivery: es **alineacion incremental, controlada y medible**, fase por fase.

Este documento conserva el diagnostico original y lo convierte en una guia ejecutable para varios sprints, priorizando:

- reduccion de deuda estructural,
- claridad de fronteras de capa,
- control del flujo multi-tenant,
- convivencia temporal legacy/moderno sin romper operacion.

## 2. Diagnostico consolidado del estado actual

### 2.1 Patron real detectado (no teorico)

Los nombres de proyecto (`CC.Domain`, `CC.Aplication`, `CC.Infraestructure`, `Api-eCommerce`) sugieren arquitectura en capas limpia. La implementacion real muestra acoplamiento cruzado:

- `CC.Aplication` referencia `CC.Infraestructure` en su `.csproj`.
- servicios de Application consumen `AdminDbContext`, `TenantDbContextFactory` y entidades de infraestructura.
- API ejecuta queries EF directo en endpoints/controllers.
- Domain contiene DTOs y dependencias tecnicas que no son propias del nucleo de negocio.

**Conclusion tecnica:** layered architecture hibrida con regla de dependencias rota y migracion incompleta a modulos por feature.

### 2.2 Evidencia clave (snapshot actual)

1. **Dependencias de proyecto**

- `CC.Aplication/CC.Aplication.csproj` referencia `CC.Infraestructure`.
- `CC.Domain/CC.Domain.csproj` incluye paquetes tecnicos (`EntityFrameworkCore`, `Identity`, `AutoMapper`).

2. **Mezcla de capas**

- `CC.Aplication` usa `CC.Infraestructure.*` en 31 de 54 archivos (~57%).
- `Api-eCommerce` usa `CC.Infraestructure.*` en 34 de 61 archivos (~56%).

3. **API con logica de datos**

- `Endpoints/TenantAdminEndpoints.cs` (~1417 lineas) con CRUD/queries y acceso de datos directo.
- `Controllers/StorefrontController.cs` (~716 lineas) con resolucion de tenant + consultas directas.

4. **Duplicidad de modelo**

- conceptos equivalentes representados en `Domain` y `Infrastructure.Tenant.Entities`.
- mezcla de entidades en el mismo contexto de persistencia.

5. **Legacy activo en runtime**

- `DependencyInyectionHandler`, `DBContext`, repositorios base y migraciones legacy coexistiendo con Admin/Tenant DbContexts modernos.

6. **Inconsistencia transversal**

- doble `IFeatureService`, doble `PagedResult<T>`, servicios con mismo nombre en rutas distintas.
- dependencia legacy `Microsoft.AspNet.WebApi.Core` en API (.NET 8) con warning de compatibilidad.

7. **Salud de compilacion**

- `dotnet build` OK con 119 warnings: ruido de nullability, miembros ocultos y compatibilidad legacy.

### 2.3 Fortalezas existentes (base para modernizacion)

1. Flujo multi-tenant funcional (`TenantResolutionMiddleware`, `TenantAccessor`, `TenantDbContextFactory`).
2. Separacion AdminDB/TenantDB ya iniciada.
3. Evolucion por features visible en varios modulos.
4. Base de pruebas (unit/integration/e2e) util para refactor seguro.
5. Contratos e interfaces presentes en parte del sistema.

## 3. Impacto en negocio y delivery

Los hallazgos tecnicos ya tienen impacto operativo. No son problemas cosmeticos.

1. **Mayor lead time por feature**

- misma regla de negocio repartida entre API, servicios y repositorios.
- efecto: mayor tiempo de analisis, mas retrabajo y menor predictibilidad por sprint.

2. **Riesgo de regresion mas alto**

- archivos gigantes y rutas de ejecucion duplicadas (legacy + moderno).
- efecto: cambios pequenos con superficie de impacto grande, mas hotfixes y mayor costo de QA.

3. **Onboarding lento del equipo**

- no hay frontera unica para "donde vive" cada tipo de logica.
- efecto: incorporacion lenta de devs, dependencia de conocimiento tacito, baja escalabilidad del equipo.

4. **Costo de mantenimiento creciente**

- duplicidad de contratos/modelos y warnings altos.
- efecto: esfuerzo no productivo sube sprint a sprint; cae el throughput real.

5. **Riesgo al escalar modulos multi-tenant**

- resolucion de tenant y acceso de datos no estan encapsulados de forma uniforme.
- efecto: riesgo de fugas de datos entre tenants, errores de aislamiento y mayor riesgo reputacional/operativo.

6. **Freno a evolucion del frontend**

- backend sin contratos estables obliga ajustes frecuentes en UI.
- efecto: mayor coordinacion backend/frontend, mas bloqueos cruzados y menor velocidad de refinamiento paralelo.

## 4. Priorizacion de hallazgos

### 4.1 Priorizacion por riesgo (P0/P1/P2)

#### P0 - Riesgo alto

1. Regla de dependencia rota (`Application -> Infrastructure`).
2. API con logica de negocio y queries directas a DB.
3. Modelo de dominio duplicado/no canonico.
4. Pipeline dual legacy + moderno sin frontera operativa.

#### P1 - Riesgo medio

5. God files en servicios/endpoints/controllers.
6. DTOs/contratos dispersos entre Domain, Application y API.
7. Tipos transversales duplicados (`IFeatureService`, `PagedResult<T>`, etc.).
8. Configuracion EF inconsistente y riesgo de drift.

#### P2 - Riesgo bajo/medio

9. Alto volumen de warnings.
10. Dependencia legacy incompatible en API.

### 4.2 Matriz impacto vs esfuerzo (con quick wins)

| Categoria                                | Item                                                                   | Impacto    | Esfuerzo | Resultado esperado                                       |
| ---------------------------------------- | ---------------------------------------------------------------------- | ---------- | -------- | -------------------------------------------------------- |
| Alto impacto / Bajo esfuerzo (Quick Win) | Retirar `Microsoft.AspNet.WebApi.Core` si no hay uso real              | Alto       | Bajo     | Menos riesgo de compatibilidad y menos ruido de build    |
| Alto impacto / Bajo esfuerzo (Quick Win) | Congelar altas en `DependencyInyectionHandler`                         | Alto       | Bajo     | Detiene crecimiento de deuda legacy                      |
| Alto impacto / Bajo esfuerzo (Quick Win) | Check CI para prohibir `using CC.Infraestructure` en Application nuevo | Alto       | Bajo     | Evita nueva contaminacion de capas                       |
| Alto impacto / Bajo esfuerzo (Quick Win) | Unificar `PagedResult<T>`                                              | Medio/Alto | Bajo     | Menos ambiguedad y menor costo de mantenimiento          |
| Alto impacto / Alto esfuerzo             | Extraer queries de `TenantAdminEndpoints` a casos de uso               | Alto       | Alto     | API mas delgada y reglas en un solo lugar                |
| Alto impacto / Alto esfuerzo             | Consolidar modelo canonico por bounded context                         | Alto       | Alto     | Menos duplicidad estructural y menor riesgo de regresion |
| Bajo impacto / Bajo esfuerzo             | Limpieza inicial de warnings de nulos en modulos piloto                | Medio      | Bajo     | Mejor senal de calidad en compilacion                    |
| Deuda estructural de largo plazo         | Retiro progresivo de `DBContext` legacy y repositorios base            | Alto       | Alto     | Plataforma mas mantenible y gobernable                   |

## 5. Arquitectura objetivo de transicion (3 estados)

### 5.1 Estado actual (AS-IS)

- Convivencia de 3 flujos activos.
- Fronteras de capa ambiguas.
- Tenant atraviesa capas de forma inconsistente.

### 5.2 Estado intermedio de transicion (TO-BE Transitional)

Objetivo: estabilizar convivencia temporal sin reescritura.

1. **Legacy aislado pero activo**

- legacy sigue corriendo para no frenar operacion,
- pero queda congelado para nuevas features.

2. **Application sellada progresivamente**

- nuevos casos de uso solo via puertos (interfaces).
- adaptadores de infraestructura implementan puertos.

3. **API delgada en modulos priorizados**

- modulos piloto sin `DbContext` directo,
- API centrada en contratos HTTP, autorizacion y orquestacion.

4. **TenantContext centralizado**

- resolucion en borde (middleware),
- consumo uniforme en Application via abstraccion.

### 5.3 Estado objetivo final (TO-BE Target)

- Domain limpio de concerns tecnicos.
- Application sin referencias a infraestructura concreta.
- Infrastructure implementa puertos de persistencia/servicios externos.
- API como adaptador HTTP (sin reglas de negocio ni acceso DB directo).
- Legacy retirado del runtime principal.

### 5.4 Decisiones de transicion (no negociables)

1. No Big Bang Rewrite.
2. No migracion total en una sola release.
3. Cada fase deja guardrails para evitar retroceso.
4. Se permite coexistencia temporal solo si tiene frontera y plan de retiro.

## 6. Estrategia multi-tenant explicita (regla de diseno)

### 6.1 Que se queda en cada capa

#### API

- autenticacion/autorizacion,
- resolucion inicial de tenant en middleware,
- binding request/response,
- invocacion de casos de uso,
- manejo de codigos HTTP.

**No permitido en API:** queries EF, reglas de negocio de tenant, calculos de dominio, reglas de aislamiento de datos.

#### Application

- orquestacion de casos de uso,
- politicas de negocio multi-tenant,
- validacion semantica,
- consumo de `ITenantContext` y puertos.

**No permitido en Application:** `DbContext`, `DbSet`, `IQueryable` de infraestructura concreta, connection strings, factories EF concretas.

#### Infrastructure

- implementacion de puertos de persistencia,
- `AdminDbContext`/`TenantDbContext`,
- resolucion tecnica de conexion y acceso a proveedores externos,
- mapeo ORM/configuracion EF.

**No permitido en Infrastructure:** reglas de negocio que definan comportamiento funcional de casos de uso.

### 6.2 Como debe viajar TenantContext

1. Middleware resuelve tenant (host/header/token/ruta segun politica vigente).
2. Tenant se guarda en un `ITenantContextAccessor` scoped.
3. Application consume `ITenantContext` como entrada de caso de uso.
4. Infrastructure usa ese contexto solo para seleccionar conexion/particion.

### 6.3 Donde si y donde no se puede resolver tenant

**Permitido resolver tenant en:**

- middleware de entrada HTTP,
- bootstrap de jobs/background con contexto explicito,
- adaptadores de mensajeria al iniciar procesamiento.

**No permitido resolver tenant en:**

- controllers/endpoints individuales,
- repositorios de negocio,
- entidades de dominio,
- helpers staticos reutilizables.

### 6.4 Mecanismos para evitar filtracion de tenant a capas incorrectas

1. Contrato unico `ITenantContext` y prohibicion de pasar `tenantId` suelto salvo excepcion ADR.
2. Test de arquitectura que detecte lectura de headers/claims fuera de API middleware.
3. Revisiones de PR con checklist de aislamiento multi-tenant.
4. Regla CI para fallar si aparece resolucion de tenant fuera de zonas permitidas.

## 7. Gobernanza arquitectonica automatizable

Las reglas deben ejecutarse en CI/CD, no quedar en documento.

1. **Regla de referencias de capa**

- prohibir nuevas referencias directas de `CC.Aplication` hacia `CC.Infraestructure`.
- implementacion sugerida: test de arquitectura (NetArchTest) + script de validacion en pipeline.

2. **Regla API sin DbContext directo**

- detectar `DbContext`, `DbSet`, `TenantDbContextFactory` en controllers/endpoints.
- excepciones solo con ADR aprobada y vencimiento.

3. **Regla de tamano maximo de archivo**

- alertar >500 lineas y bloquear >800 lineas para codigo nuevo/modificado.

4. **Regla ADR obligatoria para excepciones**

- si PR viola una regla de capas debe incluir ADR (template + check automatizado de archivo ADR).

5. **Regla de congelamiento legacy**

- bloquear nuevas features en carpetas/handlers legacy.
- permitir solo fixes criticos y tareas de retiro.

6. **Regla de contratos canonicos**

- detectar duplicados de tipos transversales (`PagedResult`, `IFeatureService`, etc.).

7. **Regla de warning budget**

- baseline inicial y objetivo de reduccion progresiva por fase.
- bloquear PR que incremente el numero total de warnings sin justificacion.

## 8. Roadmap incremental por fases (ejecutable)

### Fase 0 - Baseline y guardrails (1-2 semanas)

**Objetivo**

- detener crecimiento de deuda mientras se mantiene delivery.

**Por que existe**

- sin guardrails, cualquier refactor parcial se degrada en pocas semanas.

**Alcance**

- ADR de arquitectura y coexistencia.
- congelamiento de legacy para nuevas features.
- checks CI iniciales (referencias, DbContext en API, warning budget base).

**Que si entra**

- decisiones y reglas,
- cambios de pipeline,
- quick wins de compatibilidad.

**Que no entra**

- refactor funcional grande de modulos.

**Entregables**

- ADR-001 y ADR-002 aprobados.
- job CI con reglas minimas.
- inventario de puntos legacy activos.

**Definition of Done**

- no se aceptan PRs que agreguen feature nueva en legacy.
- CI detecta al menos 2 reglas arquitectonicas.

**Riesgos**

- resistencia del equipo por friccion inicial de pipeline.

**Dependencias**

- acuerdo de equipo sobre reglas y excepciones.

**KPI/Criterio de validacion**

- 0 nuevas altas funcionales en legacy.
- 100% PRs pasan por checks de arquitectura base.

**Recomendacion para no mezclar con nuevas features**

- cualquier feature de negocio debe usar reglas nuevas desde esta fase; no esperar fases siguientes para cumplir guardrails.

### Fase 1 - Sellado de Application en modulos piloto (2-4 semanas)

**Objetivo**

- cortar dependencia directa de Application hacia infraestructura en 2 dominios piloto.

**Por que existe**

- es el punto de mayor retorno para testabilidad y mantenibilidad.

**Alcance**

- Catalog y Plans (pilotos).
- creacion de puertos de repositorio/servicios.
- migracion de DTOs de caso de uso a Application Contracts.

**Que si entra**

- interfaz de puertos,
- adaptadores en infraestructura,
- cambios de DI por modulo piloto.

**Que no entra**

- consolidacion completa de todos los modulos.

**Entregables**

- puertos definidos y usados por casos de uso piloto.
- eliminacion de `using CC.Infraestructure.*` en pilotos.

**Definition of Done**

- modulos piloto compilan y pasan tests sin acceso infra directo desde Application.

**Riesgos**

- deuda oculta en servicios compartidos legacy.

**Dependencias**

- fase 0 operativa,
- inventario de servicios usados por pilotos.

**KPI/Criterio de validacion**

- bajar `using CC.Infraestructure` en Application de 31 a <=15 archivos.

**Recomendacion para no mezclar con nuevas features**

- features nuevas en Catalog/Plans deben entrar ya sobre puertos, no sobre implementaciones directas.

### Fase 2 - API Slimming en rutas criticas (3-5 semanas)

**Objetivo**

- mover logica de negocio y datos fuera de endpoints/controllers criticos.

**Por que existe**

- hoy la API concentra reglas y EF directo, principal fuente de regresiones.

**Alcance**

- `TenantAdminEndpoints` y `StorefrontController` (segmentados por casos de uso).
- estandar de contratos HTTP y validacion sintactica.

**Que si entra**

- extraccion de handlers/casos de uso,
- capa de mapping request/response,
- tests de contrato en rutas afectadas.

**Que no entra**

- migrar toda la API a un unico paradigma.

**Entregables**

- primeras vertical slices fuera de archivos gigantes.
- reduccion efectiva de codigo de consulta EF en API.

**Definition of Done**

- rutas migradas no usan `DbContext` directo en capa HTTP.

**Riesgos**

- ruptura de comportamiento por reubicacion de reglas tacitas.

**Dependencias**

- fase 1 estable para modulos implicados.

**KPI/Criterio de validacion**

- acceso `DbContext` directo en API <=20% de endpoints activos.

**Recomendacion para no mezclar con nuevas features**

- cualquier mejora funcional en rutas objetivo debe salir junto al refactor de slimming, no como parche separado en API gruesa.

### Fase 3 - Consolidacion de dominio y contratos (3-6 semanas)

**Objetivo**

- establecer modelo canonico por bounded context y eliminar duplicidad estructural.

**Por que existe**

- sin modelo canonico, la deuda vuelve a crecer por cada feature.

**Alcance**

- User/Role/Product/Category (iniciar por contextos de mayor uso).
- unificacion de tipos transversales duplicados.

**Que si entra**

- consolidacion progresiva por contexto,
- explicitacion de mapeos temporales legacy.

**Que no entra**

- rediseno total del dominio en una sola iteracion.

**Entregables**

- catalogo de modelos canonicos por contexto.
- tipos transversales unificados.

**Definition of Done**

- no existen homonimos activos sin ADR en contextos intervenidos.

**Riesgos**

- impacto en queries/reportes heredados.

**Dependencias**

- fase 2 con rutas principales desacopladas.

**KPI/Criterio de validacion**

- 1 implementacion por contrato transversal.
- reduccion de warnings globales a <80.

**Recomendacion para no mezclar con nuevas features**

- features de dominios en consolidacion solo se aceptan si usan modelo canonico acordado.

### Fase 4 - Retiro legacy controlado (3-6 semanas)

**Objetivo**

- sacar legacy del camino de evolucion sin interrumpir operacion.

**Por que existe**

- coexistencia indefinida duplica soporte y riesgo operativo.

**Alcance**

- desactivar registros legacy no usados.
- plan de sunset de `DBContext` y migraciones legacy.
- cierre de ultimo flujo funcional dependiente de pipeline antiguo.

**Que si entra**

- retiro tecnico, toggles de compatibilidad temporales, plan de rollback.

**Que no entra**

- cambios funcionales de negocio no relacionados al retiro.

**Entregables**

- runtime principal sin dependencia funcional del pipeline legacy.

**Definition of Done**

- 0 nuevas llamadas de negocio via `ServiceBase/RepositoryBase`.

**Riesgos**

- deuda oculta en jobs o procesos operativos no cubiertos por tests.

**Dependencias**

- fases 1-3 cerradas en modulos core.

**KPI/Criterio de validacion**

- 0 features nuevas en legacy durante toda la fase.
- warnings <60.

**Recomendacion para no mezclar con nuevas features**

- cualquier feature nueva se hace solo en pipeline moderno; si depende de legacy, primero debe existir tarea de migracion tecnica.

### Fase 5 - Hardening continuo y optimizacion operativa (continuo)

**Objetivo**

- convertir la arquitectura alineada en practica sostenible.

**Por que existe**

- sin hardening continuo, reaparece drift arquitectonico.

**Alcance**

- reduccion final de warnings,
- ampliacion de test suite critica,
- observabilidad por caso de uso,
- ajuste de SLOs tecnicos.

**Que si entra**

- calidad, estabilidad, monitoreo, performance puntual.

**Que no entra**

- rediseno estructural mayor fuera de roadmap.

**Entregables**

- tablero de salud arquitectonica y tecnica.

**Definition of Done**

- reglas CI maduras + indicadores operativos estables.

**Riesgos**

- priorizar urgencias de negocio y postergar calidad.

**Dependencias**

- cierre de fases estructurales previas.

**KPI/Criterio de validacion**

- warnings <40,
- regresiones por release en descenso,
- lead time tecnico estable.

**Recomendacion para no mezclar con nuevas features**

- reservar capacidad fija por sprint (20-30%) para hardening, no tratarlo como trabajo "sobrante".

## 9. Secuencia recomendada de ejecucion lenta y controlada

1. **Primero Fase 0**

- porque instala guardrails y evita que la deuda siga creciendo mientras se refactoriza.

2. **Luego Fase 1**

- porque sellar Application reduce acoplamiento y habilita API slimming con menor riesgo.

3. **Despues Fase 2**

- porque mover logica fuera de API disminuye regresiones y clarifica ownership por capa.

4. **Continuar con Fase 3**

- porque consolidar modelo sin haber adelgazado API suele romper mas cosas de las que arregla.

5. **Ejecutar Fase 4**

- solo cuando modulos core ya no dependan del pipeline legacy.

6. **Mantener Fase 5 en paralelo al cierre**

- para sostener calidad y evitar recaida.

### Senales para pasar a la siguiente fase

- DoD de fase actual cumplido.
- KPIs minimos alcanzados o tendencia sostenida de mejora.
- no hay incidentes abiertos de severidad alta relacionados al cambio de fase.
- pruebas criticas en verde durante al menos 2 ciclos de integracion.

### Cuando NO conviene avanzar

- si aumenta regresion funcional despues de un refactor,
- si se disparan excepciones ADR para saltar reglas,
- si el equipo vuelve a agregar features en legacy,
- si la cobertura de rutas criticas baja respecto al baseline.

## 10. Backlog tecnico inicial priorizado

### 10.1 Quick wins

| Titulo                                                      | Objetivo                                 | Impacto    | Esfuerzo | Prioridad | Dependencia                   |
| ----------------------------------------------------------- | ---------------------------------------- | ---------- | -------- | --------- | ----------------------------- |
| Retirar package `Microsoft.AspNet.WebApi.Core`              | eliminar dependencia legacy incompatible | Alto       | Bajo     | P0        | Inventario de uso real        |
| Congelar `DependencyInyectionHandler` para nuevas features  | frenar deuda nueva en pipeline legacy    | Alto       | Bajo     | P0        | ADR-002                       |
| Check CI de `using CC.Infraestructure` en Application nuevo | impedir nueva contaminacion de capas     | Alto       | Bajo     | P0        | Fase 0                        |
| Unificar `PagedResult<T>`                                   | evitar ambiguedad transversal            | Medio/Alto | Bajo     | P1        | Decision de contrato canonico |

### 10.2 Saneamiento de capas

| Titulo                                                            | Objetivo                                 | Impacto | Esfuerzo   | Prioridad | Dependencia      |
| ----------------------------------------------------------------- | ---------------------------------------- | ------- | ---------- | --------- | ---------------- |
| Crear puertos `ITenantCatalogRepository` y `IAdminPlanRepository` | desacoplar Application de infra concreta | Alto    | Medio      | P0        | Fase 0           |
| Migrar Catalog a puertos                                          | piloto de sellado de Application         | Alto    | Medio/Alto | P0        | Item anterior    |
| Migrar Plans a puertos                                            | segundo piloto para validar patron       | Alto    | Medio/Alto | P0        | Item anterior    |
| Mover DTOs de caso de uso a Application Contracts                 | aclarar frontera DTO dominio/caso de uso | Medio   | Medio      | P1        | ADR de contratos |

### 10.3 API slimming

| Titulo                                                                   | Objetivo                                | Impacto    | Esfuerzo | Prioridad | Dependencia         |
| ------------------------------------------------------------------------ | --------------------------------------- | ---------- | -------- | --------- | ------------------- |
| Partir `TenantAdminEndpoints` en slices (Products/Orders/Users/Settings) | reducir god file y extraer casos de uso | Alto       | Alto     | P0        | Fase 1              |
| Refactor `StorefrontController` hacia handlers de Application            | quitar queries directas en API          | Alto       | Alto     | P0        | Fase 1              |
| Definir regla por modulo: Controller o Minimal API                       | evitar dualidad inconsistente           | Medio/Alto | Medio    | P1        | ADR de surface HTTP |

### 10.4 Consolidacion de dominio

| Titulo                                      | Objetivo                         | Impacto | Esfuerzo | Prioridad | Dependencia                  |
| ------------------------------------------- | -------------------------------- | ------- | -------- | --------- | ---------------------------- |
| Definir modelo canonico de Product/Category | eliminar duplicidad de entidades | Alto    | Alto     | P0        | Fase 2                       |
| Definir modelo canonico de User/Role        | unificar identidad y permisos    | Alto    | Alto     | P0        | Fase 2                       |
| Unificar `IFeatureService`                  | una sola abstraccion transversal | Medio   | Medio    | P1        | Relevamiento de consumidores |

### 10.5 Retiro de legacy

| Titulo                                                               | Objetivo                    | Impacto    | Esfuerzo | Prioridad | Dependencia         |
| -------------------------------------------------------------------- | --------------------------- | ---------- | -------- | --------- | ------------------- |
| Inventario de rutas aun dependientes de `ServiceBase/RepositoryBase` | planear sunset sin ceguera  | Alto       | Medio    | P0        | Fase 1              |
| Plan de sunset de `DBContext` legacy                                 | retirar pipeline dual       | Alto       | Alto     | P0        | Fase 3              |
| Desactivar registros DI legacy no usados                             | reducir superficie de error | Medio/Alto | Medio    | P1        | Inventario completo |

### 10.6 Hardening

| Titulo                                        | Objetivo                         | Impacto    | Esfuerzo | Prioridad | Dependencia |
| --------------------------------------------- | -------------------------------- | ---------- | -------- | --------- | ----------- |
| Reducir warnings de 119 a <80                 | mejorar senal de build y calidad | Medio/Alto | Medio    | P1        | Fases 1-3   |
| Tests de contrato en endpoints criticos       | proteger refactor incremental    | Alto       | Medio    | P1        | Fase 2      |
| Alertas de tamano de archivo y ADR exceptions | sostener gobernanza              | Medio      | Bajo     | P1        | Fase 0      |

## 11. Recomendacion ejecutiva final

Este backend **no debe seguir creciendo sin alineacion** porque la deuda ya impacta velocidad, riesgo y costo de mantenimiento. Seguir agregando features sobre la arquitectura actual aumenta regresiones y hace cada sprint menos predecible.

Tambien **no hace falta reescribirlo**: hay una base funcional y testeable que permite refactor incremental. La estrategia mas segura es:

1. instalar guardrails de arquitectura,
2. sellar Application en modulos piloto,
3. adelgazar API en rutas criticas,
4. consolidar modelo,
5. retirar legacy de forma controlada,
6. endurecer calidad de forma continua.

Refinar backend primero, mientras frontend se sigue refinando en paralelo, reduce friccion de contratos, mejora ritmo de entrega y disminuye riesgo operativo. Es la via pragmatica para evolucionar sin detener negocio.

## 12. Nota metodologica

Analisis y plan basados en:

- estructura de solucion y referencias de proyectos,
- inspeccion de Program, middleware, endpoints y controllers,
- revision de servicios representativos de Domain/Application/Infrastructure/API,
- evidencia de acoplamiento, tamano de archivos y salud de build actual,
- criterio de modernizacion incremental para sistemas productivos.
