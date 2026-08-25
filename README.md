<div dir="rtl">

# Workflow Engine

سرویس ارجاع و مدیریت گردش کار بر پایهٔ ASP.NET Core (`net10.0`) با معماری تمیز (Clean Architecture). این سیستم بدون نیاز به مفسر پیچیدهٔ BPMN طراحی شده و قابلیت‌های گردش کار را از طریق APIهای سبک و سرراست در اختیار برنامه‌های شما قرار می‌دهد.

<div dir="ltr">

```bash
dotnet run --project src/WorkflowEngine.Server
```

</div>

مستندات Swagger: [http://127.0.0.1:8081/swagger](http://127.0.0.1:8081/swagger)

شناسایی کاربر با هدر `X-Actor-Id` است (لاگین داخل انجین وجود ندارد). در پروداکشن بدون `WF_API_KEYS` سرویس بالا نمی‌آید. این کلید را **React در مرورگر نفرستید**؛ فقط بک‌اند شما (BFF) از env آن را به‌صورت `X-API-Key` به انجین می‌دهد. جزئیات و دیاگرام: [docs/architecture.md](docs/architecture.md#api-key-architecture).

<div dir="ltr">

```bash
dotnet test
```

</div>

## قابلیت‌ها و سرویس‌ها

**۱. شروع فرایند** — ثبت شناسهٔ فرایند (`processKey`) و ایجادکننده (`initiator`) به همراه پارامترهای اختیاری. خروجی: `definitionKey` و `instanceId`.

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/processes/start \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: alice' \
  -d '{"processKey":"purchase","initiator":"alice","parameters":{"amount":150000000}}'
```

</div>

**لیست اجراها** — یک `processKey` می‌تواند چندین بار اجرا شود (مانند فرایند خاتمهٔ همکاری برای هر کارمند):

<div dir="ltr">

```bash
curl -s http://127.0.0.1:8081/v1/processes/employeeTermination/instances
```

</div>

**۲. ارجاع** — ارجاع کار به یک کاربر، گروه، یا چند نفر هم‌زمان. مقدار `definitionKey` مشخص می‌کند این ارجاع متعلق به کدام فرایند است. خروجی: `instanceId` جدید و تسک‌های ایجادشده.

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/referrals \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: alice' \
  -d '{"definitionKey":"purchase","parentInstanceId":"INSTANCE","from":"alice","title":"Review","to":{"kind":"user","id":"mortenaho"}}'
```

</div>

انواع گیرنده (`to.kind`): `user` (فردی)، `group` (گروهی) یا `users` (چندنفر هم‌زمان با فیلد `ids`).

**۳. کارتابل وظایف** — دریافت وظایف باز متعلق به شخص یا گروه:

<div dir="ltr">

```bash
curl -s 'http://127.0.0.1:8081/v1/tasks?user=mortenaho'
curl -s 'http://127.0.0.1:8081/v1/tasks?group=legal'
```

</div>

**۴. تخصیص و تحویل کار (Claim)** — رزرو کردن تسک گروهی توسط یکی از اعضا جهت جلوگیری از اقدام هم‌زمان دیگران:

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/tasks/TASK_ID/claim \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: mortenaho' \
  -d '{"from":"mortenaho"}'
```

</div>

**۵. تکمیل چندنفره** — بررسی وضعیت تکمیل وظایف ارجاع‌شده به چند نفر:

<div dir="ltr">

```bash
curl -s http://127.0.0.1:8081/v1/instances/REFERRAL_INSTANCE/completion
```

</div>

فیلد `allCompleted` در پاسخ متد `POST /v1/tasks/{id}/complete` نیز برگردانده می‌شود.

**۶. تکمیل و پایان فرایند** — تکمیل تسک جاری و بستن کامل پرونده (تغییر وضعیت سایر وظایف باز به `cancelled`):

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/tasks/TASK_ID/complete-and-end \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: mortenaho' \
  -d '{"note":"Case closed"}'
```

</div>

**۷. فرایندهای کاربر** — شمارش و دریافت فهرست فرایندهای آغازشده توسط کاربر بر اساس وضعیت (`state` شامل باز، بسته، یا شروع‌نشده):

<div dir="ltr">

```bash
curl -s 'http://127.0.0.1:8081/v1/users/alice/processes'
curl -s 'http://127.0.0.1:8081/v1/users/alice/processes?state=open'
```

</div>

## استقرار با Docker

خارج از `Development` متغیر `WF_API_KEYS` اجباری است. برای اجرای محلی با Compose:

<div dir="ltr">

```bash
cp .env.example .env
docker compose up --build
```

</div>

| متغیر محیطی | مقدار پیش‌فرض | توضیحات |
|-------------|---------------|----------|
| `ADDR` | `:8081` | آدرس گوش دادن؛ فقط اگر `ASPNETCORE_URLS` خالی باشد |
| `ASPNETCORE_URLS` | خالی | اولویت بالاتر برای آدرس گوش دادن |
| `DATABASE_URL` | خالی | اتصال به Postgres (خالی = حافظه موقت) |
| `WF_USERS` | خالی | خالی = `OpenDirectory` (هر شناسه پذیرفته می‌شود)؛ در غیر این صورت فهرست کاربران دایرکتوری ایستا |
| `WF_GROUP_<id>` | — | اعضای گروه `id` (مثال: `WF_GROUP_legal=mortenaho,cara`) |
| `WF_API_KEYS` | در Development خالی؛ در پروداکشن اجباری | کلید مشترک سرویس (نه توکن لاگین). فقط بک‌اند/Gateway با `X-API-Key` بفرستد — نه React |
| `ASPNETCORE_ENVIRONMENT` | بسته به اجرا | در `Development` می‌توان بدون کلید کار کرد |

راهنمای کامل پیکربندی: [docs/usage.md — بخش ۴](docs/usage.md#۴-پیکربندی-محیط-و-docker).

**معماری پیشنهادی با فرانت React:** مرورگر → API اپ شما (جلسه/کوکی) → انجین روی شبکهٔ داخلی با `X-API-Key`. اگر `fetch` مستقیم از React به پورت `8081` کلید را در هدر بگذارد، در Network مرورگر لو می‌رود.

راهنمای جامع: [docs/usage.md](docs/usage.md) · معماری کلید و BFF: [docs/architecture.md](docs/architecture.md#api-key-architecture) · پایگاه داده: [docs/database.md](docs/database.md)

</div>
