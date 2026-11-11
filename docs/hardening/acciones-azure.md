# Acciones en Azure para el equipo

Ninguna de estas acciones se ha ejecutado. Cada una incluye la comprobación previa, el impacto y el rollback. Las marcadas **requiere revisión senior** afectan a seguridad, red, identidad o RBAC.

Convenciones: suscripción `b6d04525-e7b4-4b17-8492-2471f9779962`, grupo `rg-daily-enhanced-dev-centralus`.

## Clasificación (arquitectura vigente: `internal-minimal`)

| Acción | Clasificación |
|---|---|
| A1 Foundry ApiKey → Managed Identity / Entra ID | RECOMMENDED NOW: tarea independiente con migración controlada; no implementada |
| A2 Reconstruir la imagen | RECOMMENDED NOW |
| A3 Retención de imágenes subidas | RECOMMENDED NOW |
| A4 Redirect URI de MSAL | RECOMMENDED NOW, opcional |
| A5 Búsqueda híbrida | RECOMMENDED NOW, con condición (sin semántico mientras Search sea Free) |
| A6 Clave de consulta para Copilot Studio | RECOMMENDED NOW, si se confirma que hoy usa una clave de administración (verificar antes el tipo) |
| A7 Indexador a demanda y programación | RECOMMENDED NOW |
| A8 Indexación incremental y borrado lógico | FUTURE |
| A9 Cuota de `gpt-5.1` | **CLOSED / LEGACY INFRASTRUCTURE FINDING** (no es un bloqueante actual) |
| A10 Mínimo privilegio de la identidad web | RECOMMENDED NOW, tarea posterior: retirada **secuencial** de Azure AI Developer y luego de Cognitive Services OpenAI Contributor (no retirar nada aún) |
| A11 Red privada | FUTURE – ROADMAP / ADVANCED HARDENING |
| A12 `AllowAllWindowsAzureIps` | FUTURE – Future infrastructure hardening |
| A13 Asignaciones administrativas | Hallazgo de gobierno externo (escalar a CONSEIN) |

---

## A1. Conexión del proyecto de Foundry a Search con Entra ID en lugar de clave

**Clasificación: RECOMMENDED NOW (B1).** La verificación de solo lectura **V1** (2026-11-11, [lote-7-simplificado.md](lote-7-simplificado.md), sección 13) concluyó: **el agente v10 consulta Search con clave de API** (asset de índice v2 → conexión `azureairesourceprueba2`, `authType: ApiKey`). La identidad del proyecto tiene Search Index Data Reader, pero no se usa. Según Learn, la autenticación sin claves usa la identidad de la **cuenta** (que hoy no tiene roles). No modificar la conexión funcional actual. **Tarea independiente, no implementada**: los 9 pasos (identidad soportada, conexión Entra, rol único, pruebas en Search, Foundry, web app y Copilot Studio, rollback a ApiKey, retirada condicional del rol del proyecto) están en [lote-7-simplificado.md](lote-7-simplificado.md), sección 13.6. No desactivar `disableLocalAuth` ni rotar o eliminar la clave.

**Requiere revisión senior.**

- **Objetivo:** que el agente consulte Search con la identidad del proyecto y deje de depender de una clave. Es requisito previo para poder desactivar algún día la autenticación local de Search.
- **Estado actual (verificado):** la conexión `azureairesourceprueba2` es de tipo `CognitiveSearch`, `authType: ApiKey`, compartida con todos los proyectos. La identidad del proyecto (`principalId d2d03254…`) ya tiene **Search Index Data Reader** sobre el servicio de Search.
- **Cambio propuesto:** desde el portal de Foundry (Management center → Connected resources), crear una conexión nueva a la misma URL de Search con autenticación **Microsoft Entra ID**, con un nombre distinto (por ejemplo `search-daily-entra`). Después, publicar una versión nueva del agente cuya herramienta `azure_ai_search` use esa conexión. Se crea en paralelo para que la conexión actual siga disponible para el rollback.
- **Comprobación previa (solo lectura):**
  - `az role assignment list --scope /subscriptions/b6d04525-e7b4-4b17-8492-2471f9779962/resourceGroups/rg-daily-enhanced-dev-centralus/providers/Microsoft.Search/searchServices/azureairesourceprueba2 -o table` debe mostrar Search Index Data Reader para la identidad del proyecto.
  - `az search service show --name azureairesourceprueba2 --resource-group rg-daily-enhanced-dev-centralus --subscription b6d04525-e7b4-4b17-8492-2471f9779962 --query authOptions -o json` debe permitir `aadOrApiKey`.
