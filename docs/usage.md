<div dir="rtl">

# راهنمای استفاده (Usage Guide)

قابلیت‌های کلیدی موتور گردش کار:

۱. شروع فرایند جدید  
۲. فهرست‌گیری از اجراهای یک فرایند (`processKey`)  
۳. ارجاع کار به کاربر، گروه، یا چند نفر هم‌زمان  
۴. کارتابل وظایف باز  
۵. بررسی وضعیت تکمیل ارجاع‌های چندنفره  
۶. تکمیل وظیفه و بستن کامل پرونده  
۷. دریافت آمار و فهرست فرایندهای مرتبط با یک کاربر  

<div dir="ltr">

```bash
dotnet run --project src/WorkflowEngine.Server
# مستندات تعاملی Swagger: http://127.0.0.1:8081/swagger
```

</div>

| عملیات | نحوهٔ فراخوانی (REST API) |
|--------|---------------------------|
| شروع فرایند | `POST /v1/processes/start` با بدنهٔ `{ "processKey", "initiator", "parameters?" }` |
| لیست اجراها | `GET /v1/processes/{processKey}/instances` |
| ارجاع کار | `POST /v1/referrals` با بدنهٔ `{ "definitionKey", "parentInstanceId?", "to" }` |
| کارتابل وظایف | `GET /v1/tasks?user=mortenaho` یا `GET /v1/tasks?group=legal` |
| تحویل گرفتن تسک (Claim) | `POST /v1/tasks/{id}/claim` |
| لغو تحویل تسک (Unclaim) | `POST /v1/tasks/{id}/unclaim` |
| وضعیت تکمیل چندنفره | `GET /v1/instances/{id}/completion` |
| تکمیل وظیفه | `POST /v1/tasks/{id}/complete` |
| تکمیل و پایان فرایند | `POST /v1/tasks/{id}/complete-and-end` |
| فرایندهای کاربر | `GET /v1/users/{user}/processes` با فیلتر اختیاری `state=open`، `closed` یا `notStarted` |

امکان استفاده به دو شیوه وجود دارد: کتابخانهٔ داخلی در کد (`Application` + `Infrastructure`) و وب‌سرویس REST. نمونه اسکریپت آزمایشی: [`examples/curl.sh`](../examples/curl.sh).

---

## ۱. مفاهیم پایه

| واژه | توضیحات و مفهوم |
|------|-----------------|
| `processKey` | کلید شناسهٔ نوع فرایند (مانند `purchase` یا `employeeTermination`) |
| `definitionKey` | کلید تعریف ثبت‌شده برای فرایند که در خروجی متد شروع بازگردانده می‌شود |
| `instanceId` | شناسهٔ یک نمونهٔ اجرایی؛ فراخوانی `Start` نمونهٔ ریشه را می‌سازد و هر ارجاع (`Refer`) یک نمونهٔ جدید ایجاد می‌کند |
| `Task` | رکورد وظیفه در کارتابل که به یک کاربر یا یک گروه تخصیص یافته است |
| `Inbox` / `Tasks` | وظایف در وضعیت باز (`open`) متعلق به یک کاربر (شامل وظایف فردی و گروه‌های عضو) یا یک گروه |

شناسهٔ انجام‌دهندهٔ عملیات از طریق هدر `X-Actor-Id` ارسال می‌گردد. موتور گردش کار جدول اختصاصی برای کاربران ذخیره نمی‌کند و اطلاعات اعضا و گروه‌ها را از طریق رابط `IDirectory` دریافت می‌نماید.

---

## ۲. استفاده از طریق SDK در زبان C#‎

<div dir="ltr">

```csharp
using WorkflowEngine.Application;
using WorkflowEngine.Domain;
using WorkflowEngine.Infrastructure;

var dir = new StaticDirectory(
    ["alice", "mortenaho", "cara", "dan"],
    new Dictionary<string, IReadOnlyList<string>>
    {
        ["legal"] = ["mortenaho", "cara"],
        ["finance"] = ["dan", "cara"],
    });
var eng = new Engine(new MemoryStore(), dir);

// شروع فرایند
var started = await eng.Start("purchase", "alice", new Dictionary<string, object?> { ["amount"] = 1.5e8 });
// مقادیر خروجی: started.DefinitionKey, started.InstanceId

// ارجاع به کاربر یا گروه
var refer = await eng.Refer("alice", new ReferInput
{
    DefinitionKey = started.DefinitionKey,
    ParentInstanceId = started.InstanceId,
    Title = "بررسی حقوقی",
    ToKind = AssigneeKind.User, // یا AssigneeKind.Group یا AssigneeKind.Users
    ToId = "mortenaho",
    // ToIds = ["mortenaho", "cara"], // در صورت انتخاب AssigneeKind.Users
});
// مقادیر خروجی: refer.InstanceId, refer.Task / refer.Tasks

// دریافت وظایف کارتابل
var inbox = await eng.PendingTasks("mortenaho", "");
var groupInbox = await eng.PendingTasks("", "legal");

// تکمیل و بستن کل پرونده
var ended = await eng.CompleteAndEnd(refer.Task!.Id, "mortenaho", "پرونده بسته شد");
_ = ended.Process.Status; // برابر با completed

// دریافت آمار و وضعیت فرایندهای کاربر
var mine = await eng.ListUserProcesses("alice");
var open = await eng.ListUserProcesses("alice", "open");
```

