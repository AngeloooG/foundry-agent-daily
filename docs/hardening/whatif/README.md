# LEGACY DESIGN - DO NOT DEPLOY

This what-if belongs to the previous private-network architecture.
It is retained only for historical and architectural reference.
The current target architecture is internal-minimal.

---

**Clasificación:** LEGACY (histórico técnico). No es el plan de despliegue actual. **No volver a ejecutar estos what-if.**

| Archivo | Contenido | Fecha |
|---|---|---|
| `dev.txt` | What-if de `infra/platform` con `main.dev.bicepparam`: 17 creaciones más 5 asignaciones de rol en un grupo de recursos **nuevo**. Era parte del mismo diseño descartado, aunque usaba red pública | 2026-11-10 |
| `prod.txt` | What-if de `infra/platform` con `main.prod.bicepparam` y `enableAgentVnetInjection=false`: 40 creaciones más 5 asignaciones de rol (red privada completa) | 2026-11-10 |

**Por qué se conservan:** son la trazabilidad de por qué se descartó el diseño de 45 recursos.

**Arquitectura vigente:** [`lote-7-simplificado.md`](../lote-7-simplificado.md) (perfil `internal-minimal`: 0 recursos nuevos obligatorios).
