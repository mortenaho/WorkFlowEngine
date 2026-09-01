---
title: شروع سریع
description: از نصب تا اولین فرایند TaskFlow در چند دقیقه
---

<div dir="rtl">

# شروع سریع

این صفحه کوتاه‌ترین مسیر برای آشنایی با TaskFlow است. اگر جزئیات بیشتری می‌خواهید، [راهنمای کامل](./usage) را ببینید.

برای یک پیاده‌سازی کامل با UI و BFF، مخزن [employee-termination-app](https://github.com/mortenaho/employee-termination-app) را ببینید: Next.js، فرایند `employeeTermination`، ارجاع موازی با `onAllCompleted`، و `complete-and-end` در انتها. انجین را جدا اجرا کنید (`dotnet run --project src/TaskFlow.Server`) و اپ را با `npm run dev` بالا بیاورید.

---

## پیش‌نیازها

- [.NET SDK 10](https://dotnet.microsoft.com/download) یا نسخهٔ پروژه
- (اختیاری) Docker برای Postgres

---

## اجرای سرور {#اجرای-سرور}

<div dir="ltr">

```bash
git clone https://github.com/mortenaho/WorkFlowEngine.git
cd WorkFlowEngine
dotnet run --project src/TaskFlow.Server
```

</div>

سرور روی `http://127.0.0.1:8081` بالا می‌آید. در محیط `Development` بدون `WF_API_KEYS` هم کار می‌کند.

مستندات تعاملی: [Swagger](http://127.0.0.1:8081/swagger)

---

## اولین فرایند (curl)

### ۱. شروع

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/processes/start \
  -H 'Content-Type: application/json' \
  -H 'X-Actor-Id: alice' \
  -d '{"processKey":"purchase","initiator":"alice","parameters":{"amount":100}}'
```

</div>

خروجی: `definitionKey` و `instanceId` — این‌ها را برای مراحل بعد نگه دارید.

### ۲. ارجاع به کاربر

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/assignments \
  -H 'Content-Type: application/json' \
  -H 'X-Actor-Id: alice' \
  -d '{
    "definitionKey": "purchase",
    "parentInstanceId": "INSTANCE_ID",
    "title": "بررسی",
    "to": { "kind": "user", "id": "mortenaho" }
  }'
```

</div>

### ۳. کارتابل و تکمیل

<div dir="ltr">

```bash
# کارتابل mortenaho
curl -s 'http://127.0.0.1:8081/v1/tasks?user=mortenaho' -H 'X-Actor-Id: mortenaho'

# تکمیل تسک
curl -s -X POST http://127.0.0.1:8081/v1/tasks/TASK_ID/complete \
  -H 'Content-Type: application/json' \
  -H 'X-Actor-Id: mortenaho' \
  -d '{"note":"تأیید شد"}'
```

</div>

اسکریپت کامل: [`examples/curl.sh`](https://github.com/mortenaho/WorkFlowEngine/blob/main/examples/curl.sh)

---

## اتصال از C# (میکروسرویس)

یک بار سرور را بالا بیاورید؛ هر سرویس فقط SDK را اضافه می‌کند:

<div dir="ltr">

```csharp
builder.Services.AddTaskFlowClient(o =>
{
    o.BaseAddress = new Uri("http://127.0.0.1:8081/");
});

// در هندلر:
var started = await tf.Start("purchase", "alice");
```

</div>

جزئیات: [SDK و میکروسرویس](./usage#۲-استفاده-از-طریق-sdk-در-زبان-c)

---

## مرحلهٔ بعد

| موضوع | لینک |
|--------|------|
| اپ نمونه (Next.js BFF) | [employee-termination-app](https://github.com/mortenaho/employee-termination-app) |
| ارجاع موازی و `onAllCompleted` | [usage → تخصیص موازی](./usage#تخصیص-موازی) |
| معماری BFF و API Key | [architecture](./architecture#۶-معماری-پیشنهادی-استقرار-react-بک‌اند-و-کلید-api) |
| Postgres و مهاجرت | [database](./database#۲-نحوهٔ-اتصال-و-مهاجرت-خودکار) |
| Docker Compose | [usage → پیکربندی](./usage#۴-پیکربندی-محیط-و-docker) |

</div>