</div>

در محیط پروداکشن می‌توانید رابط `IDirectory` را به سرویس هویت سازمانی خود (مانند Active Directory / LDAP یا SSO/Keycloak) متصل کنید. این رابط وظیفهٔ بررسی وجود کاربر و تعیین گروه‌ها و اعضا را برعهده دارد:

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

در ادامه دو نمونه پیاده‌سازی کاربردی برای محیط‌های سازمانی ارائه شده است:

### نمونهٔ ۱: پیاده‌سازی مبتنی بر SSO و سرویس‌های وب (مانند Keycloak یا REST Identity API)

این پیاده‌سازی نقش یک **پل ارتباطی (آداپتور)** میان موتور گردش کار و سامانهٔ احراز هویت مرکزی سازمان (SSO / IAM / Keycloak) را ایفا می‌کند. از آنجا که موتور گردش کار جدول اختصاصی برای کاربران یا چارت سازمانی ذخیره نمی‌کند، این کلاس از طریق وب‌سرویس (HTTP REST API) اطلاعات مورد نیاز را از سرویس هویت مرکزی دریافت می‌نماید:

- **بررسی وجود کاربر (`UserExists`):** هنگام ارجاع کار، از سرور SSO استعلام می‌کند که آیا این کاربر وجود دارد و فعال است یا خیر.
- **اعضای گروه (`GroupMembers`):** هنگامی که تسکی به یک واحد یا گروه (مانند `legal`) ارجاع داده می‌شود، اعضای فعال آن گروه را استعلام می‌نماید تا افراد مجاز به مشاهده و انجام تسک مشخص شوند.
- **گروه‌های کاربر (`UserGroups`):** هنگام باز شدن کارتابل یک کاربر، گروه‌ها و نقش‌های او را استعلام می‌کند تا علاوه بر وظایف فردی، وظایف ارجاع‌شده به گروه‌های او نیز در کارتابل نمایش داده شود.
- **کَش درون‌حافظه‌ای (`IMemoryCache`):** برای جلوگیری از ارسال مکرر درخواست شبکه در هر عملیات و افزایش کارایی سیستم، نتایج استعلام‌ها برای مدت مشخصی (مثلاً ۱۰ دقیقه) در حافظه کش می‌شوند.

<div dir="ltr">

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using WorkflowEngine.Application;

public sealed class HttpIdentityDirectory : IDirectory
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    public HttpIdentityDirectory(HttpClient http, IMemoryCache cache)
    {
        _http = http;
        _cache = cache;
    }

    public async Task<bool> UserExists(string userId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync($"user:exists:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var response = await _http.GetAsync($"api/v1/users/{Uri.EscapeDataString(userId)}", cancellationToken);
            return response.IsSuccessStatusCode;
        });
    }

    public async Task<IReadOnlyList<string>> GroupMembers(string groupId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync($"group:members:{groupId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var response = await _http.GetFromJsonAsync<List<string>>($"api/v1/groups/{Uri.EscapeDataString(groupId)}/members", cancellationToken);
            return (IReadOnlyList<string>)(response ?? []);
        }) ?? [];
    }

    public async Task<IReadOnlyList<string>> UserGroups(string userId, CancellationToken cancellationToken = default)
    {
        return await _cache.GetOrCreateAsync($"user:groups:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var response = await _http.GetFromJsonAsync<List<string>>($"api/v1/users/{Uri.EscapeDataString(userId)}/groups", cancellationToken);
            return (IReadOnlyList<string>)(response ?? []);
        }) ?? [];
    }
}
```

</div>

### نمونهٔ ۲: پیاده‌سازی مبتنی بر Active Directory / LDAP

در صورت استفاده از اکتیو دایرکتوری در محیط ویندوزی/سازمانی، می‌توانید با استفاده از کلاس‌های `System.DirectoryServices.AccountManagement` (یا پروتکل LDAP) مستقیماً اطلاعات را استعلام نمایید:

<div dir="ltr">

```csharp
using System.DirectoryServices.AccountManagement;
using WorkflowEngine.Application;

