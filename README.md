<div dir="rtl">

# Workflow Engine

سرویس ارجاع و مدیریت گردش کار بر پایهٔ ASP.NET Core (`net10.0`) با معماری تمیز (Clean Architecture). این سیستم بدون نیاز به مفسر پیچیدهٔ BPMN طراحی شده و قابلیت‌های گردش کار را از طریق APIهای سبک و سرراست در اختیار برنامه‌های شما قرار می‌دهد.

<div dir="ltr">

```bash
dotnet run --project src/WorkflowEngine.Server
```

</div>

مستندات Swagger: [http://127.0.0.1:8081/swagger](http://127.0.0.1:8081/swagger)

شناسایی کاربر در تمام درخواست‌ها از طریق هدر `X-Actor-Id` انجام می‌شود. این هدر لاگین نیست؛ در پروداکشن سرویس بدون `WF_API_KEYS` اصلاً بالا نمی‌آید و کلاینت باید کلید را با `X-API-Key` بفرستد.

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
  -d '{"definitionKey":"purchase","parentInstanceId":"INSTANCE","from":"alice","title":"بررسی","to":{"kind":"user","id":"bob"}}'
```

</div>

انواع گیرنده (`to.kind`): `user` (فردی)، `group` (گروهی) یا `users` (چندنفر هم‌زمان با فیلد `ids`).

**۳. کارتابل وظایف** — دریافت وظایف باز متعلق به شخص یا گروه:

<div dir="ltr">

```bash
curl -s 'http://127.0.0.1:8081/v1/tasks?user=bob'
curl -s 'http://127.0.0.1:8081/v1/tasks?group=legal'
```

</div>

**۴. تخصیص و تحویل کار (Claim)** — رزرو کردن تسک گروهی توسط یکی از اعضا جهت جلوگیری از اقدام هم‌زمان دیگران:

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/tasks/TASK_ID/claim \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: bob' \
  -d '{"from":"bob"}'
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
  -H 'Content-Type: application/json' -H 'X-Actor-Id: bob' \
  -d '{"note":"پرونده بسته شد"}'
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
| `ADDR` | `:8081` | آدرس گوش دادن به درخواست‌ها (Listen) |
| `DATABASE_URL` | خالی | اتصال به Postgres (در صورت خالی بودن، از ذخیره‌ساز حافظه‌ای استفاده می‌شود) |
| `WF_USERS` | خالی | فهرست کاربران دایرکتوری ایستا |
| `WF_GROUP_<id>` | — | اعضای گروه `id` (مثال: `WF_GROUP_legal=bob,cara`) |
| `WF_API_KEYS` | در Development خالی؛ در پروداکشن اجباری | کلید مشترک سرویس (نه توکن لاگین کاربر). با هدر `X-API-Key` یا `Authorization: Bearer` ارسال شود. بدون آن در غیر از Development فرایند شروع نمی‌شود. |

راهنمای جامع: [docs/usage.md](docs/usage.md) · مستندات پایگاه داده: [docs/database.md](docs/database.md)

</div>
