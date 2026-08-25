<div dir="rtl">

# Workflow Engine

سرویس ارجاع و گردش کار روی ASP.NET Core (`net10.0`) با Clean Architecture. گراف BPMN ندارد؛ اپ شما چهار سرویس را صدا می‌زند.

<div dir="ltr">

```bash
dotnet run --project src/WorkflowEngine.Server
```

</div>

Swagger: [http://127.0.0.1:8081/swagger](http://127.0.0.1:8081/swagger)

هدر همهٔ درخواست‌ها: `X-Actor-Id`.

<div dir="ltr">

```bash
dotnet test
```

</div>

## سرویس‌ها

**۱. شروع فرایند** — `processKey` و `initiator` (پارامتر اختیاری). خروجی: `definitionKey` + `instanceId`.

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/processes/start \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: alice' \
  -d '{"processKey":"purchase","initiator":"alice","parameters":{"amount":150000000}}'
```

</div>

**لیست اجراها** — یک `processKey` می‌تواند بارها استارت شود (مثلاً خاتمه همکاری چند کارمند):

<div dir="ltr">

```bash
curl -s http://127.0.0.1:8081/v1/processes/employeeTermination/instances
```

</div>

**۲. ارجاع** به شخص، گروه، یا چند نفر. `definitionKey` بگویید این ارجاع برای کدام فرایند است. خروجی: `instanceId` جدید + تسک.

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/referrals \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: alice' \
  -d '{"definitionKey":"purchase","parentInstanceId":"INSTANCE","from":"alice","title":"بررسی","to":{"kind":"user","id":"bob"}}'
```

</div>

`to.kind`: `user` | `group` | `users` (برای چند نفر `ids` بفرستید).

**۳. کارتابل** — تسک‌های باز دست شخص یا گروه.

<div dir="ltr">

```bash
curl -s 'http://127.0.0.1:8081/v1/tasks?user=bob'
curl -s 'http://127.0.0.1:8081/v1/tasks?group=legal'
```

</div>

**کلیم** — تسک گروهی را بردارید تا فقط شما بتوانید complete کنید:

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/tasks/TASK_ID/claim \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: bob' \
  -d '{"from":"bob"}'
```

</div>

**۴. تکمیل چندنفره** — اگر درخواست به چند نفر رفته، ببینید همه complete کرده‌اند یا نه.

<div dir="ltr">

```bash
curl -s http://127.0.0.1:8081/v1/instances/REFERRAL_INSTANCE/completion
```

</div>

`allCompleted` در پاسخ `POST /v1/tasks/{id}/complete` هم هست.

**۵. تکمیل و پایان فرایند** — تسک را تمام می‌کند و کل پرونده را می‌بندد (بقیهٔ تسک‌های باز `cancelled`):

<div dir="ltr">

```bash
curl -s -X POST http://127.0.0.1:8081/v1/tasks/TASK_ID/complete-and-end \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: bob' \
  -d '{"note":"پرونده بسته شد"}'
```

</div>

**۶. فرایندهای کاربر** — شمارش باز / بسته / استارت‌نشده و لیست با فیلتر `state`:

<div dir="ltr">

```bash
curl -s 'http://127.0.0.1:8081/v1/users/alice/processes'
curl -s 'http://127.0.0.1:8081/v1/users/alice/processes?state=open'
```

</div>

## Docker

<div dir="ltr">

```bash
docker compose up --build
```

</div>

| متغیر | پیش‌فرض | معنی |
|--------|---------|------|
| `ADDR` | `:8081` | آدرس listen |
| `DATABASE_URL` | خالی | Postgres؛ وگرنه حافظه |
| `WF_USERS` | خالی | کاربران دایرکتوری استاتیک |
| `WF_GROUP_<id>` | — | اعضای گروه `id` (مثلاً `WF_GROUP_legal=bob,cara`) |
| `WF_API_KEYS` | خالی | اگر ست شود همهٔ مسیرها جز `/health` و swagger کلید می‌خواهند |

جزئیات: [docs/usage.md](docs/usage.md) · دیتابیس: [docs/database.md](docs/database.md)

</div>
