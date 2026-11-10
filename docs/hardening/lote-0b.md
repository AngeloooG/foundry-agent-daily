# Lote 0b – Dependencias vulnerables

## 1. Objetivo

Corregir las vulnerabilidades altas y críticas detectadas en el lote 0 solo con versiones de parche o menores publicadas hace al menos 14 días (antes del 2026-10-27), una dependencia por commit y con los tests en verde después de cada una.

## 2. Archivos modificados y commits

| Commit | Dependencia | Antes → después | Severidad / aviso |
|---|---|---|---|
| `4e4c41a` | `vite` (directa, dev) | 7.3.2 → 7.3.7 | Alta: GHSA-v6wh-96g9-6wx3 y relacionadas |
| `9295f40` | `ws` (transitiva) | 8.19.0 → 8.22.0 | Alta: GHSA-58qx-3vcg-4xpx, GHSA-96hv-2xvq-fx4p |
| `dbbff97` | `postcss` (transitiva) | 8.5.6 → 8.5.32 | Alta: GHSA-qx2v-qp2m-jg93 y relacionadas. Arrastra sus dependencias `nanoid` 3.3.11 → 3.3.20 y `source-map-js` 1.2.1 → 1.2.2, que eran también vulnerables (no se pueden separar). |
| `7357ebc` | `browserslist` (transitiva) | 4.28.1 → 4.29.5 | Alta: GHSA-c83g-rgw3-j3cx, GHSA-73wf-gq98-2v4g |
| `921ea2b` | `brace-expansion` (transitiva) | 5.0.5 → 5.0.12 | Alta: GHSA-jxxr-4gwj-5jf2 y relacionadas |
| `94fcb15` | `zod` (transitiva) | 4.3.6 → 4.6.7 | Alta: GHSA-mrhx-c7xc-m3w5 |
| `6309158` | `@nevware21/ts-utils` (transitiva, runtime vía Application Insights) | 0.13.0 → 0.16.0 | Alta: GHSA-x7j8-49r8-mr43 |
| `720f208` | `System.Security.Cryptography.Pkcs` en WebApp.Api (transitiva) | 10.0.0 → 10.0.14 | Alta: CVE-2026-84008. Corrige también la copia del proyecto de tests. |
| `601375d` | `System.Security.Cryptography.Xml` en tests (directa) | 10.0.6 → 10.0.14 | Alta ×8: CVE-2026-47302 y relacionadas |
| `88b2ae0` | `Microsoft.AspNetCore.DataProtection` en tests (transitiva) | 10.0.0 → 10.0.14 | Crítica: CVE-2026-40372 |

Método npm: `npm install -D vite@7.3.7 --before=2026-10-27` (directa) y `npm update <paquete> --before=2026-10-27` (transitivas), sin `npm audit fix`. NuGet: `PackageReference` explícito a 10.0.14 (publicada el 2026-10-25); la 10.0.15 tiene menos de 14 días.

## 3. Tests añadidos

Ninguno.

## 4. Comandos y resultados exactos

- Tras cada dependencia npm: `npx vitest run` → `176 passed | 6 skipped | 3 todo` (incluye el arnés del lote 1). Tras `vite` y `@nevware21/ts-utils`: `npm run build` OK y `npx tsc -b` 0 errores.
- Tras cada dependencia NuGet: `dotnet build backend/WebApp.sln` 0 errores; `dotnet test` 51/51.
- Resultado final:

| | Antes | Después |
|---|---|---|
| `npm audit` | 25 (2 bajas, 13 moderadas, 10 altas) | 15 (2 bajas, 12 moderadas, 1 alta) |
| `npm audit --omit=dev` | 3 altas | 1 alta (`happy-dom`) |
| `dotnet list package --vulnerable` | 4 paquetes (1 crítico) | 0 |
| `dotnet build` | 13 advertencias NU | 0 advertencias |

Comprobación del bundle: tras `@nevware21/ts-utils` 0.16.0 el bundle de producción es idéntico (mismo hash). Vite resuelve `@nevware21/ts-utils` desde `node_modules` (la versión ESM de Application Insights lo importa), así que las funciones usadas por la app no cambian y la función afectada (`objDeepCopy`) no está en el bundle.

## 5. Fallos preexistentes

Los 5 errores de lint del lote 0 siguen (se corrigen en el lote 4).

## 6. Decisiones

- `happy-dom` (alta, ≤ 21.0.3): ninguna 20.x está corregida y la corrección exige la 21 (versión mayor). **No se actualiza.** Llega por `@fluentui-copilot/*` → `@lexical/headless` y como par opcional de vitest. `@fluentui-copilot` solo lo importan componentes que no son alcanzables desde `main.tsx`. Recomendación: eliminar esos componentes y la dependencia (se evalúa en el lote 4).
- Cadena `eslint 10` / `typescript-eslint` → `minimatch` (moderadas): la única corrección es bajar a eslint 9 (mayor). No se toca; solo dev.
- No se actualiza `@microsoft/applicationinsights-web` (3.4.1 → 3.4.5 disponible): no es necesario para corregir ninguna vulnerabilidad.

## 7. Riesgos pendientes

- El runtime de .NET de la imagen desplegada no cambia hasta reconstruirla ([acciones-azure.md](acciones-azure.md), A2).

## 8. Acciones para el equipo

A2 de [acciones-azure.md](acciones-azure.md).

## 9. Sin modificar a propósito

Saltos de versión mayor (`happy-dom`, `eslint`), dependencias sin vulnerabilidades altas o críticas, y código funcional.