public sealed class ActiveDirectoryService : IDirectory
{
    private readonly string _domain;
    private readonly string? _container;

    public ActiveDirectoryService(string domain, string? container = null)
    {
        _domain = domain;
        _container = container;
    }

    public Task<bool> UserExists(string userId, CancellationToken cancellationToken = default)
    {
        using var ctx = new PrincipalContext(ContextType.Domain, _domain, _container);
        using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, userId);
        return Task.FromResult(user is not null && user.Enabled == true);
    }

    public Task<IReadOnlyList<string>> GroupMembers(string groupId, CancellationToken cancellationToken = default)
    {
        using var ctx = new PrincipalContext(ContextType.Domain, _domain, _container);
        using var group = GroupPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, groupId);
        if (group is null)
            return Task.FromResult<IReadOnlyList<string>>([]);

        var members = group.GetMembers(recursive: true)
            .OfType<UserPrincipal>()
            .Where(u => u.Enabled == true)
            .Select(u => u.SamAccountName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(members);
    }

    public Task<IReadOnlyList<string>> UserGroups(string userId, CancellationToken cancellationToken = default)
    {
        using var ctx = new PrincipalContext(ContextType.Domain, _domain, _container);
        using var user = UserPrincipal.FindByIdentity(ctx, IdentityType.SamAccountName, userId);
        if (user is null)
            return Task.FromResult<IReadOnlyList<string>>([]);

        var groups = user.GetAuthorizationGroups()
            .Select(g => g.SamAccountName)
            .Where(name => !string.IsNullOrEmpty(name))
            .ToList();

        return Task.FromResult<IReadOnlyList<string>>(groups);
    }
}
```

</div>

### نحوهٔ ثبت و استفاده در `Program.cs` (تزریق وابستگی‌ها)

برای جایگزینی `StaticDirectory` با پیاده‌سازی سازمانی در ریشهٔ برنامه (`Program.cs`):

<div dir="ltr">

```csharp
// نمونه با تزریق وابستگی HttpClient و IMemoryCache:
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IDirectory, HttpIdentityDirectory>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Identity:BaseUrl"] ?? "https://iam.company.local");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {builder.Configuration["Identity:ApiKey"]}");
});

// یا در صورت نمونه‌سازی مستقیم ActiveDirectory:
// IDirectory directory = new ActiveDirectoryService("corp.company.local", "DC=corp,DC=company,DC=local");

// دریافت دایرکتوری و راه‌اندازی Engine:
var directory = app.Services.GetRequiredService<IDirectory>();
var engine = new Engine(store, directory);
```

</div>

---

## ۳. راهنمای وب‌سرویس REST API

آدرس پایه: `http://localhost:8081`.  
هدرهای اصلی:
- `X-Actor-Id`: شناسهٔ کاربر انجام‌دهندهٔ درخواست (اجباری در اکثر عملیات). این هدر لاگین نیست و انجین توکن صادر نمی‌کند.
- `X-Tenant-Id`: شناسهٔ سازمان جهت جداسازی داده‌ها (اختیاری؛ پیش‌فرض: `default`).
- `X-API-Key`: کلید مشترک سرویس برای بک‌اند یا API Gateway. در پروداکشن اجباری است (متغیر `WF_API_KEYS`). مسیرهای `/health` و مستندات مستثنی هستند.

<a id="react-bff"></a>

### اتصال از React: کلید را در مرورگر نفرستید

