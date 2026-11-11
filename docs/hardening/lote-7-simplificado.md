# Lote 7 simplificado: perfil `internal-minimal`

**Estado:** **APROBADO** (dirección general) el 2026-11-11 como arquitectura objetivo vigente de Daily Enhanced. Es la **fuente de verdad** de las decisiones de infraestructura.

**Verificaciones de solo lectura del 2026-11-11 incluidas:** V1 (Foundry → Search), A9 (cuota de `gpt-5.1`), inventario RBAC de A10 y análisis de C1, C2, C3 y C6.

**Sin cambios en:** Bicep, bicepparam, código, Azure, RBAC, Entra, Search, Foundry, Container Apps, SQL ni Copilot Studio. Sin commits.

**Fuentes:**
- Lecturas de Azure del 2026-11-10 y del 2026-11-11 (comandos en cada sección).
- Microsoft Learn, consultado el 2026-11-11.

## Clasificación obligatoria

| Categoría | Significado |
|---|---|
| **CURRENT** | Estado real que funciona hoy |
| **RECOMMENDED NOW** | Cambio apropiado para el proyecto interno actual |
| **FUTURE** | Arquitectura avanzada que **no** se despliega ahora |
| **LEGACY - DO NOT DEPLOY** | Diseño anterior descartado; se conserva como evidencia |
| **CLOSED** | Hallazgo cerrado: no es un problema actual |

Clasificación técnica usada para analizar el diseño anterior:

| Cat. | Significado |
|---|---|
| A | Existente y reutilizable |
| B | Necesario |
| C | Opcional |
| D | Solo red privada |
| E | Solo alta disponibilidad |
| F | Innecesario para Daily interno |

En `internal-minimal`, D, E y F quedan fuera, y los objetos C están desactivados por defecto y requieren aprobación individual.

## 0. Decisiones vigentes

| # | Decisión | Clasificación |
|---|---|---|
| 1 | `internal-minimal` es la arquitectura objetivo; la red privada completa es solo roadmap | — |
| 2 | Search: desarrollo continúa en `azureairesourceprueba2` (Free). No se migra, no se cambia el SKU, no se crea otro Search. Basic es **RECOMMENDED FOR FUTURE PRODUCTION**. S1 solo si una función concreta lo exige | CURRENT / FUTURE |
| 3 | What-if del diseño anterior: se conservan, no se ejecutan y no se borran | LEGACY - DO NOT DEPLOY |
| 4 | `CLAUDE.local.md`: secciones 1 (un lote a la vez, con autorización) y 6 (`internal-minimal`) actualizadas | — |
| 5 | Sin Key Vault (no hay secretos aplicativos) | CURRENT |
| 6 | 0 recursos nuevos obligatorios | — |
| 7 | V1 aceptada: Foundry v10 consulta Search con **ApiKey** (sección 13). No se cambia la conexión, no se migra a Entra, no se desactiva `disableLocalAuth`, no se toca la clave y no se retira el rol del proyecto. La migración es la tarea A1 (13.6) | CURRENT · A1: RECOMMENDED NOW |
| 8 | A10: hipótesis de mínimo privilegio aceptada como **candidata**: KEEP Cognitive Services User y AcrPull; CANDIDATE TO REMOVE Azure AI Developer y Cognitive Services OpenAI Contributor; DO NOT ADD Foundry User. Retirada secuencial como tarea posterior (14.6). Sin cambios de RBAC ahora | RECOMMENDED NOW |
| 9 | A11, red privada | FUTURE – NOT FOR CURRENT DEPLOYMENT |
| 10 | A12, `AllowAllWindowsAzureIps` | FUTURE – Future infrastructure hardening |
| 11 | A13, asignaciones administrativas | Hallazgo de gobierno externo; escalar a CONSEIN |
| 12 | A9: el error pertenecía a la infraestructura legacy; el despliegue actual funciona (sección 15) | **CLOSED / LEGACY INFRASTRUCTURE FINDING** |
| 13 | C1: RECOMMENDED, no activar todavía. C2 y C3: **una** alerta consolidada, RECOMMENDED. C6: OPTIONAL / PENDING TELEMETRY BASELINE. El primer despliegue de `internal-minimal` no crea alertas (parámetros desactivados) | RECOMMENDED NOW / OPTIONAL |
| 14 | Bicep: no se empieza hasta cerrar V1, A9, A10 y los opcionales. Debe distinguir recursos gestionados de recursos referenciados | — |

---

## 1. Cantidad de recursos propuesta anteriormente (LEGACY)

Según los what-if del 2026-11-10 ([whatif/](whatif/README.md), LEGACY - DO NOT DEPLOY):

| Perfil anterior | Objetos ARM nuevos | Desglose |
|---|---|---|
| `main.dev.bicepparam` | **22** | 17 creaciones más 5 asignaciones de rol, en un grupo de recursos **nuevo** |
| `main.prod.bicepparam` | **45** | 40 creaciones más 5 asignaciones de rol, en otro grupo nuevo |

Los dos duplicaban Foundry, Search, SQL, Log Analytics, Application Insights y la identidad. Producción añadía además 21 objetos de red:

- 1 VNet;
- 6 zonas DNS privadas y sus 6 vínculos;
- 4 private endpoints y sus 4 grupos de zonas DNS.

## 2. Por qué se proponía cada grupo

El diseño anterior seguía la especificación original del lote 7 de `CLAUDE.local.md`, que pedía:

- SQL dedicada;
- Key Vault;
- private endpoints y zonas DNS privadas;
- vínculos privados compartidos;
- nombres sin "prueba".

| Grupo | Motivo |
|---|---|
| Grupo y servicios nuevos | Nombres sin "prueba"; separar la plataforma de `azd`; la VNet de Foundry y de Container Apps solo se configura al crear el recurso |
| Red privada | Aislamiento completo por Private Link |
| Search S1 × 2 | SLA y conexiones privadas de un indexador con skillset |
| SQL GP_Gen5_2 | "SQL dedicada" de la especificación |
| Foundry nuevo | Nombres e inyección de VNet del Agent Service |
| Key Vault | Regla de la organización para secretos |
| Log Analytics y Application Insights nuevos | Plataforma autocontenida |
| 5 asignaciones de rol | Mínimo privilegio en la plataforma nueva |
| Alerta del indexador | Observabilidad de la ingesta |

## 3. Recursos eliminados de la nueva propuesta

| Recurso propuesto antes | Cat. | Motivo |
|---|---|---|
| Grupo `rg-daily-<env>-centralus` | A | Se reutiliza `rg-daily-enhanced-dev-centralus` |
| Log Analytics y Application Insights nuevos | A | Se reutilizan `law-blx2jbksxc7iu`, `appi-blx2jbksxc7iu` y `appi-fe-blx2jbksxc7iu` |
| Identidad `id-web-daily-<env>` | A | Se reutiliza `id-web-daily-enhanced-dev` |
| Cuenta, proyecto y despliegues de Foundry nuevos | A | Se reutilizan los actuales; esto evita el problema de cuota del Bicep legacy (sección 15) |
| `networkInjections` y capability host | D | Solo red privada |
| Search nuevo (Basic en dev, S1 × 2 en prod) | A/E | Se reutiliza `azureairesourceprueba2` (Free) |
| Vínculos privados compartidos | D | Solo red privada |
| SQL nueva y GP_Gen5_2 | A / E-F | Se reutilizan `knowledgeserver` y `free-sql-db-knowledge-specialist` |
| Key Vault | F | Sin secretos aplicativos (anexo C) |
| VNet, subredes, zonas DNS, vínculos, private endpoints | D | Solo red privada |
| Rol *Monitoring Metrics Publisher* para la web | F | Solo para telemetría autenticada con Entra (FUTURE) |
| Perfil de producción con plataforma propia | F | Un solo diseño con parámetros |

