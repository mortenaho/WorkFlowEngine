# راهنمای استفاده

چهار سرویس روی یک کرنل:

1. شروع فرایند
2. ارجاع به شخص / گروه / چند نفر
3. کارتابل تسک‌های باز
4. وضعیت تکمیل همهٔ گیرندگان یک درخواست

```bash
go run ./cmd/server
# Swagger: http://127.0.0.1:8081/swagger
```

| کار | درخواست |
|-----|---------|
| شروع | `POST /v1/processes/start` با `{ "processKey", "initiator", "parameters?" }` |
| ارجاع | `POST /v1/referrals` با `{ "definitionKey", "parentInstanceId?", "to" }` |
| کارتابل | `GET /v1/tasks?user=bob` یا `?group=legal` |
| کلیم | `POST /v1/tasks/{id}/claim` |
| آزاد کردن | `POST /v1/tasks/{id}/unclaim` |
| وضعیت چندنفره | `GET /v1/instances/{id}/completion` |
| تکمیل تسک | `POST /v1/tasks/{id}/complete` |

دو راه مصرف: SDK در `pkg/workflow` و REST. نمونه curl: [`examples/curl.sh`](../examples/curl.sh).

---

## ۱. مفاهیم

| واژه | معنی |
|------|------|
| processKey | نوع فرایند (مثلاً `purchase`) |
| definitionKey | کلید تعریف همان فرایند؛ در خروجی start برمی‌گردد |
| instanceId | یک اجرای زنده. start یک اینستنس می‌سازد؛ هر ارجاع اینستنس **جدید** می‌سازد |
| Task | آیتم کارتابل برای شخص یا گروه |
| Inbox | تسک‌های `open` دست یک شخص (به‌علاوه گروه‌هایش) یا یک گروه |

بازیگر REST هدر `X-Actor-Id` است. انجین کاربر ذخیره نمی‌کند؛ گروه‌ها را با `Directory` می‌دهید.

---

## ۲. SDK

```go
dir := workflow.NewStaticDirectory(
    []string{"alice", "bob", "cara", "dan"},
    map[string][]string{
        "legal":   {"bob", "cara"},
        "finance": {"dan", "cara"},
    },
)
eng := workflow.NewEngine(workflow.NewMemoryStore(), dir)
ctx := context.Background()

started, err := eng.Start(ctx, "purchase", "alice", workflow.Vars{"amount": 1.5e8})
// started.DefinitionKey, started.InstanceID

ref, err := eng.Refer(ctx, "alice", workflow.ReferInput{
    DefinitionKey:    started.DefinitionKey,
    ParentInstanceID: started.InstanceID,
    Title:            "بررسی حقوقی",
    ToKind:           workflow.AssigneeUser, // یا AssigneeGroup / AssigneeUsers
    ToID:             "bob",
    // ToIDs: []string{"bob", "cara"}, // برای AssigneeUsers
})
// ref.InstanceID, ref.Task / ref.Tasks

inbox, _ := eng.PendingTasks(ctx, "bob", "")
groupInbox, _ := eng.PendingTasks(ctx, "", "legal")

done, _ := eng.CompleteTask(ctx, ref.Task.ID, "bob", "تأیید شد", nil)
_ = done.Completion.AllCompleted

comp, _ := eng.Completion(ctx, ref.InstanceID)
```

در پروداکشن `Directory` را روی LDAP / سرویس هویت خود پیاده کنید:

```go
type Directory interface {
    UserExists(ctx context.Context, userID string) (bool, error)
    GroupMembers(ctx context.Context, groupID string) ([]string, error)
    UserGroups(ctx context.Context, userID string) ([]string, error)
}
```

---

## ۳. REST

پایه: `http://localhost:8081`. هویت: `X-Actor-Id`. اختیاری: `X-Tenant-Id`، `X-API-Key` (اگر `WF_API_KEYS` ست باشد).

### شروع

```json
POST /v1/processes/start
{ "processKey": "purchase", "initiator": "alice", "parameters": { "amount": 150000000 } }

→ { "definitionKey": "purchase", "instanceId": "..." }
```

اگر `initiator` خالی باشد از `X-Actor-Id` استفاده می‌شود. اگر تعریف برای `processKey` نباشد، ساخته می‌شود.

### ارجاع

```json
POST /v1/referrals
{
  "definitionKey": "purchase",
  "parentInstanceId": "<instance از start>",
  "from": "alice",
  "title": "بررسی",
  "to": { "kind": "user", "id": "bob" }
}

→ { "instanceId": "...", "definitionKey": "purchase", "task": { ... }, "tasks": [ ... ] }
```

اگر `from` خالی باشد از `X-Actor-Id` استفاده می‌شود.

| `to.kind` | فیلد | نتیجه |
|-----------|------|--------|
| `user` | `id` | یک تسک برای آن شخص |
| `group` | `id` | یک تسک برای گروه؛ همهٔ اعضا در کارتابل می‌بینند |
| `users` | `ids` | یک تسک برای هر نفر؛ با همان `instanceId` وضعیت همه را می‌گیرید |

### کارتابل

- `GET /v1/tasks?user=bob` — تسک شخصی + تسک گروه‌هایی که bob عضو آن‌هاست
- `GET /v1/tasks?group=legal` — فقط تسک‌های همان گروه

هر تسک `assigneeKind` و `assigneeId` دارد تا معلوم باشد دست کیست.

### کلیم

تسک گروهی در کارتابل همهٔ اعضا `open` است. کسی که کار را برمی‌دارد:

```bash
POST /v1/tasks/{id}/claim
{ "from": "bob" }
```

وضعیت `claimed` و `claimedBy=bob` می‌شود. نفر دوم `409` می‌گیرد. کارتابل بقیه خالی می‌شود. فقط bob می‌تواند complete کند.

`POST /v1/tasks/{id}/unclaim` تسک را دوباره `open` می‌کند.

تسک شخصی بدون کلیم هم complete می‌شود؛ کلیم اختیاری است.

### تکمیل چندنفره

`GET /v1/instances/{referralInstanceId}/completion`:

```json
{
  "instanceId": "...",
  "allCompleted": false,
  "total": 3,
  "completed": 1,
  "open": 2,
  "tasks": [ ... ]
}
```

`POST /v1/tasks/{id}/complete` هم فیلد `completion` را برمی‌گرداند.

---

## ۴. Docker

```bash
docker compose up --build
curl -s http://localhost:8081/health
```

| متغیر | معنی |
|--------|------|
| `DATABASE_URL` | اتصال Postgres؛ خالی = حافظه |
| `ADDR` | پیش‌فرض `:8081` |
| `WF_USERS` / `WF_GROUP_LEGAL` / `WF_GROUP_FINANCE` | دایرکتوری استاتیک |
| `WF_API_KEYS` | اگر ست شود همهٔ مسیرها جز `/health` کلید می‌خواهند |

بدون `DATABASE_URL` داده با خاموش شدن سرور از بین می‌رود. Compose به Postgres وصل است.
