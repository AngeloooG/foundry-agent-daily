# Acciones en Azure para el equipo

Ninguna de estas acciones se ha ejecutado. Cada una incluye la comprobación previa, el impacto y el rollback. Las marcadas **requiere revisión senior** afectan a seguridad, red, identidad o RBAC.

Convenciones: suscripción `b6d04525-e7b4-4b17-8492-2471f9779962`, grupo `rg-daily-enhanced-dev-centralus`.

---

## A1. Conexión del proyecto de Foundry a Search con Entra ID en lugar de clave

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

- **Objetivo:** que la imagen use un runtime de .NET corregido. La desplegada (`web:deploy-1786477517`) se construyó en 08/2026 y el runtime base probablemente contiene versiones afectadas por las vulnerabilidades del lote 0.
- **Comando:** el flujo habitual de despliegue (`azd deploy`), solo después de fusionar este trabajo y con el IaC reconciliado (ver lote 7).
- **Comprobación previa:** `az containerapp show --name ca-web-blx2jbksxc7iu --resource-group rg-daily-enhanced-dev-centralus --subscription b6d04525-e7b4-4b17-8492-2471f9779962 --query "properties.template.containers[0].image" -o tsv`.
- **Rollback:** reactivar la revisión anterior de la Container App.

## A3. Retención de imágenes subidas por la web app

- **Objetivo:** las imágenes que los usuarios adjuntan se suben a Files de Foundry con el prefijo `webapp-upload-` y no caducan (la API GA no admite `expires_after`). El endpoint público de limpieza se eliminó en el lote 2 porque cualquier usuario podía borrar las de todos.
- **Propuesta:** tarea operativa periódica (por ejemplo, un script ejecutado por el equipo de operaciones con su propia identidad) que liste los archivos con propósito `assistants` y prefijo `webapp-upload-` más antiguos de N días y los borre. Antes de borrar, comprobar que ninguna conversación activa los necesita (las respuestas ya generadas no se ven afectadas; volver a enviar la conversación sí).
- **Comprobación previa:** inventario de archivos por prefijo y antigüedad (solo lectura).
- **Impacto:** Copilot Studio y Search, ninguno. Web app: imágenes antiguas dejan de estar disponibles para el modelo en conversaciones viejas.
- **Rollback:** no aplica (borrado). Empezar con un periodo de retención amplio.

## A4. (Opcional) Redirect URI para la renovación silenciosa de MSAL

**Requiere revisión senior** (cambio en Entra ID).

- **Objetivo:** con `frame-ancestors 'none'` la renovación silenciosa por iframe de MSAL falla y el frontend cae a una redirección de página completa (aprox. una vez al día por usuario, cuando caduca el refresh token). Para evitarlo se puede usar una página en blanco (`/redirect.html`) como redirect URI de las llamadas silenciosas, servida con `frame-ancestors 'self'` solo en esa ruta.
- **Cambio:** añadir la redirect URI de tipo SPA `https://<url-de-la-container-app>/redirect.html` (y `http://localhost:5173/redirect.html` en desarrollo) al registro `ai-foundry-agent-env-DailyEnhanced` (appId `b1d2995c-…`). Después, activar `SecurityHeaders__SilentRedirectPageEnabled=true` en la Container App y `VITE_MSAL_SILENT_REDIRECT_ENABLED=true` en el build del frontend.
- **Comprobación previa (solo lectura):** `az ad app show --id b1d2995c-345d-49f1-bbbc-164f029120cb --query spa.redirectUris` (comando de lectura; la configuración local de Claude Code lo tiene bloqueado, ejecútalo el equipo).
- **Prueba:** procedimiento de `verificacion-manual.md`, caso "renovación silenciosa".
- **Impacto:** Copilot Studio, Search y Foundry, ninguno.
- **Rollback:** desactivar las dos variables (la redirect URI extra puede quedar registrada sin efecto).
