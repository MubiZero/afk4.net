#!/usr/bin/env bash
# Deploy or update the AFK4 staging uptime-monitor Cloudflare Worker.
#
# Idempotent: re-running uploads the latest worker.js + bindings + cron
# schedule. Optionally pushes Telegram secrets when the env vars are set.
#
# Required env:
#   CF_API_TOKEN   - Cloudflare API token (Workers Scripts: Edit on the account)
#   CF_ACCOUNT_ID  - Cloudflare account id
#
# Optional env (set both to enable Telegram alerting, omit to keep secrets unchanged):
#   TELEGRAM_BOT_TOKEN
#   TELEGRAM_CHAT_ID
#
# Optional overrides:
#   WORKER_NAME      (default: afk4-staging-uptime-monitor)
#   KV_NAMESPACE     (default: afk4-staging-uptime-monitor-state)
#   CRON_SCHEDULE    (default: */5 * * * *)
#   COMPAT_DATE      (default: 2026-05-24)

set -euo pipefail

export WORKER_NAME="${WORKER_NAME:-afk4-staging-uptime-monitor}"
export KV_NAMESPACE="${KV_NAMESPACE:-afk4-staging-uptime-monitor-state}"
export CRON_SCHEDULE="${CRON_SCHEDULE:-*/5 * * * *}"
export COMPAT_DATE="${COMPAT_DATE:-2026-05-24}"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WORKER_DIR="$REPO_ROOT/deploy/cloudflare/staging-uptime-monitor"
WORKER_JS="$WORKER_DIR/worker.js"

CF_BASE="https://api.cloudflare.com/client/v4"

require_env() {
  local name="$1"
  if [[ -z "${!name:-}" ]]; then
    echo "ERROR: required env var $name is not set" >&2
    exit 1
  fi
}

require_env CF_API_TOKEN
require_env CF_ACCOUNT_ID

if [[ ! -f "$WORKER_JS" ]]; then
  echo "ERROR: worker source not found at $WORKER_JS" >&2
  exit 1
fi

cf_curl() {
  curl -sS -H "Authorization: Bearer $CF_API_TOKEN" "$@"
}

cf_check() {
  python3 -c '
import json, sys
data = json.load(sys.stdin)
if not data.get("success"):
    sys.stderr.write("CF API failure: " + json.dumps(data.get("errors")) + "\n")
    sys.exit(1)
print(json.dumps(data.get("result")))
'
}

echo ">>> Verifying token"
cf_curl "$CF_BASE/accounts/$CF_ACCOUNT_ID/tokens/verify" | cf_check >/dev/null
echo "    OK"

echo ">>> Ensuring KV namespace '$KV_NAMESPACE' exists"
NAMESPACE_ID=$(
  cf_curl "$CF_BASE/accounts/$CF_ACCOUNT_ID/storage/kv/namespaces?per_page=100" |
  python3 -c "
import json, sys, os
title = os.environ['KV_NAMESPACE']
data = json.load(sys.stdin)
for ns in data.get('result', []):
    if ns.get('title') == title:
        print(ns['id'])
        break
" KV_NAMESPACE="$KV_NAMESPACE"
)

if [[ -z "$NAMESPACE_ID" ]]; then
  NAMESPACE_ID=$(
    cf_curl -X POST -H "Content-Type: application/json" \
      -d "{\"title\":\"$KV_NAMESPACE\"}" \
      "$CF_BASE/accounts/$CF_ACCOUNT_ID/storage/kv/namespaces" |
    cf_check |
    python3 -c "import json,sys; print(json.loads(sys.stdin.read())['id'])"
  )
  echo "    created $NAMESPACE_ID"
else
  echo "    found $NAMESPACE_ID"
fi
export NAMESPACE_ID

echo ">>> Uploading Worker script '$WORKER_NAME'"
METADATA=$(python3 -c "
import json, os
print(json.dumps({
    'main_module': 'worker.js',
    'compatibility_date': os.environ['COMPAT_DATE'],
    'bindings': [
        {'type': 'kv_namespace', 'name': 'STATE', 'namespace_id': os.environ['NAMESPACE_ID']}
    ],
}))
")

TMP_METADATA=$(mktemp)
printf '%s' "$METADATA" > "$TMP_METADATA"

cf_curl -X PUT \
  -F "metadata=@$TMP_METADATA;type=application/json" \
  -F "worker.js=@$WORKER_JS;type=application/javascript+module" \
  "$CF_BASE/accounts/$CF_ACCOUNT_ID/workers/scripts/$WORKER_NAME" |
  cf_check >/dev/null

rm -f "$TMP_METADATA"
echo "    OK"

echo ">>> Setting cron schedule '$CRON_SCHEDULE'"
cf_curl -X PUT -H "Content-Type: application/json" \
  -d "[{\"cron\":\"$CRON_SCHEDULE\"}]" \
  "$CF_BASE/accounts/$CF_ACCOUNT_ID/workers/scripts/$WORKER_NAME/schedules" |
  cf_check >/dev/null
echo "    OK"

if [[ -n "${TELEGRAM_BOT_TOKEN:-}" && -n "${TELEGRAM_CHAT_ID:-}" ]]; then
  echo ">>> Pushing TELEGRAM_BOT_TOKEN secret"
  cf_curl -X PUT -H "Content-Type: application/json" \
    -d "$(python3 -c "import json,os; print(json.dumps({'name':'TELEGRAM_BOT_TOKEN','text':os.environ['TELEGRAM_BOT_TOKEN'],'type':'secret_text'}))")" \
    "$CF_BASE/accounts/$CF_ACCOUNT_ID/workers/scripts/$WORKER_NAME/secrets" |
    cf_check >/dev/null
  echo "    OK"

  echo ">>> Pushing TELEGRAM_CHAT_ID secret"
  cf_curl -X PUT -H "Content-Type: application/json" \
    -d "$(python3 -c "import json,os; print(json.dumps({'name':'TELEGRAM_CHAT_ID','text':os.environ['TELEGRAM_CHAT_ID'],'type':'secret_text'}))")" \
    "$CF_BASE/accounts/$CF_ACCOUNT_ID/workers/scripts/$WORKER_NAME/secrets" |
    cf_check >/dev/null
  echo "    OK"
else
  echo ">>> Skipping Telegram secrets (TELEGRAM_BOT_TOKEN / TELEGRAM_CHAT_ID not set)"
  echo "    The worker will probe endpoints but suppress alerts until both"
  echo "    secrets are configured. Re-run this script with the env vars set,"
  echo "    or push them manually via dashboard / API."
fi

echo ""
echo "Deployment complete."
echo "  Worker:      $WORKER_NAME"
echo "  KV:          $KV_NAMESPACE ($NAMESPACE_ID)"
echo "  Cron:        $CRON_SCHEDULE"
echo "  Compat date: $COMPAT_DATE"