- **Prueba:** en el playground de la nueva versión del agente, hacer una pregunta respondida por un caso indexado y comprobar que cita el documento. Repetir desde la web app fijando `AI_AGENT_VERSION` a la nueva versión en un entorno de prueba.
- **Impacto:** web app, ninguno si se fija la versión; Copilot Studio, ninguno (no usa esta conexión); Search, ninguno.
- **Rollback:** volver `AI_AGENT_VERSION` a la versión anterior (10), que sigue usando la conexión con clave. No borrar la conexión con clave hasta completar el inventario de consumidores.
- **Nota:** el agente usa hoy `query_type: simple`. Cambiar a `vector_semantic_hybrid` es una decisión aparte (ver el informe del lote 6).

## A2. Reconstruir y redesplegar la imagen del contenedor

**Clasificación: RECOMMENDED NOW.** Es el despliegue del código de los lotes 0–5.

- **Objetivo:** que la imagen use un runtime de .NET corregido. La desplegada (`web:deploy-1786477517`) se construyó en 08/2026 y el runtime base probablemente contiene versiones afectadas por las vulnerabilidades del lote 0.
- **Comando:** el flujo habitual de despliegue (`azd deploy`), solo después de fusionar este trabajo y con el IaC reconciliado (ver lote 7).
- **Comprobación previa:** `az containerapp show --name ca-web-blx2jbksxc7iu --resource-group rg-daily-enhanced-dev-centralus --subscription b6d04525-e7b4-4b17-8492-2471f9779962 --query "properties.template.containers[0].image" -o tsv`.
- **Rollback:** reactivar la revisión anterior de la Container App.

## A3. Retención de imágenes subidas por la web app

**Clasificación: RECOMMENDED NOW** (operativa, baja prioridad).

- **Objetivo:** las imágenes que los usuarios adjuntan se suben a Files de Foundry con el prefijo `webapp-upload-` y no caducan (la API GA no admite `expires_after`). El endpoint público de limpieza se eliminó en el lote 2 porque cualquier usuario podía borrar las de todos.
- **Propuesta:** tarea operativa periódica (por ejemplo, un script ejecutado por el equipo de operaciones con su propia identidad) que liste los archivos con propósito `assistants` y prefijo `webapp-upload-` más antiguos de N días y los borre. Antes de borrar, comprobar que ninguna conversación activa los necesita (las respuestas ya generadas no se ven afectadas; volver a enviar la conversación sí).
- **Comprobación previa:** inventario de archivos por prefijo y antigüedad (solo lectura).
- **Impacto:** Copilot Studio y Search, ninguno. Web app: imágenes antiguas dejan de estar disponibles para el modelo en conversaciones viejas.
- **Rollback:** no aplica (borrado). Empezar con un periodo de retención amplio.

## A4. (Opcional) Redirect URI para la renovación silenciosa de MSAL

**Clasificación: RECOMMENDED NOW, opcional.** Solo si la redirección diaria molesta a los usuarios.

**Requiere revisión senior** (cambio en Entra ID).