`X-API-Key` برای `curl`، تست، و **بک‌اند شما** است، نه برای `fetch` در React. اگر فرانت کلید را در هدر بگذارد، در Network مرورگر لو می‌رود. معماری پیشنهادی و دیاگرام‌ها: [architecture.md — بخش ۶](architecture.md#api-key-architecture).

**React (فقط API خودتان، بدون کلید انجین):**

<div dir="ltr">

```js
// اشتباه: fetch('http://engine:8081/v1/tasks?user=mortenaho', { headers: { 'X-API-Key': '...' } })
await fetch("/api/inbox", { credentials: "include" });
```

</div>

**بک‌اند / BFF (این‌جا کلید از env خوانده می‌شود):**

<div dir="ltr">

```js
// مثال Express؛ معادل آن در ASP.NET / Nest یکسان است
app.get("/api/inbox", async (req, res) => {
  const userId = req.session.userId; // از لاگین اپ شما، نه از query مرورگر
  const r = await fetch(`${process.env.WF_ENGINE_URL}/v1/tasks?user=${encodeURIComponent(userId)}`, {
    headers: {
      "X-API-Key": process.env.WF_API_KEYS.split(",")[0],
      "X-Actor-Id": userId,
    },
  });
  res.status(r.status).json(await r.json());
});
```

</div>

فلو خلاصه: کاربر در اپ شما لاگین می‌کند → React به `/api/...` همان اپ درخواست می‌زند → بک‌اند جلسه را چک می‌کند → با `X-API-Key` و `X-Actor-Id` به انجین می‌زند → پاسخ را به React برمی‌گرداند.

### ۱. شروع فرایند (Start)

<div dir="ltr">

```json
POST /v1/processes/start
{ "processKey": "purchase", "initiator": "alice", "parameters": { "amount": 150000000 } }

→ { "definitionKey": "purchase", "instanceId": "..." }
```

</div>

در صورت خالی بودن فیلد `initiator`، مقدار هدر `X-Actor-Id` جایگزین می‌شود. اگر برای `processKey` تعریفی وجود نداشته باشد، به‌صورت خودکار ساخته می‌شود.

یک `processKey` می‌تواند بارها شروع شود (برای نمونه فرایند `employeeTermination` برای هر کارمند به‌طور مستقل اجرا می‌گردد):

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

فهرست فوق فقط نمونه‌های ریشه (Start) را بازمی‌گرداند؛ وظایف مربوط به ارجاع‌های فرزند نیز در فیلد `tasks` همان نمونه لیست می‌شوند.

### ۲. ارجاع کار (Referral)

<div dir="ltr">

```json
POST /v1/referrals
{
  "definitionKey": "purchase",
  "parentInstanceId": "<شناسه instance دریافتی از start>",
  "from": "alice",
  "title": "بررسی",
  "to": { "kind": "user", "id": "mortenaho" }
}

→ { "instanceId": "...", "definitionKey": "purchase", "task": { ... }, "tasks": [ ... ] }
```

</div>

در صورت خالی بودن فیلد `from`، مقدار هدر `X-Actor-Id` درج می‌شود.

| مقدار `to.kind` | فیلدهای مورد نیاز | نتیجه |
|-----------------|-------------------|--------|
| `user` | `id` | ایجاد یک تسک اختصاصی برای همان کاربر |
| `group` | `id` | ایجاد یک تسک گروهی؛ کلیهٔ اعضای گروه آن را در کارتابل مشاهده می‌کنند |
| `users` | `ids` | ایجاد یک تسک مجزا به‌ازای هر کاربر؛ وضعیت تکمیل کل ارجاع با شناسهٔ `instanceId` قابل رهگیری است |

### ۳. کارتابل وظایف (Tasks)

- `GET /v1/tasks?user=mortenaho` — دریافت وظایف شخصی کاربر `mortenaho` به‌همراه وظایف گروه‌هایی که وی در آن‌ها عضویت دارد.
- `GET /v1/tasks?group=legal` — صرفاً وظایف ارجاع‌شده به گروه `legal`.

هر تسک دارای مشخصه‌های `assigneeKind` و `assigneeId` است تا مالکیت آن مشخص باشد.

### ۴. تحویل گرفتن وظیفهٔ گروهی (Claim)

تسک‌های گروهی در ابتدا در وضعیت `open` قرار دارند و برای همهٔ اعضای گروه قابل مشاهده هستند. عضوی که می‌خواهد وظیفه را انجام دهد آن را رزرو می‌کند:

<div dir="ltr">

```bash
POST /v1/tasks/{id}/claim
{ "from": "mortenaho" }
```

</div>

وضعیت تسک به `claimed` و فیلد `claimedBy` به `mortenaho` تغییر می‌یابد. در صورت تلاش عضو دیگر برای کلیم هم‌زمان، خطای `409 Conflict` بازگردانده شده و تسک از کارتابل سایر اعضا خارج می‌شود. پس از این مرحله، صرفاً کاربر `mortenaho` مجاز به تکمیل تسک خواهد بود.

با فراخوانی `POST /v1/tasks/{id}/unclaim` تسک مجدداً آزاد شده و به وضعیت `open` بازمی‌گردد.

تسک‌های فردی نیازی به کلیم اجباری ندارند و مستقیماً قابل تکمیل هستند.

### ۵. بررسی وضعیت تکمیل چندنفره (Completion)

<div dir="ltr">

```bash
GET /v1/instances/{referralInstanceId}/completion
```

</div>

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

پاسخ متد `POST /v1/tasks/{id}/complete` نیز شامل فیلد `completion` است.

تکمیل معمولی صرفاً همان وظیفه (و در صورت اتمام همهٔ وظایف ارجاع، اینستنس ارجاع مربوطه) را می‌بندد؛ اما اینستنس ریشه در وضعیت `running` باقی می‌ماند.

### ۶. تکمیل وظیفه و بستن کل فرایند (Complete and End)

مسیر `POST /v1/tasks/{id}/complete-and-end` با همان مجوزهای تکمیل وظیفه، اقدامات زیر را به‌صورت یکپارچه انجام می‌دهد:

۱. وضعیت وظیفهٔ جاری را به `done` تغییر می‌دهد.  
۲. سایر وظایف باز (`open` یا `claimed`) در کل درخت فرایند را به وضعیت `cancelled` منتقل کرده و از کارتابل‌ها خارج می‌سازد.  
۳. اینستنس ریشه و تمام ارجاع‌های فرزند را در وضعیت `completed` قرار می‌دهد.  

پس از این مرحله، ثبت هرگونه ارجاع جدید روی این فرایند با خطای `400 Bad Request` مواجه خواهد شد.

### ۷. فرایندهای کاربر (User Processes)

متد `GET /v1/users/alice/processes` فهرست فرایندهایی که توسط کاربر `alice` ایجاد شده‌اند را بازمی‌گرداند:

| وضعیت (`state`) | توضیحات |
|-----------------|----------|
| `notStarted` | فرایند شروع شده اما هنوز ارجاعی برای آن ثبت نشده است |
| `open` | فرایند دارای ارجاع بوده و ریشه همچنان در وضعیت `running` قرار دارد |
| `closed` | فرایند به‌طور کامل خاتمه یافته (`completed`) است |

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

با ارسال پارامتر `?state=open`، لیست خروجی بر اساس فرایندهای باز فیلتر می‌شود؛ در حالی که مقادیر آماری (`open`, `closed`, `notStarted`, `total`) همواره وضعیت کلی را گزارش می‌دهند.

---

## ۴. پیکربندی محیط و Docker

اگر `ASPNETCORE_ENVIRONMENT` برابر `Development` نباشد و `WF_API_KEYS` خالی باشد، سرویس هنگام شروع متوقف می‌شود. این کلید توکن لاگین کاربر نیست؛ یک راز مشترک است که **فقط برنامهٔ شما (یا Gateway)** با هر درخواست می‌فرستد. کاربر نهایی و React به خودِ انجین لاگین نمی‌کنند و کلید را نمی‌بینند. نمونهٔ `curl` زیر برای تست از سرور/ترمینال است، نه برای کپی در فرانت.

<div dir="ltr">

```bash
cp .env.example .env
docker compose up --build
curl -s http://localhost:8081/health
curl -s -H 'X-API-Key: local-dev-key' -H 'X-Actor-Id: alice' \
  http://localhost:8081/v1/users/alice/processes
```

</div>

| متغیر محیطی | توضیحات |
|-------------|----------|
| `DATABASE_URL` | رشتهٔ اتصال به پایگاه دادهٔ Postgres؛ در صورت عدم تنظیم، داده‌ها در حافظه موقت نگهداری می‌شوند |
| `ADDR` | پورت و آدرس دریافت درخواست‌ها (پیش‌فرض: `:8081`) |
| `WF_USERS` / `WF_GROUP_<id>` | تنظیمات کاربران و اعضای گروه‌ها در دایرکتوری ایستا |
| `WF_API_KEYS` | کلید(های) مشترک API (جداشده با ویرگول). خارج از Development اجباری است. درخواست‌ها باید `X-API-Key` یا `Authorization: Bearer` معتبر بفرستند |
| `ASPNETCORE_ENVIRONMENT` | در `Development` می‌توان بدون کلید کار کرد؛ در `Production` بدون `WF_API_KEYS` سرویس بالا نمی‌آید |

در صورت عدم استفاده از `DATABASE_URL`، با خاموش شدن سرویس داده‌ها پاک می‌شوند. فایل `docker-compose.yml` به‌طور پیش‌فرض سرویس Postgres را پیکربندی و متصل می‌کند.

مستندات ساختار پایگاه داده: [database.md](database.md).

</div>
