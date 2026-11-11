# Matriz RBAC de Daily

**Requiere revisión senior.** No se ha quitado ni asignado ningún rol. La evidencia viene de `az role assignment list --all --subscription b6d04525-e7b4-4b17-8492-2471f9779962` (filtrado por el grupo `rg-daily-enhanced-dev-centralus`) y de `--include-inherited` sobre el grupo, ambos del **2026-11-10**. Los identificadores van truncados.

> **Clasificación:**
> - Los roles actuales y su evidencia son **CURRENT**.
> - Las reducciones propuestas (A10) son **RECOMMENDED NOW**. Se aplican solo con el procedimiento de [lote-7-simplificado.md](lote-7-simplificado.md), sección 14.
> - La identidad que usa Foundry → Search queda **pendiente de la verificación V1** (sección 13 de ese documento).
> - Las filas de CI futura y de identidad de despliegue son **FUTURE**.
>
> No se ha creado ni retirado ninguna asignación.

El rol mínimo de cada relación se aplica siempre con alcance del recurso individual, nunca de grupo ni de suscripción. La plantilla `infra/platform/modules/rbac.bicep` que lo implementaba pertenece al diseño legacy; la nueva versión de Bicep (`internal-minimal`) solo incluirá B2.

## 1. Identidades

| Identidad | Tipo | Identificador | Uso |
|---|---|---|---|
| Web app (`id-web-daily-enhanced-dev`) | Identidad asignada por el usuario | principal `6b88e44e…`, cliente `1c1e6a9a…` | Backend de la Container App → Foundry; extracción de imágenes del ACR |
| Proyecto de Foundry (`prj-daily-enhanced-dev`) | Identidad del sistema del proyecto | principal `d2d03254…` | Herramienta `azure_ai_search` del agente (cuando la conexión use Entra, A1) |
| Cuenta de Foundry (`prj-daily-enhanced-dev-resource`) | Identidad del sistema | sin asignaciones | No se usa |
| Search (`azureairesourceprueba2`) | Identidad del sistema | principal `3d150947…` | Indexador → SQL; skill de embeddings y vectorizador → Azure OpenAI |
| CI futura | — | no existe | La CI actual no se autentica en Azure (decisión del lote 0) |
| Despliegue | Usuarios y principales de servicio | usuario `e71b…` (Owner de la suscripción); principal de servicio `2907…` (Contributor y UAA en la suscripción) | `azd` y scripts de post-aprovisionamiento |

## 2. Matriz

Leyenda de estado: **Confirmado** = rol mínimo verificado con documentación y código; **Pendiente** = necesita la prueba previa antes de cambiar nada.

### 2.1 Web app (identidad de la Container App)

| Destino | Rol actual (evidencia) | Rol mínimo | Alcance | Justificación | Riesgo de retirar el actual | Prueba previa | Estado |
|---|---|---|---|---|---|---|---|
| Foundry, proyecto | **Azure AI Developer**, **Cognitive Services OpenAI Contributor** y **Cognitive Services User** sobre el proyecto | **Cognitive Services User** (ya asignado; corrige la recomendación anterior de Foundry User, que tiene las mismas `dataActions` y más `actions`) | Proyecto | El backend solo usa acciones de datos: leer el agente, conversaciones, Responses y archivos. `Microsoft.CognitiveServices/*` (`dataActions`) de Cognitive Services User las cubre. Azure AI Developer (`MachineLearningServices/*` y `dataActions` incluidas) y OpenAI Contributor (escritura de despliegues) no aportan nada que se use | Si alguna operación no estuviera cubierta, el chat devolvería 403 → 502 | Acción **A10**: retirar de uno en uno Azure AI Developer y después OpenAI Contributor, con las pruebas y el rollback de lote-7-simplificado.md, sección 14 | **Pendiente** (inventario del 2026-11-11 hecho; sin cambios) |
| Foundry, cuenta (Azure OpenAI) | Ninguno | Ninguno | — | El backend llama al endpoint del **proyecto**, no al de Azure OpenAI de la cuenta | — | — | Confirmado (código: `AI_AGENT_ENDPOINT` = endpoint del proyecto) |
| Search | Ninguno | Ninguno | — | El backend no llama a Search; lo hace el agente | — | — | Confirmado (sin SDK de Search en el backend) |
| SQL | Ninguno | Ninguno | — | Sin acceso a SQL | — | — | Confirmado |
| ACR | **AcrPull** sobre `crblx2jbksxc7iu` | AcrPull | Registro | Extracción de la imagen con identidad, sin admin | La Container App no arranca revisiones nuevas | — | Confirmado |
| Key Vault | No existe | Ninguno hoy. *Key Vault Secrets User* sobre el vault solo si en el futuro hiciera falta un secreto | Vault | Hoy no hay secretos: todo va con identidad | — | — | Confirmado |
| Application Insights (backend) | Ninguno (cadena de conexión con autenticación local) | **Monitoring Metrics Publisher** | Componente del backend | Necesario para la ingesta con Entra y para `disableLocalAuth` | Ninguno (es un alta) | Configurar la autenticación Entra del SDK y comprobar que llegan trazas antes de desactivar la autenticación local | Pendiente |
| Application Insights (frontend) | — | Ninguno | — | El SDK del navegador no admite Entra: el componente del frontend **mantiene** la autenticación local | — | — | Confirmado |

### 2.2 Proyecto de Foundry (identidad del sistema)