- **Objetivo:** con `frame-ancestors 'none'` la renovación silenciosa por iframe de MSAL falla y el frontend cae a una redirección de página completa (aprox. una vez al día por usuario, cuando caduca el refresh token). Para evitarlo se puede usar una página en blanco (`/redirect.html`) como redirect URI de las llamadas silenciosas, servida con `frame-ancestors 'self'` solo en esa ruta.
- **Cambio:** añadir la redirect URI de tipo SPA `https://<url-de-la-container-app>/redirect.html` (y `http://localhost:5173/redirect.html` en desarrollo) al registro `ai-foundry-agent-env-DailyEnhanced` (appId `b1d2995c-…`). Después, activar `SecurityHeaders__SilentRedirectPageEnabled=true` en la Container App y `VITE_MSAL_SILENT_REDIRECT_ENABLED=true` en el build del frontend.
- **Comprobación previa (solo lectura):** `az ad app show --id b1d2995c-345d-49f1-bbbc-164f029120cb --query spa.redirectUris` (comando de lectura; la configuración local de Claude Code lo tiene bloqueado, ejecútalo el equipo).
- **Prueba:** procedimiento de `verificacion-manual.md`, caso "renovación silenciosa".
- **Impacto:** Copilot Studio, Search y Foundry, ninguno.
- **Rollback:** desactivar las dos variables (la redirect URI extra puede quedar registrada sin efecto).

## A5. Búsqueda híbrida semántica en el agente de Foundry

**Clasificación: RECOMMENDED NOW, con condición.** Con Search Free, usar `vector_simple_hybrid` (sin semántico) para no agotar la asignación semántica gratuita que comparte con Copilot Studio. `vector_semantic_hybrid` solo con Search Basic (FUTURE).

- **Objetivo:** que el agente use el vectorizador y la configuración semántica del índice (hoy `query_type: simple`).
- **Cambio:** en Foundry, nueva versión del agente `Daily-Enhanced` con la herramienta `azure_ai_search` en `query_type: vector_semantic_hybrid`. No cambia la versión que usa la web app hasta fijar `AI_AGENT_VERSION`.
- **Comprobación previa:** `az rest --method get --resource https://ai.azure.com --url "https://prj-daily-enhanced-dev-resource.services.ai.azure.com/api/projects/prj-daily-enhanced-dev/agents/Daily-Enhanced/versions/10?api-version=v1" --query "definition.tools"`.
- **Prueba:** mismo conjunto de 10–20 preguntas reales contra v10 y la nueva versión; comparar fuentes citadas y precisión.
- **Impacto:** Copilot Studio y Search, ninguno; web app, solo al cambiar `AI_AGENT_VERSION`; coste, el de los embeddings de cada consulta (bajo).
- **Rollback:** volver `AI_AGENT_VERSION` a 10.

## A6. Copilot Studio: clave de consulta dedicada y fuente duplicada

**Clasificación: RECOMMENDED NOW**, si se confirma que Copilot Studio usa hoy una clave de administración. Antes hay que **verificar el tipo de clave actual**. La conexión de Copilot Studio es independiente de la de Foundry: no se asume que compartan la misma clave. Copilot Studio no se modifica en esta fase.

**Requiere revisión senior** (claves).

- **Objetivo:** que Copilot Studio no use una clave temporal (posiblemente de administración) y eliminar la fuente de conocimiento duplicada con referencia rota.
- **Cambio:** 1) En el portal de Search, crear la clave de consulta `daily-copilot-studio`. 2) En la conexión del conector Azure AI Search de Copilot Studio, sustituir la clave actual por la nueva. 3) Comprobar que el agente responde. 4) Eliminar la fuente de conocimiento duplicada (la de la referencia rota), tras exportar la solución.
- **Comprobación previa:** identificar en el portal (Search → Keys) si la clave actual es de administración o de consulta, e inventariar quién más la usa (la conexión de Foundry `azureairesourceprueba2` usa también una clave: ver A1).
- **Impacto:** Copilot Studio (reconfiguración de la conexión); web app y Foundry, ninguno.
- **Rollback:** volver a la clave anterior en la conexión (no se rota ni se borra ninguna clave en esta acción).

## A7. Ejecución del indexador a demanda y programación diaria

