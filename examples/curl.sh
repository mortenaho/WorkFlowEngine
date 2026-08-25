#!/usr/bin/env bash
set -euo pipefail
BASE="${BASE:-http://127.0.0.1:8081}"
# First value of WF_API_KEYS, or WF_API_KEY. Empty in Development if keys are not set.
API_KEY="${WF_API_KEY:-${WF_API_KEYS%%,*}}"

json() {
  local args=(-H 'Content-Type: application/json' -H "X-Actor-Id: ${1}")
  if [[ -n "${API_KEY}" ]]; then
    args+=(-H "X-API-Key: ${API_KEY}")
  fi
  curl -sS "${args[@]}" "${@:2}"
}

echo "== start =="
START=$(json alice -X POST "$BASE/v1/processes/start" \
  -d '{"processKey":"purchase","initiator":"alice","parameters":{"amount":150000000}}')
echo "$START"
DEF=$(echo "$START" | python3 -c 'import json,sys; print(json.load(sys.stdin)["definitionKey"])')
ROOT=$(echo "$START" | python3 -c 'import json,sys; print(json.load(sys.stdin)["instanceId"])')

echo "== refer to mortenaho + cara =="
REF=$(json alice -X POST "$BASE/v1/referrals" -d "$(cat <<EOF
{"definitionKey":"$DEF","parentInstanceId":"$ROOT","title":"تأیید موازی","to":{"kind":"users","ids":["mortenaho","cara"]}}
EOF
)")
echo "$REF"
RID=$(echo "$REF" | python3 -c 'import json,sys; print(json.load(sys.stdin)["instanceId"])')
MORTENAHO_TASK=$(echo "$REF" | python3 -c 'import json,sys; d=json.load(sys.stdin);
print(next(t["id"] for t in d["tasks"] if t["assigneeId"]=="mortenaho"))')
CARA_TASK=$(echo "$REF" | python3 -c 'import json,sys; d=json.load(sys.stdin);
print(next(t["id"] for t in d["tasks"] if t["assigneeId"]=="cara"))')

echo "== inbox mortenaho =="
json mortenaho "$BASE/v1/tasks?user=mortenaho"
echo
echo "== completion before =="
json alice "$BASE/v1/instances/$RID/completion"
echo

echo "== mortenaho complete =="
json mortenaho -X POST "$BASE/v1/tasks/$MORTENAHO_TASK/complete" -d '{"note":"ok"}'
echo
echo "== cara complete =="
json cara -X POST "$BASE/v1/tasks/$CARA_TASK/complete" -d '{"note":"ok"}'
echo
echo "== completion after =="
json alice "$BASE/v1/instances/$RID/completion"
echo

echo "== refer to group legal =="
json alice -X POST "$BASE/v1/referrals" -d "$(cat <<EOF
{"definitionKey":"$DEF","parentInstanceId":"$ROOT","title":"بررسی حقوقی","to":{"kind":"group","id":"legal"}}
EOF
)"
echo
echo "== inbox group legal =="
json alice "$BASE/v1/tasks?group=legal"
echo

echo "== alice processes (open) =="
json alice "$BASE/v1/users/alice/processes?state=open"
echo

echo "== new process: complete-and-end =="
START2=$(json alice -X POST "$BASE/v1/processes/start" \
  -d '{"processKey":"purchase","initiator":"alice"}')
ROOT2=$(echo "$START2" | python3 -c 'import json,sys; print(json.load(sys.stdin)["instanceId"])')
REF2=$(json alice -X POST "$BASE/v1/referrals" -d "$(cat <<EOF
{"definitionKey":"$DEF","parentInstanceId":"$ROOT2","title":"تأیید نهایی","to":{"kind":"users","ids":["mortenaho","cara"]}}
EOF
)")
END_TASK=$(echo "$REF2" | python3 -c 'import json,sys; d=json.load(sys.stdin);
print(next(t["id"] for t in d["tasks"] if t["assigneeId"]=="mortenaho"))')
json mortenaho -X POST "$BASE/v1/tasks/$END_TASK/complete-and-end" -d '{"note":"پرونده بسته شد"}'
echo
json alice "$BASE/v1/users/alice/processes?state=closed"
echo
