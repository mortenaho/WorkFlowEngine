<div dir="rtl">

# راهنمای استفاده

سرویس‌ها روی یک کرنل:

1. شروع فرایند
2. لیست اجراهای یک processKey
3. ارجاع به شخص / گروه / چند نفر
4. کارتابل تسک‌های باز
5. وضعیت تکمیل همهٔ گیرندگان یک درخواست

<div dir="ltr">

```bash
dotnet run --project src/WorkflowEngine.Server
# Swagger: http://127.0.0.1:8081/swagger
```

</div>

| کار | درخواست |
|-----|---------|
| شروع | `POST /v1/processes/start` با `{ "processKey", "initiator", "parameters?" }` |
| لیست اجراها | `GET /v1/processes/{processKey}/instances` |
| ارجاع | `POST /v1/referrals` با `{ "definitionKey", "parentInstanceId?", "to" }` |
| کارتابل | `GET /v1/tasks?user=bob` یا `?group=legal` |
| کلیم | `POST /v1/tasks/{id}/claim` |
| آزاد کردن | `POST /v1/tasks/{id}/unclaim` |
| وضعیت چندنفره | `GET /v1/instances/{id}/completion` |
| تکمیل تسک | `POST /v1/tasks/{id}/complete` |
| تکمیل و پایان فرایند | `POST /v1/tasks/{id}/complete-and-end` |
| فرایندهای کاربر | `GET /v1/users/{user}/processes` با `state=open` یا `closed` یا `notStarted` |

دو راه مصرف: کتابخانه (`Application` + `Infrastructure`) و REST. نمونه curl: [`examples/curl.sh`](../examples/curl.sh).

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

<div dir="ltr">

```csharp
using WorkflowEngine.Application;
using WorkflowEngine.Domain;
using WorkflowEngine.Infrastructure;

var dir = new StaticDirectory(
    ["alice", "bob", "cara", "dan"],
    new Dictionary<string, IReadOnlyList<string>>
    {
        ["legal"] = ["bob", "cara"],
        ["finance"] = ["dan", "cara"],
    });
var eng = new Engine(new MemoryStore(), dir);

var started = await eng.Start("purchase", "alice", new Dictionary<string, object?> { ["amount"] = 1.5e8 });
// started.DefinitionKey, started.InstanceId

var refer = await eng.Refer("alice", new ReferInput
{
    DefinitionKey = started.DefinitionKey,
    ParentInstanceId = started.InstanceId,
    Title = "بررسی حقوقی",
    ToKind = AssigneeKind.User, // یا AssigneeKind.Group / AssigneeKind.Users
    ToId = "bob",
    // ToIds = ["bob", "cara"], // برای AssigneeKind.Users
});
// refer.InstanceId, refer.Task / refer.Tasks

var inbox = await eng.PendingTasks("bob", "");
var groupInbox = await eng.PendingTasks("", "legal");

var ended = await eng.CompleteAndEnd(refer.Task!.Id, "bob", "پرونده بسته شد");
_ = ended.Process.Status; // completed

var mine = await eng.ListUserProcesses("alice");
var open = await eng.ListUserProcesses("alice", "open");
```

</div>

در پروداکشن `IDirectory` را در Infrastructure روی LDAP / سرویس هویت خود پیاده کنید:

<div dir="ltr">

```csharp
public interface IDirectory
{
    Task<bool> UserExists(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GroupMembers(string groupId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> UserGroups(string userId, CancellationToken cancellationToken = default);
}
```

</div>

---

## ۳. REST

پایه: `http://localhost:8081`. هویت: `X-Actor-Id`. اختیاری: `X-Tenant-Id` (سازمان)، `X-API-Key` (اگر `WF_API_KEYS` ست باشد).

### شروع

<div dir="ltr">

```json
POST /v1/processes/start
{ "processKey": "purchase", "initiator": "alice", "parameters": { "amount": 150000000 } }

→ { "definitionKey": "purchase", "instanceId": "..." }
```