**Clasificación: RECOMMENDED NOW** (pasos 1 y 2). El paso 3 (`AutoPause` en la base) es una decisión de coste aparte y **no** forma parte del hardening: la base actual no se modifica sin una decisión explícita.

- **Objetivo:** publicar el contenido nuevo en minutos sin impedir la pausa automática de SQL (ver lote 6, 3.4).
- **Cambio:** 1) En el flujo `FLW_ActualizarConocimientoDeEspecialistaAzureSQL`, después del procedimiento, una acción HTTP `POST https://azureairesourceprueba2.search.windows.net/indexers/indexer-indexdailyprueba5vectorsearch/run?api-version=2024-07-01` autenticada con Entra ID (identidad con *Search Service Contributor* sobre el servicio; **no** clave de administración). 2) Programación de respaldo `{"interval":"PT24H","startTime":"<fuera de horario>"}` en el indexador. 3) (Decisión de coste) `freeLimitExhaustionBehavior=AutoPause` en la base de desarrollo.
- **Comprobación previa:** `az sql db show --name free-sql-db-knowledge-specialist --server knowledgeserver --resource-group rg-daily-enhanced-dev-centralus --subscription b6d04525-e7b4-4b17-8492-2471f9779962 --query "{autoPause:autoPauseDelay,free:useFreeLimit,onExhaustion:freeLimitExhaustionBehavior}"`.
- **Impacto:** Copilot Studio y la web app ven antes el contenido nuevo; con `AutoPause`, al agotar la cuota la base se pausa hasta el mes siguiente (el indexador y Daily fallarían hasta entonces).
- **Rollback:** quitar la acción del flujo y la programación.

## A8. Indexación incremental y borrado lógico en la vista SQL

**Clasificación: FUTURE.** Bloqueada hasta que exista `docs/sql/`.

**Requiere revisión senior** (esquema de datos).

- **Objetivo:** reindexar solo lo que cambia y propagar borrados (lote 6, 3.2 y 3.3).
- **Cambio:** 1) `rowversion` (`RowVer`) y `EsEliminado bit NOT NULL DEFAULT 0` en la tabla principal; el procedimiento actualiza la fila principal en cada cambio. 2) La vista expone `HWM` (= `RowVer`) y `EsEliminado` y **no** filtra las filas eliminadas. 3) Origen de datos con `HighWaterMarkChangeDetectionPolicy` (`HWM`) y `SoftDeleteColumnDeletionDetectionPolicy` (`EsEliminado` = `1`); indexador con `convertHighWaterMarkToRowVersion: true`. 4) Reset y ejecución completa una vez.
- **Comprobación previa:** script actual del procedimiento y de la vista en `docs/sql/` (bloqueado hasta que se exporte).
- **Prueba en desarrollo:** modificar una experiencia y comprobar que solo se procesa ese documento; marcar otra como eliminada y comprobar que desaparecen sus fragmentos del índice; reprocesar un Word y comprobar que no hay duplicados.
- **Impacto:** Copilot Studio y la web app, ninguno visible (mismo índice).
- **Rollback:** quitar las directivas del origen de datos (vuelve a indexación completa); las columnas nuevas no rompen la vista.

## A9. Cuota de `gpt-5.1` GlobalStandard en centralus

**Clasificación: CLOSED / LEGACY INFRASTRUCTURE FINDING** (verificación del 2026-11-11, [lote-7-simplificado.md](lote-7-simplificado.md), sección 15). Estado real: despliegue `gpt-5.1` GlobalStandard en centralus, capacidad 500, `provisioningState: Succeeded`. `InsufficientQuota` pertenecía a la infraestructura legacy, que intentaba crear **otro** despliegue. **No es un bloqueante actual de Daily.**

**Advertencia que se conserva:** NO recrear, redimensionar ni sustituir automáticamente el deployment actual de `gpt-5.1` sin revisar primero la cuota disponible.

