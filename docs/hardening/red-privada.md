# Diseño de red privada de Daily

> **NOT FOR CURRENT DEPLOYMENT.**
> **Clasificación: FUTURE – ROADMAP / ADVANCED HARDENING.** Este documento no forma parte de la arquitectura vigente (`internal-minimal`, ver [lote-7-simplificado.md](lote-7-simplificado.md)) y **no es requisito previo de la producción interna**. Se conserva como diseño de referencia por si en el futuro se exige aislamiento de red. Algunas afirmaciones sobre niveles de Search que aparecen aquí se corrigen en el anexo H de lote-7-simplificado.md.

**Requiere revisión senior** (red, identidad y RBAC). Nada de esto se ha desplegado. El Bicep correspondiente está en [`infra/platform/`](../../infra/platform/) (`enablePrivateNetworking = true` en `main.prod.bicepparam`).

Fuentes revisadas en Microsoft Learn (2026-11-10):

- [Make outbound connections through a shared private link](https://learn.microsoft.com/azure/search/search-indexer-howto-access-private)
- [Service limits in Azure AI Search, *shared private link resource limits*](https://learn.microsoft.com/azure/search/search-limits-quotas-capacity#shared-private-link-resource-limits)
- [Configure network isolation for Microsoft Foundry, *agent tools with network isolation*](https://learn.microsoft.com/azure/foundry/how-to/configure-private-link)
- [Networking options for Foundry Agent Service](https://learn.microsoft.com/azure/foundry/agents/concepts/networking-options)
- [Add Azure AI Search as a knowledge source (Copilot Studio), *Virtual Network support*](https://learn.microsoft.com/microsoft-copilot-studio/knowledge-azure-ai-search)
- [Virtual Network support overview (Power Platform)](https://learn.microsoft.com/power-platform/admin/vnet-support-overview)
- [Set up Virtual Network support for Power Platform](https://learn.microsoft.com/power-platform/admin/vnet-support-setup-configure)

## 1. Estado actual (verificado con lecturas)

| Recurso | Red hoy | Consecuencia para la migración |
|---|---|---|
| Container Apps `cae-blx2jbksxc7iu` | Sin VNet (`vnetConfiguration: null`), entorno solo de consumo. Ingress externo, sin restricciones de IP. | Un entorno **no admite añadir VNet después**: hay que crear un entorno nuevo con perfiles de carga de trabajo en una subred delegada. |
| Foundry `prj-daily-enhanced-dev-resource` | Acceso público, `defaultAction: Allow`, claves habilitadas, **sin inyección de VNet del Agent Service**. | La inyección solo se puede definir **al crear la cuenta**. Para que el agente llegue a un Search privado hace falta una cuenta nueva. |
| Search `azureairesourceprueba2` | Free, público. | Free no admite private endpoints ni vínculos privados compartidos: hace falta un servicio nuevo, S1 o superior (sección 3.4). |
| SQL `knowledgeserver` | Público, Entra-only, TLS 1.2. Reglas: `AllowAllWindowsAzureIps` y 5 IP de cliente. | `AllowAllWindowsAzureIps` admite tráfico de **cualquier** suscripción de Azure. Se sustituye por un private endpoint más un vínculo privado compartido desde Search. |
| ACR `crblx2jbksxc7iu` | Basic, público, sin admin, sin pull anónimo. | Basic no admite private endpoints (requiere Premium). La extracción con identidad administrada es aceptable mientras tanto. |

## 2. Topología objetivo

```
                          Internet (usuarios CONSEIN, Entra ID)
                                   │ HTTPS
                     ┌─────────────▼──────────────┐
                     │ Container App (ingress     │  snet-aca 10.40.0.0/23
                     │ externo) SPA + API         │  delegada a Microsoft.App/environments
                     └─────────────┬──────────────┘
 VNet vnet-daily-prod 10.40.0.0/16 │ (centralus)
  ┌────────────────────────────────┼─────────────────────────────────────────┐
  │ snet-pe 10.40.2.0/24           ▼                                         │
  │   pe-foundry (account) ◄── Container App (identidad administrada)        │
  │   pe-search            ◄── Agent Service (snet-agent 10.40.3.0/24,       │
  │   pe-sql                    inyección de VNet; capability host)          │
  │   pe-keyvault                                                            │
  │ Zonas DNS privadas vinculadas: privatelink.search.windows.net,           │
  │   privatelink.database.windows.net, privatelink.cognitiveservices.azure.com,│
  │   privatelink.openai.azure.com, privatelink.services.ai.azure.com,       │
  │   privatelink.vaultcore.azure.net                                        │
  └──────────────────────────────────────────────────────────────────────────┘
  Search ──vínculo privado compartido (sqlServer)──────► SQL
  Search ──vínculo privado compartido (openai_account)─► Foundry (embeddings y vectorizador)

  Fase final (opcional): VNets de Power Platform en eastus + westus
  (subred delegada a Microsoft.PowerPlatform/enterprisePolicies) ── emparejamiento ──► vnet-daily-prod
  Copilot Studio ──► pe-search
```

## 3. Flujos

| # | Origen → destino | Mecanismo | DNS | Autenticación | En Bicep |
|---|---|---|---|---|---|
| 1 | Container Apps → Foundry | Private endpoint `account` de Foundry; el entorno de Container Apps en `snet-aca` | `*.services.ai.azure.com`, `*.openai.azure.com` y `*.cognitiveservices.azure.com` resuelven a la IP privada | Identidad administrada de la web app, **Foundry User** en el proyecto | PE y zonas: sí. El entorno de Container Apps pertenece al IaC de la web app (`infra/main*.bicep`): pendiente, ver 5.F2 |
| 2 | Foundry (agente) → Search | Inyección de VNet del Agent Service (`networkInjections`, `snet-agent`) más el capability host del proyecto más el PE de Search | `*.search.windows.net` → privada | Identidad del proyecto, **Search Index Data Reader**, conexión `search-daily` con `authType: AAD` | Sí (`enableAgentVnetInjection`) |
| 3 | Search → SQL (indexador) | Vínculo privado compartido `sqlServer`; aprobación en SQL; indexador con `executionEnvironment: "private"` | Lo gestiona Search | Identidad del sistema de Search: usuario contenido con `SELECT` en la vista y Reader en el servidor | Vínculo: sí. Aprobación e indexador: manual (plano de datos) |
| 4 | Search → Foundry (embeddings y vectorizador) | Vínculo privado compartido `openai_account`; aprobación en Foundry; `bypass: AzureServices` se conserva como respaldo | Lo gestiona Search | Identidad del sistema de Search, **Cognitive Services OpenAI User** en la cuenta | Vínculo: sí. Aprobación: manual |
| 5 | Copilot Studio → Search | Soporte de VNet de Power Platform (subredes delegadas en eastus y westus, emparejadas con la VNet de centralus) | Zona `privatelink.search.windows.net` vinculada también a las VNets de Power Platform | Clave de consulta dedicada (A6) o, más adelante, Entra | **No**. Search queda público con `keepSearchPublic = true` hasta validar este flujo |

### 3.1 Container Apps → Foundry

- El SDK usa el endpoint del proyecto (`https://<cuenta>.services.ai.azure.com/api/projects/<proyecto>`). Con el PE y las tres zonas vinculadas, el nombre resuelve a `10.40.2.x` desde el entorno.
- La web app no necesita salida a Internet para Foundry. Sí la necesita para Entra ID (validación de tokens y metadatos OIDC) y para Application Insights (ingesta pública con Entra; ver 3.6).

### 3.2 Foundry → Search (herramienta `azure_ai_search` del agente)

- Según Learn, la herramienta Azure AI Search usa la subred de la VNet del Agent Service. Para ello necesita:
  - una subred dedicada delegada a `Microsoft.App/environments`, de /27 o más, cuyo tamaño no se puede cambiar después;
  - la configuración `networkInjections` **en la creación de la cuenta**;
  - un capability host **a nivel de proyecto** (sin él falla con `name or host unknown`).
- Las conversaciones y los archivos del agente (configuración básica, recursos gestionados por la plataforma) no necesitan Cosmos DB, Storage ni Search propios. Si en el futuro se exige que esos datos estén en el inquilino, se pasa a la configuración estándar (plantilla `15-private-network-standard-agent-setup`).
- **Limitación verificada:** `az deployment sub what-if` devuelve `InternalServerError` cuando la cuenta declara `networkInjections` (probado con las API `2025-06-01` y `2025-04-01-preview`). El what-if guardado usa `enableAgentVnetInjection=false`. La plantilla compila y sigue la de ejemplo oficial (`11-private-network-basic-vnet`). Hay que validarla con un despliegue real en una suscripción de pruebas.

### 3.3 Search → SQL y Search → Foundry (vínculos privados compartidos)

1. Search crea las solicitudes de private endpoint; quedan en `Pending`.
2. El equipo las aprueba en SQL (*Networking → Private access*) y en Foundry (*Networking → Private endpoint connections*).
3. El indexador se actualiza con `"parameters": { "configuration": { "executionEnvironment": "private" } }`. Si no se hace, el indexador se ejecuta en el entorno multiinquilino, que no atraviesa private endpoints, y **falla sin avisar dejando el índice vacío**.
4. El grupo `openai_account` es el documentado para que el skill de embeddings y el vectorizador lleguen al endpoint de Azure OpenAI de una cuenta de Foundry. Según Learn, `az search shared-private-link-resource create` puede rechazarlo; ARM y REST con `2025-05-01` lo aceptan (el Bicep usa ARM).
5. Learn advierte que, en algunos escenarios, la llamada de embeddings en ingesta todavía depende de la excepción de servicios de confianza. Por eso la cuenta de Foundry conserva `networkAcls.bypass = AzureServices`. Si la ingesta devuelve `403 Public access is disabled`, ese es el primer punto a revisar.

### 3.4 Nivel de Search

| Nivel | Indexador privado con skillset | Vínculo privado compartido a un recurso de Foundry | Conclusión |
|---|---|---|---|
| Free | No | No | Sirve solo para desarrollo actual |
| Basic | **No** | **No** | Válido para desarrollo con red pública (`main.dev.bicepparam`) |
| S1 (creado después del 2024-04-03) | Sí | Sí | **Mínimo para producción privada** (`main.prod.bicepparam`: `standard`, 2 réplicas) |

Con Basic en desarrollo, el indexador llega a SQL por red pública. Hay que añadir la IP de salida de Search como regla explícita en `sqlFirewallRules`, **nunca** `0.0.0.0`/`AllowAllWindowsAzureIps` (ver [`infra/platform/README.md`](../../infra/platform/README.md)).

### 3.5 Copilot Studio → Search (limitaciones, no se asume que funciona)

Learn confirma que Copilot Studio admite índices de Search con private endpoint, **solo** si el entorno de Power Platform usa el soporte de VNet:

- El entorno debe ser un **entorno administrado** (Managed Environment), lo que implica licencias premium para los usuarios del entorno. No se admiten entornos Trial ni de Dataverse for Teams.
- Para la geografía *United States*, las VNets **tienen que estar en eastus y westus**, una en cada región, con subredes del mismo tamaño delegadas a `Microsoft.PowerPlatform/enterprisePolicies`. **No pueden estar en centralus**, donde está Daily, así que hace falta emparejamiento con `vnet-daily-prod` y vincular la zona `privatelink.search.windows.net` a esas VNets.
- Tamaño: 25–30 IP por entorno de producción y 6–10 para los que no son de producción, más 5 reservadas. Una vez delegado, no se puede cambiar el rango sin desvincular.
- Activar o desactivar la delegación provoca **hasta 30 minutos de interrupción** en el entorno.
- **Afecta a todo el entorno**: todos los conectores con soporte de VNet de ese entorno (también los flujos de Power Automate, incluido `FLW_ActualizarConocimientoDeEspecialistaAzureSQL` si está ahí) pasan a salir por la subred delegada. Las llamadas a recursos públicos siguen funcionando mientras no se restrinja la salida (NAT o firewall). Las de SQL resolverán al private endpoint, así que hay que comprobar el DNS.
- Hace falta asociar una suscripción de Azure al inquilino de Power Platform, más los roles Network Contributor (Azure) y Power Platform Administrator (Entra).
- **Sin verificar** (requiere un administrador de Power Platform): la región del entorno de Copilot Studio (`Get-EnvironmentRegion` del módulo de diagnóstico) y si ya es un entorno administrado.

Mientras no se complete y se pruebe, **Search mantiene su endpoint público** (`keepSearchPublic = true`). Copilot Studio usa la clave de consulta dedicada (A6) y el resto de consumidores (web app y agente) va por private endpoint.

### 3.6 Application Insights

- La ingesta del **backend** puede autenticarse con Entra (Monitoring Metrics Publisher, ya incluido en `rbac.bicep`) y seguir siendo pública. Azure Monitor Private Link Scope (AMPLS) queda fuera del alcance porque afecta a toda la resolución DNS de Azure Monitor de la VNet.
- La telemetría del **navegador** (`appi-fe-*`, SDK JavaScript) no puede usar Entra: necesita la cadena de conexión con autenticación local y una ingesta pública. Por eso `disableLocalAuth` solo puede aplicarse al componente del backend, nunca al del frontend (ver [`matriz-rbac.md`](matriz-rbac.md)).

## 4. Costes aproximados (centralus, precios de lista; verificar en la calculadora antes de aprobar)

| Elemento | Coste aproximado |
|---|---|
| 4 private endpoints (Search, SQL, Foundry, Key Vault) | ~7–8 USD/mes cada uno, más tráfico procesado |
| 6 zonas DNS privadas | ~0,50 USD/mes por zona, más consultas |
| Vínculos privados compartidos de Search | Facturados por uso de Private Link |
| Search S1 × 2 réplicas | ~2 unidades de búsqueda S1 (del orden de cientos de USD/mes) frente a Free hoy |
| SQL GP_Gen5_2 aprovisionada | Del orden de cientos de USD/mes frente a la oferta gratuita actual |
| ACR Premium (si se exige PE para el registro) | Incremento frente a Basic |
| Power Platform: entorno administrado | Licencias premium por usuario del entorno |

La red privada es para producción. En desarrollo, `enablePrivateNetworking = false`.

## 5. Estrategia de migración

La migración es **en paralelo**: se crea la plataforma nueva sin tocar `rg-daily-enhanced-dev-centralus`, y el cambio se hace por la configuración de la web app y de Copilot Studio.

| Fase | Pasos | Criterio para avanzar | Rollback |
|---|---|---|---|
| F0. Requisitos | A1 (Foundry→Search con Entra), A6 (clave de consulta dedicada), A9 (cuota de modelos), inventario de consumidores de claves | El agente responde con la conexión Entra en desarrollo | Versión anterior del agente |
| F1. Plataforma | Desplegar `infra/platform` (prod) con `keepSearchPublic = true`. Aprobar los vínculos privados. Exportar e importar las definiciones del índice, el skillset, el origen de datos (`ResourceId` del SQL nuevo) y el indexador (`executionEnvironment: private`). Migrar los datos de SQL. Crear el agente en la cuenta nueva con la conexión `search-daily` | Indexador con éxito y mismo número de documentos que el índice actual. El agente responde en el playground (desde la VNet) | Borrar el grupo de recursos nuevo; nada del actual cambió |
| F2. Web app | Entorno de Container Apps nuevo en `snet-aca` (perfiles de carga de trabajo). Desplegar la web app con la identidad `id-web-daily-prod` y el endpoint del proyecto nuevo. Redirect URIs de Entra para la URL nueva | Verificación manual completa ([verificacion-manual.md](verificacion-manual.md)) en la URL nueva | Seguir usando la Container App actual |
| F3. Cambio | Comunicar a los usuarios: **se pierde el historial** (lote 2) y cambia la URL. Apuntar Copilot Studio al Search nuevo con la clave de consulta nueva | 1 semana sin incidencias | Volver a la URL y al Search anteriores (siguen vivos) |
| F4. Copilot Studio privado | Entorno administrado, VNets en eastus y westus, política empresarial, emparejamiento y vínculo DNS. Probar en el panel de prueba. Después `keepSearchPublic = false` | Copilot Studio responde con Search sin acceso público | `keepSearchPublic = true` (redespliegue) |
| F5. Sin claves | `disableLocalAuth = true` solo cuando el inventario demuestre que nadie usa claves (Copilot Studio en Entra) | Inventario firmado por el responsable | `disableLocalAuth = false` |
| F6. Retirada | Retirar los recursos antiguos tras el periodo acordado | — | No aplica |

## 6. Pruebas de DNS y conectividad

Todas son de lectura o de diagnóstico; las ejecuta el equipo con sus permisos.

| Prueba | Desde | Resultado esperado |
|---|---|---|
| `nslookup <cuenta>.services.ai.azure.com` | Consola de la Container App (`az containerapp exec ... --command sh`) o una VM de diagnóstico en la VNet | CNAME `*.privatelink.services.ai.azure.com` → `10.40.2.x` |
| `nslookup <search>.search.windows.net`, `<sql>.database.windows.net`, `<kv>.vault.azure.net` | Igual | IP de `snet-pe` |
| Las mismas consultas desde fuera de la VNet | Portátil | IP pública; con el acceso público deshabilitado, las llamadas devuelven 403 |
| `curl -s -o /dev/null -w "%{http_code}" https://<search>.search.windows.net/indexes?api-version=2024-07-01` | Dentro de la VNet | `401`/`403` (llega y pide credenciales). Un *timeout* indica un fallo de red o DNS |
| `az search shared-private-link-resource list --service-name <search> --resource-group <rg> --subscription <sub> --query "[].{name:name,status:properties.status}"` | Cualquiera | `Approved` en los dos vínculos |
| Estado del indexador (`az rest --method get --resource https://search.azure.com --url "https://<search>.search.windows.net/indexers/<indexador>/status?api-version=2024-07-01"`) | Dentro de la VNet (o con Search público) | `success` y el mismo número de documentos que el índice actual |
| Pregunta de prueba en el playground del agente | Navegador conectado a la VNet (VPN o Bastion): con el acceso público deshabilitado, **el portal de Foundry no funciona desde fuera** | Respuesta con cita del documento |
| Pregunta desde Daily (verificación manual) | Navegador del usuario | Respuesta con cita; sin errores en la consola de CSP |
| Panel de prueba de Copilot Studio (F4) | Copilot Studio | Respuesta con resultados del índice |
| Herramienta de diagnóstico de subred de Power Platform (`Test-DnsResolution`, `Test-NetworkConnectivity` del módulo de diagnóstico) | Entorno de Power Platform | Resuelve y conecta con `<search>.search.windows.net` en el puerto 443 por IP privada |