## 4. Recursos actuales reutilizados (CURRENT)

| Recurso | Papel | Gestionado por el repositorio o externo referenciado (propuesta para el Bicep) |
|---|---|---|
| `rg-daily-enhanced-dev-centralus` | Grupo único | Externo (se despliega dentro) |
| `ca-web-blx2jbksxc7iu` | SPA y API | **Gestionado** (hoy lo despliega `azd`) |
| `cae-blx2jbksxc7iu` | Entorno de Container Apps | Por decidir (hoy lo creó `azd`) |
| `crblx2jbksxc7iu` | Registro | Por decidir (hoy lo creó `azd`) |
| `id-web-daily-enhanced-dev` | Identidad de la web, asignada solo a `ca-web-blx2jbksxc7iu` | Por decidir |
| `prj-daily-enhanced-dev-resource` / `prj-daily-enhanced-dev` y sus despliegues | Agente `Daily-Enhanced` v10 y modelos | **Externo referenciado** (compartido) |
| `azureairesourceprueba2` (Free) | Índice compartido con Copilot Studio | **Externo referenciado** |
| `knowledgeserver` / `free-sql-db-knowledge-specialist` | Fuente de verdad del conocimiento | **Externo referenciado** |
| `storageresourceprueba` | Sin uso en el código de Daily | Fuera de alcance |
| `law-blx2jbksxc7iu`, `appi-blx2jbksxc7iu`, `appi-fe-blx2jbksxc7iu` | Observabilidad de la web | Por decidir (hoy los creó `azd`) |
| `prj-daily-enhanced-dev-resource-appinsights` y `-logs` | Observabilidad de Foundry | Externo |

Arquitectura vigente (verificada con V1):

```
Usuario CONSEIN ──► Microsoft Entra ID (token, tid, scope Chat.ReadWrite)
        │ HTTPS
        ▼
Container App ca-web-blx2jbksxc7iu (SPA + API)
        │ identidad administrada id-web-daily-enhanced-dev (Cognitive Services User sobre el proyecto, entre otros)
        ▼
Foundry: agente Daily-Enhanced v10 → asset de índice v2 → conexión azureairesourceprueba2 (ApiKey)
        │ clave de API (conexión de Foundry)
        ▼
Azure AI Search azureairesourceprueba2 (Free) ◄── Copilot Studio (su propia conexión de Power Platform, clave temporal)
        │ identidad del sistema de Search
        ▼
Azure SQL knowledgeserver / free-sql-db-knowledge-specialist
```

## 5. Recursos nuevos

### 5.1 Obligatorios: **0**

| Cambio RECOMMENDED NOW | ¿Recurso nuevo? | Estado |
|---|---|---|
| B1. Conexión de Foundry a Search con Entra (A1) | No: es un subrecurso de configuración del proyecto existente | Tarea independiente con migración controlada (13.6); no implementada |
| B2. Reducción de roles de la identidad web (A10) | **No: solo retirar roles.** No hace falta añadir ninguno (sección 14) | Tarea posterior con retirada secuencial (14.6); sin cambios ahora |

### 5.2 Azure AI Search