| Destino | Rol actual (evidencia) | Rol mínimo | Alcance | Justificación | Riesgo de retirar | Prueba previa | Estado |
|---|---|---|---|---|---|---|---|
| Search | **Search Index Data Reader** sobre el servicio | **No participa en el flujo actual** (V1, 2026-11-11: el agente v10 usa la conexión `azureairesourceprueba2` con ApiKey). El rol mínimo con Entra se determinará en A1 (según Learn, la identidad de la cuenta) | Servicio de Search | Se conserva hasta completar A1 | Ninguno hoy | A1, paso 9: retirarlo solo si se demuestra que no es la identidad utilizada | **CURRENT: sin uso; no retirar** |
| Foundry, cuenta | Ninguno | Ninguno | — | El proyecto usa los modelos de su propia cuenta sin rol adicional | — | — | Confirmado |
| SQL, ACR, Key Vault, Application Insights | Ninguno | Ninguno | — | Sin acceso | — | — | Confirmado |

### 2.3 Search (identidad del sistema)

| Destino | Rol actual (evidencia) | Rol mínimo | Alcance | Justificación | Riesgo de retirar | Prueba previa | Estado |
|---|---|---|---|---|---|---|---|
| Foundry, cuenta (Azure OpenAI) | **Cognitive Services OpenAI User** sobre la cuenta | Cognitive Services OpenAI User | Cuenta (no el proyecto) | Skill de embeddings e inferencia del vectorizador sin clave (`resourceUri` de la cuenta, sin `apiKey` ni `authIdentity`) | El indexador falla y las consultas vectoriales devuelven error | — | Confirmado |
| SQL, servidor | **Reader** sobre `knowledgeserver` | Reader | Servidor | Requisito documentado para la conexión con identidad y `ResourceId` (*Set up an indexer connection to Azure SQL using a managed identity*, paso 2) | El indexador no puede conectar | — | Confirmado |
| SQL, base de datos | Usuario contenido `[azureairesourceprueba2]` con CONNECT y SELECT solo sobre `dbo.vw_DailyConocimiento_Search` | Igual | Vista | Mínimo privilegio de datos (T-SQL, fuera de ARM) | El indexador no lee | — | Confirmado (contexto del proyecto) |
| ACR, Key Vault, Application Insights | Ninguno | Ninguno | — | Sin acceso | — | — | Confirmado |

### 2.4 CI futura

| Destino | Rol actual | Rol mínimo (si algún día se añade despliegue continuo) | Alcance | Justificación | Estado |
|---|---|---|---|---|---|
| ACR | Ninguno | AcrPush | Registro | Publicar la imagen | Pendiente (sin CD) |
| Container App | Ninguno | *Container Apps Contributor* | La Container App | Crear revisiones | Pendiente (sin CD) |
| Resto | Ninguno | Ninguno | — | La CI no necesita datos | Confirmado |

Autenticación por **federación OIDC** de GitHub a una identidad dedicada, limitada a la rama protegida y al entorno de GitHub con aprobación. Sin secretos de cliente y sin Owner ni Contributor de suscripción.

### 2.5 Identidad de despliegue

| Destino | Rol actual (evidencia) | Rol mínimo | Alcance | Justificación | Riesgo de retirar | Prueba previa | Estado |
|---|---|---|---|---|---|---|---|
| Recursos de Daily | Usuario `e71b…`: **Owner** de la suscripción. Principal de servicio `2907…`: **Contributor** y **User Access Administrator** de la suscripción | **Contributor** sobre el grupo de recursos de Daily, más **Role Based Access Control Administrator** sobre ese grupo **con condición** que solo permita asignar los 5 roles de `rbac.bicep` (Foundry User, Monitoring Metrics Publisher, Search Index Data Reader, Cognitive Services OpenAI User, Reader) | Grupo de recursos | La plataforma crea el grupo (alcance de suscripción): el primer despliegue lo hace un administrador; los siguientes, la identidad limitada | Si el principal `2907…` lo usa alguna automatización desconocida, esta dejaría de funcionar | Identificar el origen del principal `2907…` (registros de actividad y propietario de la aplicación) | Pendiente |

## 3. Hallazgos fuera del alcance de Daily (solo informar; hallazgo de gobierno A13, escalar al administrador de Azure/Entra de CONSEIN)

- Dos usuarios tienen **User Access Administrator en la raíz (`/`)**. Es una elevación de administrador global que suele quedar activa por olvido. Recomendación para los administradores del inquilino: retirarla y usar PIM.
- La regla `AllowAllWindowsAzureIps` de `knowledgeserver` permite conexiones desde cualquier suscripción de Azure (ver [red-privada.md](red-privada.md)).

## 4. Qué implementaba el Bicep legacy (LEGACY DESIGN - DO NOT DEPLOY)

> En `internal-minimal`:
> - las asignaciones 3, 4 y 5 **ya existen** en los recursos actuales y no se redeclaran;
> - la 1 es B2 (A10), que se aplica con su procedimiento;
> - la 2 es FUTURE (solo hace falta con telemetría autenticada por Entra).

`rbac.bicep` (diseño legacy) creaba, con alcance de recurso:

1. Web app → proyecto: **Foundry User**.
2. Web app → Application Insights del backend: **Monitoring Metrics Publisher**.
3. Proyecto → Search: **Search Index Data Reader**.
4. Search → cuenta de Foundry: **Cognitive Services OpenAI User**.
5. Search → servidor SQL: **Reader**.

AcrPull pertenece al IaC de la web app (`infra/main*.bicep`), que es donde está el ACR. El usuario contenido de SQL se crea con T-SQL ([`infra/platform/README.md`](../../infra/platform/README.md)).