| Dato | Valor |
|---|---|
| Origen del dato | Validación previa (*preflight*) de ARM durante `az deployment sub what-if` del diseño legacy, 2026-11-10. No es una métrica de Azure Monitor: es el error `InsufficientQuota` |
| Cuota afectada | *One Thousand Tokens Per Minute – gpt-5.1 – GlobalStandard* |
| Unidad | 1 unidad = 1.000 tokens por minuto (TPM) |
| Despliegue que se intentaba crear | `gpt-5.1`, SKU GlobalStandard, capacidad 50 unidades, en la cuenta **nueva** del diseño legacy (ya descartada) |
| Región | centralus (cuota por suscripción y región) |
| Límite | 250 unidades |
| Uso | 500 unidades (*"Current usage: 500 / 250 quota units"*) |
| Disponible | 0 (*"only 0 quota units are available"*) |
| Comandos de lectura (ejecutados el 2026-11-11; resultados en lote-7-simplificado.md, sección 15) | `az cognitiveservices usage list --location centralus --subscription b6d04525-e7b4-4b17-8492-2471f9779962 -o table` · `az cognitiveservices account deployment list --name prj-daily-enhanced-dev-resource --resource-group rg-daily-enhanced-dev-centralus --subscription b6d04525-e7b4-4b17-8492-2471f9779962 --query "[].{name:name,model:properties.model.name,version:properties.model.version,sku:sku.name,capacity:sku.capacity}" -o table` |
| Interpretación (verificada el 2026-11-11: las 500 unidades son el despliegue `gpt-5.1` de Daily, capacidad 500) | La cuota regional de `gpt-5.1` GlobalStandard de la suscripción está totalmente consumida por despliegues existentes (los de Daily y, posiblemente, los de otras aplicaciones de la misma suscripción; por verificar con los comandos anteriores). Que el uso supere el límite sugiere que el límite se redujo después de crear los despliegues, o que hay asignaciones previas; no se puede saber sin la lectura. **Los despliegues existentes siguen funcionando**: solo se bloquea crear despliegues nuevos o aumentar capacidad |
| Impacto en `internal-minimal` | Ninguno |
| Pendiente | Comparar el consumo real de tokens de Daily con la capacidad asignada (decisión operativa, sin cambios ahora) |

## A10. Mínimo privilegio para la identidad de la Container App

**Clasificación: RECOMMENDED NOW (tarea posterior, B2).** Hipótesis candidata, no cambio aprobado. KEEP: Cognitive Services User y AcrPull. CANDIDATE TO REMOVE: Azure AI Developer y Cognitive Services OpenAI Contributor. DO NOT ADD: Foundry User. Procedimiento secuencial en [lote-7-simplificado.md](lote-7-simplificado.md), sección 14.6.

**Requiere revisión senior** (RBAC).

- **Objetivo:** dejar la identidad web con el mínimo candidato **Cognitive Services User** (proyecto, ya asignado) y **AcrPull**, retirando de uno en uno *Azure AI Developer* y *Cognitive Services OpenAI Contributor*. El inventario del 2026-11-11 muestra que no hace falta **añadir** ningún rol. Foundry User **no** se añade: tiene las mismas `dataActions` que Cognitive Services User y más `actions` (lote-7-simplificado.md, sección 14).
- **Procedimiento:** paso 1, retirar **solo** Azure AI Developer y validar (Container App Healthy, inicio de sesión, `GET /api/agent` 200, metadata del agente, conversación, streaming, consulta con Search, adjuntos, cancelación o regeneración, logs sin 401/403); si falla, restaurarlo. Si funciona, esperar validación. Paso 2, retirar Cognitive Services OpenAI Contributor con las mismas pruebas; si falla, restaurar solo ese rol. Nunca dos roles a la vez. Sin identidad temporal.
- **Estado:** no ejecutado. No se retira ningún permiso actual.
- **Impacto:** Copilot Studio y Search, ninguno. Web app: 403 de Foundry (502 en el backend) si el rol candidato no cubre alguna acción.
- **Rollback:** volver a asignar el rol retirado, con el mismo alcance.

