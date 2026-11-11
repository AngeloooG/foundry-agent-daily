# Matriz RBAC de Daily

**Requiere revisión senior.** No se ha quitado ni asignado ningún rol. La evidencia viene de `az role assignment list --all --subscription b6d04525-e7b4-4b17-8492-2471f9779962` (filtrado por el grupo `rg-daily-enhanced-dev-centralus`) y de `--include-inherited` sobre el grupo, ambos del **2026-11-10**. Los identificadores van truncados.

El rol mínimo de cada relación es el que crea [`infra/platform/modules/rbac.bicep`](../../infra/platform/modules/rbac.bicep) en la plataforma nueva, siempre con alcance del recurso individual, nunca de grupo ni de suscripción.

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
| Foundry, proyecto | **Azure AI Developer**, **Cognitive Services OpenAI Contributor** y **Cognitive Services User** sobre el proyecto | **Foundry User** (`53ca6127…`, antes *Azure AI User*) | Proyecto | El backend solo usa acciones de datos: leer el agente, crear y leer conversaciones, Responses, y subir y descargar archivos. No gestiona despliegues, conexiones ni el proyecto. Los tres roles actuales permiten además crear y borrar agentes y despliegues | Si Foundry User no cubre alguna acción de datos (por ejemplo, Files), el chat devuelve 403 → 502 | Acción **A10**: asignar Foundry User en paralelo, desplegar una revisión de prueba con una identidad nueva que **solo** tenga Foundry User y ejecutar la verificación manual (enviar mensaje, adjuntar imagen, descargar archivo citado, listar conversaciones). Solo después, retirar los tres roles | **Pendiente** |
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
| Search | **Search Index Data Reader** sobre el servicio | Search Index Data Reader | Servicio de Search (opcionalmente, el índice concreto) | Lectura del índice por la herramienta del agente con la conexión Entra (A1). Hoy no se usa porque la conexión es por clave | Hoy ninguno; después de A1, el agente deja de encontrar documentos | A1 | Confirmado |
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

## 3. Hallazgos fuera del alcance de Daily (solo informar)

- Dos usuarios tienen **User Access Administrator en la raíz (`/`)**. Es una elevación de administrador global que suele quedar activa por olvido. Recomendación para los administradores del inquilino: retirarla y usar PIM.
- La regla `AllowAllWindowsAzureIps` de `knowledgeserver` permite conexiones desde cualquier suscripción de Azure (ver [red-privada.md](red-privada.md)).

## 4. Qué implementa el Bicep

`rbac.bicep` crea, con alcance de recurso:

1. Web app → proyecto: **Foundry User**.
2. Web app → Application Insights del backend: **Monitoring Metrics Publisher**.
3. Proyecto → Search: **Search Index Data Reader**.
4. Search → cuenta de Foundry: **Cognitive Services OpenAI User**.
5. Search → servidor SQL: **Reader**.

AcrPull pertenece al IaC de la web app (`infra/main*.bicep`), que es donde está el ACR. El usuario contenido de SQL se crea con T-SQL ([`infra/platform/README.md`](../../infra/platform/README.md)).
