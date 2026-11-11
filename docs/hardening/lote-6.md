# Lote 6 – Informe de datos y Azure AI Search

> **Clasificación: CURRENT** (análisis y evidencia del 2026-11-10). Sus propuestas se clasifican en [acciones-azure.md](acciones-azure.md): A5 y A7 son RECOMMENDED NOW; A8 es FUTURE. Search sigue en Free (decisión del 2026-11-11).

Solo análisis y propuesta. No se ha cambiado nada en Azure, SQL, Search, Foundry ni Copilot Studio.

## 1. Objetivo

Proponer cómo completar los campos vacíos, indexar de forma incremental, propagar borrados y programar el indexador sin disparar el coste de SQL sin servidor, y dejar verificado el vectorizador y las recomendaciones para Copilot Studio.

## 2. Evidencia (lecturas del 2026-11-10)

| Objeto | Dato | Fuente |
|---|---|---|
| Origen de datos `azuresql-dailydb` | Contenedor `[dbo].[vw_DailyConocimiento_Search]`; **sin** `dataChangeDetectionPolicy` ni `dataDeletionDetectionPolicy` | `az rest --method get …/datasources/azuresql-dailydb?api-version=2024-07-01` (sin credenciales) |
| Indexador | `schedule: null` (solo a demanda); último resultado success 3/3, 0 errores, 6 advertencias (`EspecialistaCargo` y `EspecialistaArea` vacíos en los 3 documentos); ese mismo día hubo dos ejecuciones con 3 elementos fallidos antes de un reset (ya resueltas) | `…/indexers/…` y `…/status` |
| Skillset | SplitSkill (pages/2000/300/`es`) y AzureOpenAIEmbeddingSkill (`text-embedding-3-small`, 1536, sin clave) | `…/skillsets/…` |
| Vectorizador | Ver 6 | `…/indexes/…` |
| Base SQL | `GP_S_Gen5` (sin servidor), 0,5–2 vCore, **pausa automática a los 60 min**, `useFreeLimit: true`, **`freeLimitExhaustionBehavior: BillOverUsage`**, estado `Paused` | `az sql db show` |
| Agente Foundry `Daily-Enhanced` v10 | Herramienta `azure_ai_search` sobre `index-dailyprueba5vectorsearch/versions/2`, **`query_type: simple`**, top 5 | `az rest --method get` (definición del agente) |

## 3. Propuestas

### 3.1 Cargo y área del especialista

| Opción | Ventajas | Inconvenientes |
|---|---|---|
| **A. Rellenar desde Office 365 Users** en el flujo `FLW_ActualizarConocimientoDeEspecialistaAzureSQL` (acción *Obtener perfil de usuario (V2)* con `EspecialistaEmail` → `jobTitle`, `department`) y pasarlos a `dbo.usp_Daily_IngestExperienciaFromJson` | Datos coherentes con el directorio, sin trabajo manual; permite preguntas por área ("¿quién de BI…?"). Conector estándar. | Depende de que los perfiles de Entra estén completos; añade datos personales (cargo y departamento) al índice, visibles para todos los usuarios de Daily (aceptable en una app interna, pero debe documentarse). Requiere que el procedimiento acepte y guarde los dos campos. |
| B. Quitar los campos de la vista, la proyección y la configuración semántica | Elimina las advertencias sin depender del directorio | Se pierde una dimensión útil para las búsquedas. |

**Recomendación: A**, comprobando antes en Entra que los tres especialistas actuales tienen `jobTitle` y `department`. Si no los tienen, completar los perfiles es mejor que mantener datos a mano. Mientras tanto, las 6 advertencias son inofensivas.

### 3.2 Indexación incremental

- Las vistas **no** admiten el seguimiento de cambios integrado de SQL; solo la directiva de **marca de límite superior** (`HighWaterMarkChangeDetectionPolicy`) sobre una columna de la vista.
- **Propuesta:** columna `rowversion` en la tabla principal de experiencias (`RowVer`), expuesta en la vista como `HWM`. El procedimiento de ingesta debe **actualizar la fila principal** siempre que cambie cualquier dato de la experiencia (también hijos), para que su `rowversion` avance. En el origen de datos: `highWaterMarkColumnName: "HWM"`; en el indexador: `convertHighWaterMarkToRowVersion: true`.
- **Limitaciones de usar una vista:** si la vista une varias tablas, la marca solo detecta cambios de la tabla de la que sale la columna (de ahí que el procedimiento deba tocar la principal); conviene un índice sobre la columna en la tabla base; una columna `datetime` en lugar de `rowversion` puede saltarse filas confirmadas fuera de orden (desaconsejado).

### 3.3 Borrados

