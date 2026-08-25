#!/usr/bin/env bash
set -euo pipefail
BASE="${BASE:-http://127.0.0.1:8081}"

json() { curl -sS -H 'Content-Type: application/json' -H "X-Actor-Id: ${1}" "${@:2}"; }

echo "== start =="
START=$(json alice -X POST "$BASE/v1/processes/start" \
  -d '{"processKey":"purchase","initiator":"alice","parameters":{"amount":150000000}}')
echo "$START"
DEF=$(echo "$START" | python3 -c 'import json,sys; print(json.load(sys.stdin)["definitionKey"])')
ROOT=$(echo "$START" | python3 -c 'import json,sys; print(json.load(sys.stdin)["instanceId"])')

echo "== refer to bob + cara =="
REF=$(json alice -X POST "$BASE/v1/referrals" -d "$(cat <<EOF
{"definitionKey":"$DEF","parentInstanceId":"$ROOT","title":"تأیید موازی","to":{"kind":"users","ids":["bob","cara"]}}
EOF
)")
echo "$REF"
RID=$(echo "$REF" | python3 -c 'import json,sys; print(json.load(sys.stdin)["instanceId"])')
BOB_TASK=$(echo "$REF" | python3 -c 'import json,sys; d=json.load(sys.stdin);
print(next(t["id"] for t in d["tasks"] if t["assigneeId"]=="bob"))')
CARA_TASK=$(echo "$REF" | python3 -c 'import json,sys; d=json.load(sys.stdin);
print(next(t["id"] for t in d["tasks"] if t["assigneeId"]=="cara"))')

echo "== inbox bob =="
json bob "$BASE/v1/tasks?user=bob"
echo
echo "== completion before =="
json alice "$BASE/v1/instances/$RID/completion"
echo

echo "== bob complete =="
json bob -X POST "$BASE/v1/tasks/$BOB_TASK/complete" -d '{"note":"ok"}'
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
{"definitionKey":"$DEF","parentInstanceId":"$ROOT2","title":"تأیید نهایی","to":{"kind":"users","ids":["bob","cara"]}}
EOF
)")
END_TASK=$(echo "$REF2" | python3 -c 'import json,sys; d=json.load(sys.stdin);
print(next(t["id"] for t in d["tasks"] if t["assigneeId"]=="bob"))')
json bob -X POST "$BASE/v1/tasks/$END_TASK/complete-and-end" -d '{"note":"پرونده بسته شد"}'
echo
json alice "$BASE/v1/users/alice/processes?state=closed"
echo