</div>

اگر `initiator` خالی باشد از `X-Actor-Id` استفاده می‌شود. اگر تعریف برای `processKey` نباشد، ساخته می‌شود.

یک `processKey` بارها استارت می‌شود. مثلاً `employeeTermination` برای هر کارمند یک اینستنس جدا:

<div dir="ltr">

```bash
GET /v1/processes/employeeTermination/instances
```

</div>

<div dir="ltr">

```json
{
  "processKey": "employeeTermination",
  "total": 2,
  "instances": [
    {
      "instanceId": "...",
      "initiator": "hr",
      "status": "running",
      "parameters": { "employeeId": "1002" },
      "tasks": [ ... ],
      "taskTotal": 1,
      "tasksOpen": 1,
      "allTasksCompleted": false
    }
  ]
}
```

</div>

فقط اجراهای start هستند؛ ارجاع‌های فرزند در `tasks` همان اجرا می‌آیند.

### ارجاع

<div dir="ltr">

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

</div>

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

<div dir="ltr">

```bash
POST /v1/tasks/{id}/claim
{ "from": "bob" }
```

</div>

وضعیت `claimed` و `claimedBy=bob` می‌شود. نفر دوم `409` می‌گیرد. کارتابل بقیه خالی می‌شود. فقط bob می‌تواند complete کند.

`POST /v1/tasks/{id}/unclaim` تسک را دوباره `open` می‌کند.

تسک شخصی بدون کلیم هم complete می‌شود؛ کلیم اختیاری است.

### تکمیل چندنفره

`GET /v1/instances/{referralInstanceId}/completion`:

<div dir="ltr">

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

</div>

`POST /v1/tasks/{id}/complete` هم فیلد `completion` را برمی‌گرداند.

تکمیل معمولی فقط تسک (و در صورت لزوم اینستنس ارجاع) را تمام می‌کند؛ ریشهٔ فرایند `running` می‌ماند.

### تکمیل و پایان فرایند

`POST /v1/tasks/{id}/complete-and-end` همان مجوز complete را دارد، بعد:

1. تسک را `done` می‌کند
2. بقیهٔ تسک‌های `open` / `claimed` همان فرایند را `cancelled` می‌کند (از کارتابل خارج می‌شوند)
3. اینستنس ریشه و ارجاع‌های فرزند را `completed` می‌گذارد

ارجاع بعدی روی همان ریشه `400` است.

### فرایندهای کاربر

`GET /v1/users/alice/processes` اینستنس‌های start که alice شروع کرده:

| `state` | معنی |
|---------|------|
| `notStarted` | start شده، هنوز ارجاعی ندارد |
| `open` | ارجاع خورده و ریشه هنوز `running` است |
| `closed` | با complete-and-end بسته شده |

بدون `state` هر سه لیست می‌شود. پاسخ همیشه شمارش دارد:

<div dir="ltr">

```json
{
  "user": "alice",
  "open": 1,
  "closed": 2,
  "notStarted": 3,
  "total": 6,
  "instances": [ ... ]
}
```

</div>

`?state=open` فقط لیست بازها را فیلتر می‌کند؛ شمارش‌ها همچنان هر سه دسته را نشان می‌دهد.

---

## ۴. Docker

<div dir="ltr">

```bash
docker compose up --build
curl -s http://localhost:8081/health
```

</div>

| متغیر | معنی |
|--------|------|
| `DATABASE_URL` | اتصال Postgres؛ خالی = حافظه |
| `ADDR` | پیش‌فرض `:8081` |
| `WF_USERS` / `WF_GROUP_<id>` | دایرکتوری استاتیک؛ بدون پیش‌فرض |
| `WF_API_KEYS` | اگر ست شود همهٔ مسیرها جز `/health` کلید می‌خواهند |

بدون `DATABASE_URL` داده با خاموش شدن سرور از بین می‌رود. Compose به Postgres وصل است.

اسکیما، جداول و ایندکس‌ها: [database.md](database.md).

</div>