- Borrado lógico: columna `EsEliminado bit` en la tabla principal, expuesta en la vista. **La vista debe seguir devolviendo las filas eliminadas** (si las filtra, el indexador nunca ve el borrado), y marcar una fila como eliminada debe hacer avanzar `HWM`.
- Directiva del origen de datos: `SoftDeleteColumnDeletionDetectionPolicy` con `softDeleteColumnName: "EsEliminado"` y `softDeleteMarkerValue: "1"`.
- Con proyecciones de índice (`skipIndexingParentDocuments`), verificar en desarrollo que el borrado del documento padre elimina sus fragmentos hijos antes de darlo por resuelto.

### 3.4 Programación

| Opción | Efecto en SQL sin servidor (pausa a los 60 min, 0,5 vCore mínimo) | Coste aproximado | Latencia de publicación |
|---|---|---|---|
| Cada hora | La base **nunca se pausa** (cada ejecución la despierta antes de los 60 min): ≈ 0,5 × 2 592 000 s ≈ **1,3 M vCore·s/mes**, unas 13 veces la cuota gratuita (100 000 vCore·s/mes). Con `BillOverUsage` se factura el exceso. | Del orden de cientos de USD/mes (validar con la calculadora de precios) | ≤ 1 h |
| Una vez al día | ≈ 0,5 × 3 600 s × 30 ≈ 54 000 vCore·s/mes (dentro de la cuota) | ≈ 0 | ≤ 24 h |
| **A demanda desde el flujo + diaria de respaldo** | Solo despierta la base cuando hay contenido nuevo | ≈ 0 | Minutos |

**Recomendación:** que el flujo de Power Automate, tras ejecutar el procedimiento, lance el indexador (`POST /indexers/{name}/run`) con una identidad con permiso de *Search Service Contributor* (no con clave de administración), y una programación diaria de respaldo (`PT24H`) fuera del horario laboral. Además, cambiar `freeLimitExhaustionBehavior` a `AutoPause` en desarrollo para que nunca facture por sorpresa (decisión de coste; ver acciones).

### 3.5 Procedimiento de ingesta

**Bloqueado:** `docs/sql/` no existe en el repositorio y no me conecto a SQL. Para cerrarlo: exportar el script de `dbo.usp_Daily_IngestExperienciaFromJson` (y la definición de `dbo.vw_DailyConocimiento_Search`) a `docs/sql/`. Qué revisaré: que haga *upsert* por `ExperienciaId` (`MERGE` o `UPDATE`/`INSERT` con la clave), que no inserte duplicados al reprocesar el mismo Word, que actualice la fila principal (necesario para 3.2) y que sea transaccional.

### 3.6 Vectorizador

**Verificado**: `daily-vectorizer`, tipo `azureOpenAI`, `resourceUri` `https://prj-daily-enhanced-dev-resource.openai.azure.com` (endpoint del recurso, no del proyecto), `deploymentId` y `modelName` `text-embedding-3-small`, **sin `apiKey`**, **sin `authIdentity`** (usa la identidad del sistema de Search), campo `chunk_vector` de 1536 dimensiones con el perfil `daily-vector-profile` (HNSW). Coincide con el skill de embeddings. Ninguna acción.

### 3.7 Copilot Studio

- **Fuente de conocimiento duplicada con referencia rota:** eliminarla desde Copilot Studio después de confirmar cuál de las dos es la activa (la que tiene la conexión válida y responde en el panel de pruebas). Exportar antes la solución como respaldo.
- **Clave:** crear la clave de consulta `daily-copilot-studio` (acción pendiente; no existe todavía) y sustituir con ella la clave temporal actual en la conexión. Nunca una clave de administración para consultas. No se puede comprobar desde aquí qué tipo de clave usa hoy (los comandos de claves están bloqueados por diseño): el equipo debe compararla en el portal (Search → Keys).
- La migración a Entra ID queda pospuesta, como estaba decidido.

### 3.8 Hallazgo adicional: el agente de Foundry no usa la búsqueda vectorial

El agente consulta el índice con `query_type: simple` (palabras clave). El índice tiene vectorizador y configuración semántica, que solo usa Copilot Studio. **Propuesta:** publicar una versión del agente con `query_type: vector_semantic_hybrid` y compararla con la actual con un conjunto de preguntas reales antes de fijarla (`AI_AGENT_VERSION`).

## 4. Riesgos

- Programar el indexador cada hora sin cambiar la base o su comportamiento al agotar la cuota tiene un coste mensual relevante.
- Activar la marca de límite superior sin que el procedimiento toque la fila principal dejaría cambios sin indexar.

## 5. Acciones para el equipo

A5 a A8 en [acciones-azure.md](acciones-azure.md).

## 6. Sin modificar a propósito

Todo: el lote es de análisis.