## A11. Migración a red privada (ROADMAP FUTURO / ADVANCED HARDENING)

**Clasificación: FUTURE – ROADMAP / ADVANCED HARDENING.** No forma parte de `internal-minimal` ni es requisito previo de la producción interna. NOT FOR CURRENT DEPLOYMENT.

**Requiere revisión senior** (red, identidad, RBAC).

- **Objetivo:** que Container Apps → Foundry, Foundry → Search, Search → SQL y Search → Azure OpenAI vayan por Private Link.
- **Plan completo:** [red-privada.md](red-privada.md), secciones 5 (fases F0–F6 con criterio y rollback) y 6 (pruebas de DNS y conectividad).
- **Restricciones clave:**
  - La cuenta de Foundry y el entorno de Container Apps actuales **no** admiten añadir VNet después: hay que crearlos de nuevo.
  - Search tiene que ser S1 o superior.
  - Copilot Studio necesita soporte de VNet de Power Platform (entorno administrado, VNets en eastus y westus): hasta entonces, `keepSearchPublic = true`.
- **Referencia histórica:** what-if del diseño legacy en [whatif/](whatif/README.md) (LEGACY DESIGN - DO NOT DEPLOY, no volver a ejecutar).
- **Impacto:** cambio de URL e historial de conversaciones nuevo para los usuarios (avisar antes); Copilot Studio necesita la clave de consulta del Search nuevo.
- **Rollback:** por fase (ver la tabla de red-privada.md); los recursos actuales siguen vivos hasta la fase F6.

## A12. Reglas del firewall de `knowledgeserver` (Future infrastructure hardening)

**Clasificación: FUTURE – Future infrastructure hardening (deuda técnica conocida).** No retirar la regla mientras el indexador de Search Free dependa de ella. No bloquea el hardening del código.

**Requiere revisión senior** (red).

- **Objetivo:** retirar `AllowAllWindowsAzureIps` (admite tráfico desde cualquier suscripción de Azure) y las reglas `ClientIPAddress_*` antiguas.
- **Bloqueo:** el indexador del Search Free actual llega a SQL gracias a `AllowAllWindowsAzureIps`, y Free no tiene IP de salida estable ni private endpoints. **No retirar** hasta que el indexador use la plataforma nueva (S1 con vínculo privado compartido, o Basic con indexador privado y regla de IP explícita).
- **Comprobación previa:** `az sql server firewall-rule list --server knowledgeserver --resource-group rg-daily-enhanced-dev-centralus --subscription b6d04525-e7b4-4b17-8492-2471f9779962 -o table`.
- **Impacto:** indexador (Copilot Studio y la web app verían contenido desactualizado), flujo de Power Automate si usa el conector SQL desde Power Platform, y accesos administrativos.
- **Rollback:** recrear la regla.

## A13. Asignaciones administrativas excesivas o desconocidas (hallazgo de gobierno)

**Clasificación: hallazgo de gobierno externo al proyecto.** Solo se documenta. No se modifica ninguna asignación (User Access Administrator, Contributor, asignaciones desconocidas, ámbito de suscripción ni de inquilino) desde este proyecto. Escalar al administrador de Azure/Entra de CONSEIN. No es responsabilidad del código de Daily.

**Requiere revisión senior.**

- Dos usuarios con **User Access Administrator en `/`** (elevación de administrador global sin retirar). Recomendación para el administrador de CONSEIN: revisar si sigue siendo necesaria y usar PIM. Desde este proyecto no se ejecuta ningún cambio.
- El principal de servicio `2907…` tiene **Contributor** y **User Access Administrator** sobre la suscripción. El administrador de CONSEIN debe identificar su origen (registro de actividad y propietario de la aplicación); ver [matriz-rbac.md](matriz-rbac.md), 2.5.
- **Impacto en Daily:** ninguno directo.
