#!/usr/bin/env bash
# Validates the Base64 transport of the frontend Application Insights connection string
# (deployment/docker/frontend.Dockerfile + deployment/hooks/predeploy.ps1).
#
# - Runs the REAL `npm run build` RUN block extracted from the Dockerfile with a fake `npm`, so the
#   decode/validation logic under test is exactly the one Docker executes (no copy).
# - Uses only a FICTITIOUS connection string. Never prints the sample value or its Base64 form.
# - Needs: bash, base64, sha256sum. PowerShell parity check runs only if pwsh/powershell exists.
#
# Usage: bash deployment/tests/test-frontend-appinsights-b64.sh

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOCKERFILE="$ROOT/deployment/docker/frontend.Dockerfile"
PREDEPLOY="$ROOT/deployment/hooks/predeploy.ps1"
ARG_NAME="APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING_B64"

# Fictitious value: fake GUID, https://example.test/, several ';', '/', '=' and '-'.
SAMPLE='InstrumentationKey=00000000-0000-4000-8000-000000000000;IngestionEndpoint=https://example.test/;LiveEndpoint=https://live.example.test/'
SAMPLE_B64="$(printf '%s' "$SAMPLE" | base64 | tr -d '\r\n')"
SAMPLE_SHA="$(printf '%s' "$SAMPLE" | sha256sum | cut -d' ' -f1)"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

pass=0
fail=0
ok()   { pass=$((pass + 1)); echo "  PASS  $1"; }
bad()  { fail=$((fail + 1)); echo "  FAIL  $1"; }
check() { if eval "$2"; then ok "$1"; else bad "$1"; fi; }

# Output must never contain the sample value or its Base64 form.
no_leak() { ! grep -qF -- "$SAMPLE" "$1" && ! grep -qF -- "$SAMPLE_B64" "$1"; }

# ---------------------------------------------------------------------------
echo "1. Round trip: original -> Base64 -> decode -> original"
decoded="$(printf '%s' "$SAMPLE_B64" | base64 -d)"
check "decoded value is byte-identical to the original" '[ "$decoded" = "$SAMPLE" ]'
check "sample has 3 ';'-separated segments" '[ "$(printf "%s" "$SAMPLE" | awk -F";" "{print NF}")" = "3" ]'

# PowerShell encoder used by predeploy.ps1 (UTF-8 Base64) must produce the same Base64.
PS=""
command -v pwsh >/dev/null 2>&1 && PS="pwsh"
[ -z "$PS" ] && command -v powershell.exe >/dev/null 2>&1 && PS="powershell.exe"
if [ -n "$PS" ]; then
  ps_b64="$("$PS" -NoProfile -NonInteractive -Command "[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('$SAMPLE'))" | tr -d '\r\n')"
  check "PowerShell [Convert]::ToBase64String(UTF8) == base64 (same transport encoding)" '[ "$ps_b64" = "$SAMPLE_B64" ]'
else
  echo "  SKIP  PowerShell not available (encoder parity)"
fi

# ---------------------------------------------------------------------------
echo "2. Dockerfile RUN block (extracted, executed with a fake npm)"
# Extract the RUN that decodes the argument: from 'RUN set -eu;' to the first line without a trailing '\'.
awk '/^RUN set -eu;/{f=1} f{print} f && !/\\$/{exit}' "$DOCKERFILE" > "$WORK/run.txt"
check "found the decode RUN block in the Dockerfile" '[ -s "$WORK/run.txt" ] && grep -q "npm run build" "$WORK/run.txt"'
# Join continuation lines and drop the leading 'RUN ' (Docker runs it with /bin/sh -c).
sed -e 's/\\$//' "$WORK/run.txt" | tr '\n' ' ' | sed -e 's/^RUN //' > "$WORK/run.sh"

# Fake npm: records that it was called and whether Vite would get exactly the sample (by hash only).
mkdir -p "$WORK/bin"
cat > "$WORK/bin/npm" <<'EOF'
#!/bin/sh
[ "$1 $2" = "run build" ] || { echo "fake npm: unexpected args" >&2; exit 90; }
got="$(printf '%s' "${VITE_APPLICATIONINSIGHTS_CONNECTION_STRING:-}" | sha256sum | cut -d' ' -f1)"
if [ "$got" = "$EXPECTED_SHA" ]; then echo match > "$MARKER"; else echo mismatch > "$MARKER"; fi
# The Base64 argument itself must not leak into the build process environment as the decoded value.
env | grep -q '^b64=' && echo "leak" >> "$MARKER"
exit 0
EOF
chmod +x "$WORK/bin/npm"