| Aspecto | Clasificación | Detalle |
|---|---|---|
| `azureairesourceprueba2` (Free) como desarrollo y pruebas | **CURRENT** | Se mantiene mientras funcione. No se migran índices, no se cambia el SKU, no se crea otro servicio |
| Identidad saliente en Free | **CURRENT, riesgo conocido** | Funciona hoy, pero según Learn no está disponible en Free (*"Managed or trusted identities for outbound (indexer) access: Not available on the Free tier"*, [search-sku-tier](https://learn.microsoft.com/azure/search/search-sku-tier#feature-availability-by-tier)). **No se intenta corregir** mientras funcione; no debe ser la base de producción |
| Basic | **RECOMMENDED FOR FUTURE PRODUCTION** | Soporta todas las funciones de Daily sin red privada (anexo B) |
| S1 o superior | No recomendado | Solo con una función concreta que lo exija |
| Free → Basic | FUTURE | No se puede hacer sobre el mismo servicio (*"you can't switch to or from Free"*). Requerirá un servicio nuevo y reconstruir el índice desde SQL |

Criterio para cualquier recurso nuevo futuro: ver la sección 12.

## 6. Objetos opcionales: desactivados por defecto

Detalle completo de C1, C2, C3 y C6 en la sección 22. **El primer despliegue de `internal-minimal` no crea ninguna alerta**: todos los opcionales son parámetros desactivados por defecto. No se crean recursos de observabilidad adicionales; se reutilizan Application Insights y Log Analytics existentes.

| # | Objeto | Clasificación |
|---|---|---|
| C1 | Grupo de acción (correo) | RECOMMENDED, **no activar todavía** |
| C2/C3 | **Una** alerta consolidada de errores críticos del backend y de sus dependencias (500, 502, 503, 504) | RECOMMENDED, desactivada por defecto |
| C6 | Límite diario de Log Analytics | **OPTIONAL / PENDING TELEMETRY BASELINE** |
| C4 | Diagnóstico de Search → Log Analytics | OPTIONAL |
| C5 | Alerta de fallos del indexador | OPTIONAL |
| C7 | Diagnóstico de Foundry | OPTIONAL |
| C8 | Auditoría de SQL | OPTIONAL |

## 7. Comparación

| Aspecto | LEGACY (prod) | `internal-minimal` |
|---|---|---|
| Objetos ARM nuevos | 45 | **0 obligatorios**; B1 es configuración; B2 solo retira roles; hasta 8 opcionales sujetos a aprobación |
| Red | Privada completa | Pública con autenticación |
| Foundry, Container Apps, SQL, Search | Nuevos | Existentes |
| Key Vault | Sí | No |
| Copilot Studio | VNet de Power Platform | Solo clave de consulta dedicada (A6) |
| Usuarios | URL e historial nuevos | Sin cambios, salvo el historial anterior que deja de verse (lote 2) |
| Bicep | 10 módulos que lo declaran todo | Recursos gestionados frente a externos referenciados |

## 8. Complejidad operativa

| Actividad | LEGACY | `internal-minimal` |
|---|---|---|
| Despliegue | Plataforma nueva, migración de datos, agente nuevo | Imagen nueva (A2) |
| Pasos manuales | Private Link, DNS, Power Platform | A1, A6, A10 |
| Diagnóstico | DNS privado y Private Link | Autenticación y RBAC |
| Rollback | Por fases, con recursos duplicados | Revisión anterior y versión anterior del agente |

## 9. Impacto de costes (cualitativo)

| Concepto | LEGACY | `internal-minimal` |
|---|---|---|
| Search | S1 × 2 | Free (0); Basic en el futuro |
| SQL | Aprovisionada | Oferta gratuita |
| Red y Key Vault | Private endpoints, DNS, Key Vault | 0 |
| Observabilidad | Espacios nuevos | Actuales; las alertas tienen coste muy bajo; C6 limita el coste |
| **Total** | Alto | **Prácticamente igual al actual** |

## 10. Riesgos residuales aceptados (no son vulnerabilidades críticas)

| # | Riesgo | Controles compensatorios | Si se aborda |
|---|---|---|---|
| R1 | Endpoints públicos de Foundry, Search, SQL y ACR | Autenticación obligatoria en todos; SQL solo Entra; ACR sin admin ni pull anónimo | FUTURE (A11) |
| R2 | Autenticación local en Search. **Dos consumidores con clave confirmados:** la conexión de Foundry (V1) y Copilot Studio | A1 (Foundry con Entra), A6 (clave de consulta para Copilot Studio); `disableLocalAuth` solo tras el inventario | RECOMMENDED NOW (A1, A6) |
| R3 | **Riesgo RECOMMENDED NOW.** La identidad web tiene *Cognitive Services User* y la conexión Foundry → Search usa ApiKey. Según el análisis actual, ese rol puede permitir acceder a información de la conexión (`projects/connections/listsecrets/action`), aunque la aplicación no lo usa | **No** se resuelve ajustando permisos de forma arbitraria. La mitigación estratégica es **A1** (Foundry → Search con Entra ID / identidad administrada). Pasos posteriores a A1 en la sección 13.6 | RECOMMENDED NOW (A1) |
| R4 | `AllowAllWindowsAzureIps` en SQL | SQL solo Entra; el usuario de Search solo tiene `SELECT` sobre una vista | FUTURE (A12) |
| R5 | Search Free: sin SLA, posible borrado por inactividad, identidad saliente no documentada | El índice se reconstruye desde SQL; Basic para producción | FUTURE |
| R6 | Ingress público de la Container App | Entra, `tid` y scope; propiedad con 404; rate limit; límites; CSP; HTTPS | — |
| R7 | Ingesta pública de Application Insights, sin límite diario (`dailyQuotaGb: -1`) | Telemetría sin contenido de usuario; C6 solo tras medir una línea base | OPTIONAL / PENDING TELEMETRY BASELINE (C6) |
| R8 | Exceso de roles de la identidad web | Sección 14 | RECOMMENDED NOW |
| R9 | **Sin alertas** (no hay grupos de acción ni reglas de alerta en el grupo de recursos) | C1 y la alerta consolidada C2/C3 | RECOMMENDED NOW (no se activan en el primer despliegue) |
| R10 | Una región y una réplica | Escalado a cero con reinicio; reconstrucción desde SQL | FUTURE |
| R11 | Asignaciones administrativas amplias | A13 | Fuera del proyecto |

## 11. Controles compensatorios

| Control | Estado |
|---|---|
| Entra ID, `tid` y scope en la política por defecto; propiedad con 404 | CURRENT (rama; se despliega con A2) |
| CSP con MSAL, `sessionStorage`, cabeceras, sanitización, rate limit, límites, validación de archivos, streaming idempotente, logging seguro | CURRENT (rama) |
| Identidad administrada (web → Foundry; Search → SQL y Azure OpenAI) | CURRENT |
| RBAC mínimo | RECOMMENDED NOW (A10, sección 14) |
| Alertas | RECOMMENDED NOW: C1 y alerta consolidada C2/C3 (desactivadas por defecto). C6: pendiente de línea base |
| Clave de consulta dedicada para Copilot Studio | RECOMMENDED NOW (A6) |
| Dependencias auditadas en la CI | CURRENT |

## 12. Criterio para añadir cualquier recurso nuevo

Ningún recurso nuevo se propone sin responder a estas preguntas:

1. ¿Qué problema concreto resuelve?
2. ¿Por qué no puede resolverlo un recurso existente?
3. ¿Es necesario para que Daily funcione?
4. ¿Corrige una vulnerabilidad concreta, o es solo defensa en profundidad?
5. ¿Qué coste y qué complejidad añade?
6. ¿De qué depende?
7. ¿Qué pasa si no se despliega?

Tiene prioridad resolver con código, configuración, RBAC, Entra ID, identidad administrada, CI o tests.

## 13. V1: Foundry → Azure AI Search (solo lectura, 2026-11-11)

### 13.1 Conclusión: **C. Foundry está consultando Search con API key.**

El agente que usa la web app es `Daily-Enhanced` v10, que además es la última versión publicada. Su herramienta `azure_ai_search` usa el *asset* de índice `index-dailyprueba5vectorsearch/versions/2`. Ese asset apunta a la conexión `azureairesourceprueba2`, cuyo `authType` es **`ApiKey`**.

Ni la identidad del proyecto ni la de la cuenta intervienen en las consultas de v10. El rol *Search Index Data Reader* de la identidad del proyecto existe, pero **no lo usa** la configuración actual.

### 13.2 Evidencia (cadena completa)

| # | Comprobación | Comando (solo lectura) | Resultado |
|---|---|---|---|
| 1 | Agente y versión que usa la web | `az containerapp show --name ca-web-blx2jbksxc7iu … --query "properties.template.containers[0].env[?name=='AI_AGENT_ID' \|\| name=='AI_AGENT_VERSION' \|\| name=='AI_AGENT_ENDPOINT']"` | `AI_AGENT_ID=Daily-Enhanced`, `AI_AGENT_VERSION=10`, endpoint del proyecto `prj-daily-enhanced-dev` |
| 2 | Versión más reciente del agente | `az rest --method get --resource https://ai.azure.com --url ".../agents/Daily-Enhanced?api-version=v1"` | `latest: 10` |
| 3 | Herramienta de v10 | `az rest … /agents/Daily-Enhanced/versions?api-version=v1` | v10 (2026-09-29), `gpt-5.1`: `azure_ai_search` → `index_asset_id: index-dailyprueba5vectorsearch/versions/2`, `query_type: simple`, `top_k: 5`. **No** referencia una conexión directa |
| 4 | Asset del índice v2 | `az rest … /indexes/index-dailyprueba5vectorsearch/versions/2?api-version=v1` | `type: AzureSearch`, **`connectionName: azureairesourceprueba2`**, `indexName: index-dailyprueba5vectorsearch` |
| 5 | Conexión del proyecto | `az cognitiveservices account project connection list …` y `az rest … /projects/prj-daily-enhanced-dev/connections?api-version=2025-06-01` | Una sola conexión: `azureairesourceprueba2`, categoría `CognitiveSearch`, target `https://azureairesourceprueba2.search.windows.net/`, **`authType: ApiKey`**, `isSharedToAll: true`, `ResourceId` del Search actual |
| 6 | Conexión de la cuenta | `az cognitiveservices account connection list …` | La misma: `azureairesourceprueba2`, **`ApiKey`**, compartida |
| 7 | Search | `az search service show … --query "{sku,authOptions,disableLocalAuth,…}"` | `sku: free`; `authOptions: aadOrApiKey` (`http401WithBearerChallenge`); **`disableLocalAuth: false`**; acceso público habilitado; identidad del sistema `3d150947…` |
| 8 | Identidades de Foundry | `az cognitiveservices account show … --query identity` · `az rest … /projects/prj-daily-enhanced-dev?api-version=2025-06-01 --query identity` | Cuenta: `9b857aa1-60c5-4933-be30-fab6a3a92105`. Proyecto: `d2d03254-2e5a-4867-9ada-b0016c3c93e1` |
| 9 | Roles efectivos sobre Search (directos y heredados de raíz, suscripción y grupo) | `az role assignment list --all --subscription … --query "[?scope=='/' \|\| … \|\| contains(scope,'searchServices/azureairesourceprueba2')]"` | Proyecto `d2d03254…`: **Search Index Data Reader** (alcance: servicio de Search). Usuario `e71b…`: Search Index Data Reader (servicio), Owner y Foundry User (suscripción). Principal de servicio `2907…`: Contributor y UAA (suscripción). Dos usuarios: UAA en `/`. **Cuenta `9b857aa1…`: ningún rol** |

Ningún comando devolvió claves (no se usó `listsecrets` ni `listkeys`).

### 13.3 Cómo se resuelve la inconsistencia con la observación "Project Managed Identity"

| Hipótesis | Evidencia |
|---|---|
| Era **otra conexión** | Las versiones 7 y 8 del agente referenciaban directamente `…/connections/azureairesourcepruebarqus7d` (`vector_semantic_hybrid`). El asset de índice v1 también apunta a `azureairesourcepruebarqus7d`. **Esa conexión ya no existe**: un GET a nivel de proyecto y de cuenta devuelve *"Connection azureairesourcepruebarqus7d can't be found"* (404) |
| Era **otra versión del agente** | Versiones históricas del 2026-09-29: v6 (asset v1), v7 y v8 (conexión `…rqus7d`), v9 (sin herramientas), **v10 (asset v2 → `azureairesourceprueba2`, ApiKey)** |
| Es la conexión actual con otra autenticación | **Descartado** para hoy: ARM (CLI y `2025-06-01`) devuelve `ApiKey` para `azureairesourceprueba2` a nivel de proyecto y de cuenta |
| Es la conexión de Copilot Studio | Copilot Studio usa una conexión de Power Platform independiente, que no aparece en Foundry (ver 13.4) |

**Conclusión:** la configuración con identidad administrada del proyecto que se observó existió con alta probabilidad en `azureairesourcepruebarqus7d`, usada por versiones anteriores del agente. Esa conexión fue eliminada y no se puede verificar su `authType`. El agente actual no la usa.

Si el portal de Foundry muestra hoy "Project Managed Identity" para `azureairesourceprueba2`, contradiría ARM y convendría revisar esa captura (fecha y nombre exacto).

### 13.4 Conexión de Foundry frente a conexión de Copilot Studio

| | Conexión de Foundry | Conexión de Copilot Studio |
|---|---|---|
| Dónde vive | Foundry: `azureairesourceprueba2` (cuenta y proyecto) | Power Platform (conector Azure AI Search), fuera de Azure |
| Autenticación | **ApiKey** (verificado en V1) | Clave de API temporal (contexto del proyecto) |
| Tipo de clave | No verificado (requeriría `listsecrets`, prohibido) | No verificado (A6) |
| Acción | A1: conexión paralela con Entra y versión nueva del agente | A6: clave de consulta dedicada `daily-copilot-studio` |

Son **dos consumidores independientes** de claves de Search. Los dos deben migrar antes de considerar `disableLocalAuth`.

**No se asume que compartan la misma clave.** Copilot Studio no se modifica. A6 (RECOMMENDED NOW): crear una clave de consulta dedicada exclusivamente para Copilot Studio **solo si** se confirma que hoy usa una clave de administración. Antes hay que verificar el tipo de clave actual.

### 13.5 Qué recomienda Microsoft (para diseñar B1, no aplicado)

Para agentes de Foundry (Responses API, proyecto dentro de una cuenta `AIServices`), la documentación actual indica que, sin claves, el AI Search tool usa la **identidad del sistema de la cuenta** de Foundry, con Search Index Data Contributor y Search Service Contributor ([fuente](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/ai-search#prerequisites)).

Hoy la cuenta (`9b857aa1…`) no tiene ningún rol sobre Search; el proyecto sí tiene lectura. El diseño de B1 debe partir de esto y probar primero solo lectura. Los roles de *Contributor* solo se concederían si falla una operación concreta, documentándola.

**No se ha cambiado** la conexión, el agente, los roles, Search, las claves ni las identidades.

### 13.6 Tarea A1: Foundry ApiKey → Managed Identity / Entra ID (RECOMMENDED NOW, no implementada)

Tarea independiente, con migración controlada y pruebas. Requiere autorización propia y revisión senior. Hasta entonces **no** se cambia la conexión actual, no se migra Foundry a Entra, no se desactiva `disableLocalAuth`, no se rota ni elimina la clave y no se retira el rol *Search Index Data Reader* del proyecto.

1. Identificar qué identidad soporta oficialmente la conexión de Foundry con Entra ID para este tipo de proyecto y agente (la documentación actual indica la identidad de la **cuenta**; confirmarlo en la fecha de ejecución).
2. Crear una conexión nueva con Entra ID (en paralelo) o modificar la existente, según lo que indique el paso 1. Preferencia: en paralelo, para mantener el rollback.
3. Asignar **únicamente** el rol necesario a esa identidad, empezando por *Search Index Data Reader*.
4. Probar la recuperación desde Search con la nueva conexión.
5. Probar `Daily-Enhanced` directamente en Foundry (versión nueva del agente que use la conexión Entra).
6. Probar la web app con esa versión (`AI_AGENT_VERSION`).
7. Probar Copilot Studio para demostrar que su conexión independiente no se ve afectada.
8. Mantener el rollback hacia la conexión ApiKey actual (`AI_AGENT_VERSION=10`), sin borrarla.
9. Retirar *Search Index Data Reader* de la identidad del proyecto **solo** si se demuestra que finalmente no es la identidad utilizada.

Después de validar A1 (mitigación de R3):

- eliminar la dependencia de ApiKey en la conexión de Foundry;
- comprobar los consumidores de claves;
- decidir si se rota la clave;
- revisar si sigue siendo necesaria para Copilot Studio;
- mantener Copilot Studio con una clave de consulta independiente mientras haga falta.


## 14. A10: inventario RBAC de la identidad web (solo lectura, 2026-11-11)

### 14.1 Identidad

| Dato | Valor |
|---|---|
| Nombre | `id-web-daily-enhanced-dev` (asignada por el usuario) |
| Client ID | `1c1e6a9a-7c56-488c-89fe-1d7df1621553` |
| Principal ID | `6b88e44e-c115-48d4-baa6-8c16efe371dc` |
| Recursos que la usan (búsqueda en toda la suscripción) | Solo `ca-web-blx2jbksxc7iu` |
| Uso en el código | `MANAGED_IDENTITY_CLIENT_ID` → `ManagedIdentityCredential(FromUserAssignedClientId)` ([AgentFrameworkService.cs:123](../../backend/WebApp.Api/Services/AgentFrameworkService.cs#L123)) |
| Comandos | `az identity show …`, `az role assignment list --all --assignee 6b88e44e-c115-48d4-baa6-8c16efe371dc …`, `az resource list … contains(to_string(identity.userAssignedIdentities),'id-web-daily-enhanced-dev')`, `az role definition list --name "<rol>"` |

### 14.2 Operaciones que usa la aplicación (según el código)

Todas son operaciones del **plano de datos** del proyecto de Foundry:

- leer la versión del agente (`GetAgentVersionAsync`, `GetAgentVersionsAsync`);
- respuestas en streaming (`ProjectResponsesClient`);
- conversaciones: crear, leer, actualizar metadata, listar, leer mensajes;
- archivos: subir (`UploadFileAsync`), borrar los huérfanos (`DeleteFileAsync`) y descargar (`DownloadFileAsync`, incluidos los archivos de contenedor).

El código **no** hace operaciones del plano de control (despliegues, políticas RAI, conexiones), **no** lee claves (`listkeys`) y **no** lee secretos de conexiones (`listsecrets`).

### 14.3 Matriz

| Rol actual | Alcance | Uso real | Evidencia | Candidato a retirar | Prueba necesaria | Rollback |
|---|---|---|---|---|---|---|
| **Azure AI Developer** (`64702f94…`), asignado el 2026-09-29 | Proyecto `prj-daily-enhanced-dev` | **Previsiblemente ninguno.** Sus `actions` son `Microsoft.MachineLearningServices/workspaces/*` (modelo de hubs de ML, no aplica a un proyecto `AIServices`). Sus `dataActions` (`OpenAI/*`, Speech, ContentSafety, MaaS) están **incluidas** en las de Cognitive Services User | Definición del rol; asignación con alcance de proyecto | **Sí, el primero** | Pruebas 1–7 (14.4) | `az role assignment create --assignee-object-id 6b88e44e-c115-48d4-baa6-8c16efe371dc --assignee-principal-type ServicePrincipal --role "Azure AI Developer" --scope <id del proyecto>` |
| **Cognitive Services OpenAI Contributor** (`a001fd3d…`) | Proyecto | **Previsiblemente ninguno.** Sus `dataActions` (`OpenAI/*`) están incluidas en Cognitive Services User. Sus `actions` de escritura (crear y borrar despliegues, políticas RAI, planes) no las usa el código | Definición del rol; búsqueda en el código | **Sí, el segundo** | Pruebas 1–7 | Igual, con `--role "Cognitive Services OpenAI Contributor"` |
| **Cognitive Services User** (`a97b65f3…`) | Proyecto | **Sí.** `dataActions: Microsoft.CognitiveServices/*` (salvo suplantación de agentes y despliegues de *fine-tuning*) cubre agente, respuestas, conversaciones y archivos | Definición del rol frente a las operaciones de 14.2 | **No: es el mínimo candidato** | — | — |
| **AcrPull** (`7f951dda…`) | Registro `crblx2jbksxc7iu` | **Sí:** descarga de la imagen al crear revisiones | Asignación; registro sin admin | **No** | — | — |
| *Foundry User* (`53ca6127…`) | **No asignado** | — | Sus `dataActions` y `notDataActions` son **idénticas** a las de Cognitive Services User. Sus `actions` son **más amplias**: `Microsoft.Authorization/*/read`, `Microsoft.PolicyInsights/*/read`, `Microsoft.Resources/deployments/*` | **No añadir.** No aporta ningún permiso que la aplicación necesite y ampliaría el plano de control. **Esto corrige la recomendación anterior** de sustituir los tres roles por Foundry User | — | — |

**Mínimo candidato: Cognitive Services User (proyecto) más AcrPull (registro).** No hay que añadir ningún rol; se retirarían 2, de uno en uno.

**Observación (R3):** tanto Cognitive Services User como Foundry User incluyen `Microsoft.CognitiveServices/accounts/projects/connections/listsecrets/action`. Con alcance de proyecto, la identidad web **podría** leer la clave de Search guardada en la conexión de Foundry, aunque el código no lo hace. Se mitiga con A1. Un rol personalizado sin esa acción sería FUTURE.

### 14.4 Pruebas después de cada retirada

1. `GET /api/agent` → 200, sin `Instructions`.
2. Chat: enviar un mensaje y recibir respuesta.
3. Streaming: chunks y `done`; cancelar no muestra error.
4. Foundry, archivos: adjuntar una imagen (subida) y descargar un archivo citado.
5. Conversaciones: listar y abrir una propia.
6. Search, de forma indirecta: una pregunta conocida que responda con cita del índice. No depende de la identidad web, porque la conexión usa clave, pero confirma el flujo completo.
7. Logs de Application Insights sin 403 ni 502 nuevos.

### 14.5 Revisión de lo afirmado antes sobre las opciones A y B

- **Una identidad administrada asignada por el usuario no usa redirect URI.** La afirmación anterior era imprecisa. Quien la necesita es la **SPA**: MSAL usa `redirectUri: window.location.origin` ([authConfig.ts:30](../../frontend/src/config/authConfig.ts#L30)), y una revisión con etiqueta se sirve en otro nombre de host. Para iniciar sesión en la SPA desde esa URL habría que registrar ese origen como redirect URI de tipo SPA en el registro de Entra de la aplicación. Esto afecta a cualquier prueba en el navegador sobre una revisión aislada, con A o con B, y no tiene relación con la identidad.
- **En Container Apps, las identidades administradas se configuran a nivel de aplicación, no de revisión.** La opción B ("identidad temporal solo en la revisión aislada") no es literal. Habría que asignar la identidad temporal a la app y hacer que la revisión aislada la seleccione con `MANAGED_IDENTITY_CLIENT_ID`, que el código admite. Eso es un cambio de configuración de la app y un recurso temporal.
- **Con el inventario, la reducción es solo de retiradas** (no hay que añadir roles). Eso simplifica la opción A: retirar un rol en una ventana de bajo uso, probar en la URL principal (sin redirect URI adicional) y, si falla, volver a asignarlo (propagación de RBAC de varios minutos).

**No se ha cambiado RBAC, no se ha creado ninguna identidad y no se ha retirado ningún permiso.** Decisión del 2026-11-11: **sin identidad temporal** (la opción B queda descartada). La reducción se hará sobre la identidad real, de forma secuencial (14.6).

### 14.6 Tarea A10: retirada secuencial (RECOMMENDED NOW, tarea posterior, no implementada)

Hipótesis de mínimo privilegio aceptada como **candidata**, no como cambio aprobado. Requiere autorización propia y revisión senior.

| Estado candidato | Roles |
|---|---|
| **KEEP** | Cognitive Services User (proyecto) y AcrPull (registro) |
| **CANDIDATE TO REMOVE** | Azure AI Developer y Cognitive Services OpenAI Contributor |
| **DO NOT ADD** | Foundry User |

Reglas:
- no retirar dos roles a la vez;
- no crear identidades temporales;
- cada paso, con la identidad real;
- esperar a que se propague RBAC (varios minutos) antes de validar.

**Paso 1: retirar solamente Azure AI Developer** (alcance: proyecto `prj-daily-enhanced-dev`).

Validar:
1. Container App en estado `Healthy` (revisión activa en ejecución);
2. inicio de sesión;
3. `GET /api/agent` = 200;
4. carga de la metadata de `Daily-Enhanced`;
5. creación de una conversación;
6. streaming de una respuesta;
7. una consulta que invoque Azure AI Search (respuesta con cita del índice);
8. adjuntos (imagen y archivo, que están implementados) y descarga de un archivo citado;
9. cancelación; la regeneración, si la interfaz la ofrece;
10. logs sin 401 ni 403 hacia Foundry (y sin 502 nuevos en el backend).

- **Si falla:** restaurar Azure AI Developer (`az role assignment create --assignee-object-id 6b88e44e-c115-48d4-baa6-8c16efe371dc --assignee-principal-type ServicePrincipal --role "Azure AI Developer" --scope <id del proyecto>`) y repetir la validación.
- **Si funciona:** esperar tu validación antes de continuar.

**Paso 2: retirar Cognitive Services OpenAI Contributor.** Repetir exactamente las mismas pruebas.

- **Si falla:** restaurar **solo** Cognitive Services OpenAI Contributor.

Al terminar: actualizar `matriz-rbac.md` con los roles finales y la evidencia.


## 15. A9: cuota de `gpt-5.1` (solo lectura, 2026-11-11)

### 15.1 Conclusión

| | Resultado |
|---|---|
| **CURRENT PROBLEM** | **No existe.** El despliegue `gpt-5.1` de Daily existe, está en `Succeeded` y tiene capacidad **500**. El agente v10 usa `gpt-5.1` |
| **LEGACY BICEP PROBLEM** | **Sí.** El Bicep legacy intentaba crear un despliegue **nuevo** de `gpt-5.1` en una cuenta nueva, y en la suscripción y región quedan 0 unidades disponibles |

**Clasificación: CLOSED / LEGACY INFRASTRUCTURE FINDING.** A9 **no** figura entre los bloqueantes actuales de Daily.

Única advertencia que se conserva: **NO recrear, redimensionar ni sustituir automáticamente el deployment actual de `gpt-5.1` sin revisar primero la cuota disponible.** Con el límite actual (250), una recreación no recuperaría las 500 unidades. Por eso el futuro Bicep no debe declarar despliegues de modelos: solo referenciarlos.

### 15.2 Evidencia

| Dato | Valor |
|---|---|
| Modelo | `gpt-5.1`, versión `2025-11-13` |
| Región | centralus |
| Despliegue actual | `gpt-5.1` en `prj-daily-enhanced-dev-resource`, SKU GlobalStandard, **capacidad 500**, estado `Succeeded` |
| Cuota (`OpenAI.GlobalStandard.gpt-5.1`) | Uso 500, límite 250, disponible 0 |
| Unidad | El API devuelve `Count`; el error de ARM la define como *"One Thousand Tokens Per Minute"*: 1 unidad = 1.000 TPM, así que 500 unidades = 500.000 TPM |
| Interpretación del uso por encima del límite | Las 500 unidades en uso son exactamente las del despliegue de Daily. El límite actual (250) es inferior a la asignación existente: probablemente el límite se redujo después de crear el despliegue, y la asignación previa se conserva. No afecta al funcionamiento |
| Otros modelos de Daily | `gpt-5`: capacidad 50 (cuota 50/250). `text-embedding-3-small`: capacidad 120 (cuota de la suscripción 240/1000; el resto lo usan otras cuentas de la suscripción, `prueba7-resource` y `prj-leitner-ia-dev-resource`) |
| Comandos | `az cognitiveservices usage list --location centralus --subscription b6d04525-e7b4-4b17-8492-2471f9779962 --query "[?contains(name.value,'gpt-5') \|\| contains(name.value,'embedding-3-small')].{name:name.value,current:currentValue,limit:limit,unit:unit}" -o table` · `az cognitiveservices account deployment list --name prj-daily-enhanced-dev-resource --resource-group rg-daily-enhanced-dev-centralus --subscription b6d04525-e7b4-4b17-8492-2471f9779962 --query "[].{name:name,model:properties.model.name,version:properties.model.version,sku:sku.name,capacity:sku.capacity,state:properties.provisioningState}" -o table` · `az cognitiveservices account list --subscription … --query "[?location=='centralus'].{name:name,rg:resourceGroup,kind:kind}" -o table` |

Salida sanitizada (sin secretos):

```
OpenAI.GlobalStandard.gpt-5.1                   500.0      250.0    Count
OpenAI.GlobalStandard.gpt-5                     50.0       250.0    Count
OpenAI.GlobalStandard.text-embedding-3-small    240.0      1000.0   Count

gpt-5                   gpt-5                   2025-08-07  GlobalStandard  50          Succeeded
text-embedding-3-small  text-embedding-3-small  1           GlobalStandard  120         Succeeded
gpt-5.1                 gpt-5.1                 2025-11-13  GlobalStandard  500         Succeeded
```

| Bicep que produjo `InsufficientQuota` (LEGACY) | Capacidad que solicitaba |
|---|---|
| `infra/platform/modules/foundry.bicep` (recurso `deployments`) con `infra/platform/main.dev.bicepparam`, en una cuenta **nueva** `ais-daily-dev-<sufijo>` | `gpt-5.1` GlobalStandard: **50** unidades (error del 2026-11-10: *"requires 50 quota units, but only 0 quota units are available"*). También `text-embedding-3-small`: 120 (cabía) |
| Ídem con `main.prod.bicepparam` (no llegó a evaluarse; el what-if se ejecutó con `modelDeployments='[]'`) | `gpt-5.1`: **100** unidades; `text-embedding-3-small`: 120 |

No se ha cambiado la cuota, no se ha creado ni modificado ningún despliegue y no se ha solicitado ninguna ampliación.

## 16. Acciones A1–A13

| Acción | Clasificación |
|---|---|
| A1 Foundry ApiKey → Managed Identity / Entra ID | RECOMMENDED NOW, tarea independiente con migración controlada (13.6); no implementada |
| A2 Reconstruir y desplegar la imagen | RECOMMENDED NOW |
| A3 Retención de imágenes subidas | RECOMMENDED NOW |
| A4 Redirect URI de MSAL | RECOMMENDED NOW, opcional |
| A5 Búsqueda híbrida | RECOMMENDED NOW, sin semántico mientras Search sea Free |
| A6 Clave de consulta para Copilot Studio | RECOMMENDED NOW, si se confirma que hoy usa una clave de administración (verificar antes el tipo) |
| A7 Indexador a demanda y programación | RECOMMENDED NOW (sin el cambio de `AutoPause`) |
| A8 Indexación incremental | FUTURE |
| A9 Cuota de `gpt-5.1` | **CLOSED / LEGACY INFRASTRUCTURE FINDING** (sección 15) |
| A10 Mínimo privilegio de la identidad web | RECOMMENDED NOW, tarea posterior: retirada secuencial de 2 roles (14.6) |
| A11 Red privada | FUTURE – NOT FOR CURRENT DEPLOYMENT |
| A12 `AllowAllWindowsAzureIps` | FUTURE – Future infrastructure hardening |
| A13 Asignaciones administrativas | Hallazgo de gobierno externo; escalar a CONSEIN |

## 17. Vista por área

| Área | CURRENT | RECOMMENDED NOW | FUTURE |
|---|---|---|---|
| Aplicación | Lotes 2–5 en la rama, sin desplegar | A2 y verificación manual | — |
| Identidad web | 4 roles (3 sobre el proyecto y AcrPull) | Retirar Azure AI Developer y OpenAI Contributor (A10) | Rol personalizado sin `listsecrets` |
| Foundry → Search | **ApiKey** (V1) | A1 | `disableLocalAuth` |
| Copilot Studio → Search | Conexión propia de Power Platform con clave temporal | A6 (si es clave de administración) | Entra; VNet de Power Platform |
| Search | Free (`azureairesourceprueba2`) | Sin cambios | Basic para producción |
| SQL | Oferta gratuita; `AllowAllWindowsAzureIps` | Sin cambios | A12, A8 |
| Modelos | `gpt-5.1` con 500 unidades (sobre un límite de 250); A9 CLOSED | No recrear, redimensionar ni sustituir sin revisar la cuota | — |
| Red | Pública con autenticación | — | A11 |
| Observabilidad | **Sin alertas**; Log Analytics con 30 días y sin límite diario | C1 y alerta consolidada C2/C3 (desactivadas en el primer despliegue); C6 pendiente de línea base | C4/C5 con A7; telemetría con Entra |
| Key Vault | No existe | No crear | Solo si aparece un secreto |
| IaC | `infra/main.bicep` (azd) desalineado; `infra/platform` LEGACY | Bicep `internal-minimal` tras cerrar las decisiones | — |

## 18. Clasificación de los documentos

| Documento | Clasificación |
|---|---|
| [lote-7-simplificado.md](lote-7-simplificado.md) | RECOMMENDED NOW: fuente de verdad |
| [acciones-azure.md](acciones-azure.md) | RECOMMENDED NOW, con la clasificación de cada acción |
| [verificacion-manual.md](verificacion-manual.md) | RECOMMENDED NOW |
| [matriz-rbac.md](matriz-rbac.md) | CURRENT (evidencia), con reducciones RECOMMENDED NOW |
| [README.md](README.md) | CURRENT (resumen y cuerpo del PR) |
| [lote-0.md](lote-0.md) … [lote-6.md](lote-6.md) | CURRENT (registro) |
| [red-privada.md](red-privada.md) | FUTURE – NOT FOR CURRENT DEPLOYMENT |
| [lote-7.md](lote-7.md), [whatif/](whatif/README.md), [`infra/platform/`](../../infra/platform/README.md) | LEGACY - DO NOT DEPLOY |
| `infra/README.md`, `infra/main.bicep` | CURRENT, con desalineación conocida (sin editar) |

## 19. Correcciones a entregables anteriores

1. **"Producción necesita Search S1":** solo aplicaba a la red privada. Retirado.
2. **Indexador privado en Basic** (`infra/platform/README.md`): `executionEnvironment` requiere S2 o superior. Retirado (documento LEGACY).
3. **"Sustituir los tres roles de la web por Foundry User":** incorrecto como mínimo privilegio. Foundry User tiene las mismas `dataActions` que Cognitive Services User, que ya está asignado, y más `actions`. Corregido en la sección 14.
4. **Opción B de A10:** la redirect URI la necesitaría la SPA, no la identidad; y la identidad no se asigna por revisión. Corregido en la sección 14.5.
5. **A9 como acción abierta:** era un problema solo del Bicep legacy. Cerrada como problema actual (sección 15).

## 20. Próximo paso: Bicep `internal-minimal` (NO iniciado)

Se diseñará solo cuando decidas sobre A10 y los opcionales. Principios:

- Distinguir **recursos gestionados** por el repositorio (como mínimo la Container App; el resto de lo que hoy creó `azd` queda por decidir, ver la sección 4) de **recursos externos referenciados** (Foundry y sus despliegues, Search, SQL, Storage, observabilidad de Foundry).
- No importar recursos compartidos para que el Bicep "controle todo".
- No declarar despliegues de modelos (sección 15).
- Sin red privada, sin Key Vault, sin recursos nuevos obligatorios.
- Opcionales desactivados por defecto.
- `infra/main.bicep` no se modifica todavía y no se ejecuta ningún what-if.

## 21. Pendientes

No quedan decisiones pendientes para cerrar la parte documental del lote 7. Próximas tareas, **cada una independiente y con autorización propia**:

1. A1 (13.6).
2. A10 (14.6).
3. A6 (verificar primero el tipo de clave de Copilot Studio).
4. Bicep `internal-minimal` (sección 20).
5. Línea base de telemetría para decidir C6.
6. PR en borrador (lo abre el usuario).

## 22. Análisis de C1, C2, C3 y C6 (no activados)

**Estado actual (lectura del 2026-11-11):**

- En `rg-daily-enhanced-dev-centralus` **no hay** grupos de acción, reglas de alerta de métricas, reglas de alerta de logs ni reglas de detección inteligente.
- `law-blx2jbksxc7iu`: SKU PerGB2018, retención de 30 días, límite diario `-1` (sin límite).
- El backend envía telemetría con OpenTelemetry y Azure Monitor (`UseAzureMonitor` cuando existe `APPLICATIONINSIGHTS_CONNECTION_STRING`; [Extensions.cs:71-74](../../backend/WebApp.ServiceDefaults/Extensions.cs#L71-L74)), incluidas las solicitudes HTTP.

| Campo | C1 | C2 | C3 | C6 |
|---|---|---|---|---|
| Nombre (propuesto) | `ag-daily-dev` | `alert-daily-backend-5xx` | `alert-daily-agent-errors` | Límite diario de `law-blx2jbksxc7iu` |
| Tipo | Grupo de acción (`Microsoft.Insights/actionGroups`, ubicación `global`) con un receptor de correo | Alerta de métrica (`Microsoft.Insights/metricAlerts`) sobre `requests/failed`, filtrada por la dimensión código de resultado 500 y 504 | Alerta de métrica sobre `requests/failed`, filtrada por los códigos **502** (fallo de Foundry: 401/403/5xx) y **503** (cuota 429), según [ApiExceptionHandler.cs:64-68](../../backend/WebApp.Api/Infrastructure/ApiExceptionHandler.cs#L64-L68) | Propiedad `workspaceCapping.dailyQuotaGb` del espacio existente |
| Recurso donde se aplica | Grupo de recursos | Application Insights del backend (`appi-blx2jbksxc7iu`, según la convención del IaC de `azd`; se confirmará al diseñar el Bicep) | El mismo | `law-blx2jbksxc7iu` |
| Problema que resuelve | Hoy ninguna alerta notifica a nadie | Detectar caídas o errores del backend antes de que avisen los usuarios | Detectar que Foundry falla o que se agotó la cuota del modelo | Hoy la ingesta no tiene límite: la cadena de conexión del navegador es pública por diseño y un abuso podría generar coste |
| Coste (aprox., verificar en la calculadora) | El grupo no tiene coste; los correos tienen un cupo mensual gratuito | Bajo: del orden de céntimos de USD al mes por serie temporal monitorizada | Igual que C2 | Ninguno; **limita** el coste |
| ¿Crea un recurso de Azure? | **Sí** (1) | **Sí** (1) | **Sí** (1). Se puede fusionar con C2 en una sola regla con varias series (2 → 1) | **No** |
| ¿Modifica uno existente? | No | No | No | **Sí** (`law-blx2jbksxc7iu`) |
| Valor para Daily | Alto, pero solo junto con C2/C3 | Alto | Medio-alto (en especial con A9 y la cuota) | Medio (protección de coste) |
| Impacto si no se implementa | Las alertas solo se verían en el portal | Los fallos se descubren por los usuarios | Fallos del agente sin aviso | Riesgo de factura por ingesta masiva. **Contra:** al alcanzar el límite se deja de ingerir telemetría hasta el día siguiente; el valor debe elegirse con datos reales de ingesta (requiere una lectura de la tabla `Usage`, pendiente de autorización) |

Los cuatro son opcionales y ninguno es necesario para que Daily funcione. C1, C2 y C3 añadirían hasta 3 recursos de alerta (o 2 si se fusionan C2 y C3); C6 solo cambia una propiedad.

### 22.1 Decisiones del 2026-11-11

| # | Decisión | Clasificación |
|---|---|---|
| C1 | Grupo de acción | RECOMMENDED; **no activar todavía** |
| C2/C3 | **Una sola** alerta de métrica sobre `requests/failed` de `appi-blx2jbksxc7iu`, con la dimensión código de resultado filtrada a **500, 502, 503 y 504**. Una condición de alerta de métrica admite varios valores de una dimensión, y opcionalmente dividir por ella para saber qué código la disparó. Así una regla cubre los errores del backend (500, 504) y los de sus dependencias (502 Foundry, 503 cuota). Hay que confirmar los nombres de la métrica y la dimensión al diseñar la regla | RECOMMENDED; parámetro desactivado por defecto |
| C6 | Límite diario de Log Analytics | **OPTIONAL / PENDING TELEMETRY BASELINE**. Antes de fijar un límite hay que medir: ingesta diaria, promedio, picos, retención (hoy 30 días) e impacto de alcanzar el límite. No se arriesga perder telemetría crítica por un límite elegido sin datos |

**El primer despliegue de `internal-minimal` no crea alertas.** Más adelante se podrían activar, con aprobación:

- 1 grupo de acción;
- 1 alerta consolidada de errores críticos;
- alertas del indexador, si aportan valor.

Siempre reutilizando Application Insights y Log Analytics existentes, sin crear más recursos de observabilidad.

## 23. Clasificación final (2026-11-11)

| Categoría | Elementos |
|---|---|
| **CURRENT** | Infraestructura funcional actual (`rg-daily-enhanced-dev-centralus`); Foundry `Daily-Enhanced` v10 → Search mediante **ApiKey** (conexión `azureairesourceprueba2`); Copilot Studio → Search mediante su **conexión independiente** de Power Platform (clave temporal); Search `azureairesourceprueba2` (Free); Container App `ca-web-blx2jbksxc7iu`; SQL `knowledgeserver` / `free-sql-db-knowledge-specialist` |
| **RECOMMENDED NOW** | A1, migración de Foundry → Search hacia identidad administrada (13.6); A10, reducción progresiva de permisos (14.6); C1; alerta consolidada C2/C3; clave de consulta dedicada para Copilot Studio (A6), si se confirma que hoy usa una clave de administración |
| **FUTURE** | Search Basic para la futura producción; red privada (`red-privada.md`); eliminar `AllowAllWindowsAzureIps` (A12); arquitectura privada; hardening avanzado |
| **LEGACY - DO NOT DEPLOY** | What-if anteriores (`whatif/`); Bicep de la plataforma privada anterior (`infra/platform/`); diseño de 45 recursos (`lote-7.md`) |
| **CLOSED** | A9 (`gpt-5.1`) como problema actual: LEGACY INFRASTRUCTURE FINDING |

---

## Anexo B. Nivel de Search frente a las funciones de Daily

| Función | Free | Basic | ¿Requiere S1? | Fuente |
|---|---|---|---|---|
| Indexador Azure SQL | Sí, limitado (1 min en orígenes que no son blob; 3–10 min con skillset) | Sí | No | [Indexer limits](https://learn.microsoft.com/azure/search/search-limits-quotas-capacity#indexer-limits) |
| Indexador con identidad administrada | No documentado (*"Not available on the Free tier"*), aunque funciona | Sí | No | [search-sku-tier](https://learn.microsoft.com/azure/search/search-sku-tier#feature-availability-by-tier) |
| Skillset (SplitSkill y Azure OpenAI Embedding) | Sí, no recomendado para cargas grandes | Sí | No | search-sku-tier |
| Vectorizador con identidad | Solo con clave en Free | Sí | No | [Integrated vectorization](https://learn.microsoft.com/azure/search/search-how-to-integrated-vectorization#prerequisites) |
| Búsqueda vectorial | Sí | Sí | No | [Vector index](https://learn.microsoft.com/azure/search/vector-search-how-to-create-index#prerequisites) |
| Ranking semántico | Solo plan gratuito | Plan gratuito y de pago | No | [Semantic billing](https://learn.microsoft.com/azure/search/semantic-how-to-enable-disable#billing-plans) |
| AI Search tool de Foundry | Sin requisito de nivel documentado | Igual | No | [Foundry AI Search tool](https://learn.microsoft.com/azure/foundry/agents/how-to/tools/ai-search#prerequisites) |
| Copilot Studio | Sin requisito de nivel documentado | Igual | No | [Copilot Studio knowledge](https://learn.microsoft.com/microsoft-copilot-studio/knowledge-azure-ai-search) |
| *(FUTURE)* Indexador privado con skillset y vínculo privado a Foundry | No | No | **Sí** | [Shared private link limits](https://learn.microsoft.com/azure/search/search-limits-quotas-capacity#shared-private-link-resource-limits) |

**Nota sobre A5:** en Free, las consultas semánticas del agente compartirían la asignación gratuita con Copilot Studio; al agotarse fallarían los dos. Por eso A5 usa `vector_simple_hybrid` mientras Search sea Free.

## Anexo C. Key Vault: inventario de secretos

| Posible secreto | Dónde está | ¿Key Vault? |
|---|---|---|
| Web → Foundry | Identidad administrada | No |
| Secreto de cliente de Entra | No existe (SPA pública; la API valida tokens) | No |
| SQL | Solo Entra | No |
| ACR | Pull con identidad; admin deshabilitado | No |
| Cadenas de conexión de Application Insights | Variables y build; *"isn't considered a security token or key"* | No |
| Clave de Search de Copilot Studio | Conexión de Power Platform | No (fuera del IaC) |
| Clave de Search de Foundry | Conexión `azureairesourceprueba2` (V1); desaparece con A1 | No |

**Decisión: no crear Key Vault.**

## Anexo D. RBAC por identidad (solo análisis, sin cambios)

| Identidad | Permisos actuales | Utilizados | Mínimos | Eliminables | Prueba |
|---|---|---|---|---|---|
| Web `id-web-daily-enhanced-dev` (`6b88e44e…`) | Azure AI Developer, Cognitive Services OpenAI Contributor y Cognitive Services User (proyecto); AcrPull | Cognitive Services User (datos) y AcrPull | **Cognitive Services User y AcrPull** | Azure AI Developer y OpenAI Contributor | Sección 14 |
| Proyecto de Foundry (`d2d03254…`) | Search Index Data Reader (Search) | **Ninguno** con el agente v10 (V1) | Por definir en B1 | Ninguno ahora (lo puede necesitar B1) | V1 hecho |
| Cuenta de Foundry (`9b857aa1…`) | Ninguno | Ninguno | Por definir en B1 (Learn indica esta identidad) | — | — |
| Search (`3d150947…`) | OpenAI User (cuenta); Reader (SQL); usuario contenido con `SELECT` | Los tres | Los mismos | Ninguno | — |
| Usuario `e71b…`, principal de servicio `2907…`, UAA en `/` | Ver 13.2 | — | — | — | A13 |
| Copilot Studio | Clave de Search | Consultas | Clave de consulta | La actual, si es de administración | A6 |