run_block() { # $1 = value of the Base64 build argument; output in $WORK/out.txt, exit code in $rc
  rm -f "$WORK/marker"
  set +e
  env -i PATH="$WORK/bin:$PATH" EXPECTED_SHA="$SAMPLE_SHA" MARKER="$WORK/marker" \
    "$ARG_NAME=$1" sh -c "$(cat "$WORK/run.sh")" > "$WORK/out.txt" 2>&1
  rc=$?
  set -e
}

run_block "$SAMPLE_B64"
check "valid Base64: build succeeds" '[ "$rc" -eq 0 ]'
check "valid Base64: npm run build receives exactly the original value" '[ "$(head -n1 "$WORK/marker" 2>/dev/null)" = "match" ]'
check "valid Base64: no value in the output" 'no_leak "$WORK/out.txt"'

run_block ""
check "empty Base64: build fails" '[ "$rc" -ne 0 ]'
check "empty Base64: clear message" 'grep -q "is required" "$WORK/out.txt"'
check "empty Base64: npm not called" '[ ! -e "$WORK/marker" ]'

run_block 'not*valid$base64!'
check "invalid characters: build fails without calling npm" '[ "$rc" -ne 0 ] && [ ! -e "$WORK/marker" ] && grep -q "not valid Base64" "$WORK/out.txt"'

run_block 'abc'
check "invalid length: build fails without calling npm" '[ "$rc" -ne 0 ] && [ ! -e "$WORK/marker" ] && grep -q "not valid Base64" "$WORK/out.txt"'

run_block "$(printf '%s' 'just some text' | base64 | tr -d '\r\n')"
check "valid Base64 but not a connection string: build fails without calling npm" '[ "$rc" -ne 0 ] && [ ! -e "$WORK/marker" ] && grep -q "does not decode" "$WORK/out.txt"'
check "error output never contains the sample value" 'no_leak "$WORK/out.txt"'

# ---------------------------------------------------------------------------
echo "3. Dockerfile structure"
first_from="$(grep -n '^FROM ' "$DOCKERFILE" | sed -n 1p | cut -d: -f1)"
second_from="$(grep -n '^FROM ' "$DOCKERFILE" | sed -n 2p | cut -d: -f1)"
last_from="$(grep -n '^FROM ' "$DOCKERFILE" | tail -n1 | cut -d: -f1)"
arg_line="$(grep -n "^ARG $ARG_NAME" "$DOCKERFILE" | cut -d: -f1 || true)"
check "Base64 ARG declared once, inside the frontend-builder stage" '[ -n "$arg_line" ] && [ "$(grep -c "^ARG $ARG_NAME" "$DOCKERFILE")" = "1" ] && [ "$arg_line" -gt "$first_from" ] && [ "$arg_line" -lt "$second_from" ]'
check "no ENV for the connection string (decoded or Base64)" '! grep -qE "^ENV .*(VITE_APPLICATIONINSIGHTS_CONNECTION_STRING|APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING)" "$DOCKERFILE"'
check "the plain (non-Base64) build argument is gone" '! grep -qE "^ARG APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING(=|$)" "$DOCKERFILE"'
check "the final stage does not reference App Insights at all" '! tail -n +"$last_from" "$DOCKERFILE" | grep -q "APPLICATIONINSIGHTS"'
# Only redirections allowed in the block: to stderr (>&2) or /dev/null. Any other target would be a file.
check "the RUN block writes nothing to files (only >&2 and /dev/null redirections)" '! grep -oE ">[[:space:]]*[^&[:space:]][^[:space:];)\"]*" "$WORK/run.sh" | grep -vqE ">[[:space:]]*/dev/null$"'
check "the decoded value is never echoed" '! grep -qE "echo[^;]*\\\$decoded|echo[^;]*\\\$b64" "$WORK/run.sh"'

# ---------------------------------------------------------------------------
echo "4. predeploy.ps1"
check "no semicolon escaping (-replace ';')" '! grep -qF -- "-replace '"'"';'"'"'" "$PREDEPLOY"'
check "encodes as UTF-8 Base64" 'grep -qF "[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(" "$PREDEPLOY"'
check "never passes the plain connection string as a build argument" '! grep -qE "build-arg\"?, *\"APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING=" "$PREDEPLOY"'
check "ACR path uses --secret-build-arg for the Base64 argument" 'grep -qF "\"--secret-build-arg\", \"\$appInsightsArgName=\$appInsightsB64\"" "$PREDEPLOY"'
check "local Docker path uses the same Base64 argument (value from the environment)" 'grep -qF "\"--build-arg\", \$appInsightsArgName" "$PREDEPLOY"'
check "fails when the connection string is missing" 'grep -qF "APPLICATIONINSIGHTS_FRONTEND_CONNECTION_STRING not set" "$PREDEPLOY"'

echo
echo "Result: $pass passed, $fail failed"
[ "$fail" -eq 0 ]
