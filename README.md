# Workflow Engine

سرویس ارجاع و گردش کار روی ASP.NET Core (`net10.0`). گراف BPMN ندارد؛ اپ شما چهار سرویس را صدا می‌زند.

```bash
dotnet run --project src/WorkflowEngine.Server
```

Swagger: [http://127.0.0.1:8081/swagger](http://127.0.0.1:8081/swagger)

هدر همهٔ درخواست‌ها: `X-Actor-Id`.

```bash
dotnet test
```

## سرویس‌ها

**۱. شروع فرایند** — `processKey` و `initiator` (پارامتر اختیاری). خروجی: `definitionKey` + `instanceId`.

```bash
curl -s -X POST http://127.0.0.1:8081/v1/processes/start \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: alice' \
  -d '{"processKey":"purchase","initiator":"alice","parameters":{"amount":150000000}}'
```

**لیست اجراها** — یک `processKey` می‌تواند بارها استارت شود (مثلاً خاتمه همکاری چند کارمند):

```bash
curl -s http://127.0.0.1:8081/v1/processes/employeeTermination/instances
```

**۲. ارجاع** به شخص، گروه، یا چند نفر. `definitionKey` بگویید این ارجاع برای کدام فرایند است. خروجی: `instanceId` جدید + تسک.

```bash
curl -s -X POST http://127.0.0.1:8081/v1/referrals \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: alice' \
  -d '{"definitionKey":"purchase","parentInstanceId":"INSTANCE","from":"alice","title":"بررسی","to":{"kind":"user","id":"bob"}}'
```

`to.kind`: `user` | `group` | `users` (برای چند نفر `ids` بفرستید).

**۳. کارتابل** — تسک‌های باز دست شخص یا گروه.

```bash
curl -s 'http://127.0.0.1:8081/v1/tasks?user=bob'
curl -s 'http://127.0.0.1:8081/v1/tasks?group=legal'
```

**کلیم** — تسک گروهی را بردارید تا فقط شما بتوانید complete کنید:

```bash
curl -s -X POST http://127.0.0.1:8081/v1/tasks/TASK_ID/claim \
  -H 'Content-Type: application/json' -H 'X-Actor-Id: bob' \
  -d '{"from":"bob"}'
```

**۴. تکمیل چندنفره** — اگر درخواست به چند نفر رفته، ببینید همه complete کرده‌اند یا نه.

```bash
curl -s http://127.0.0.1:8081/v1/instances/REFERRAL_INSTANCE/completion
```

`allCompleted` در پاسخ `POST /v1/tasks/{id}/complete` هم هست.

## Docker

```bash
docker compose up --build
```

| متغیر | پیش‌فرض | معنی |
|--------|---------|------|
| `ADDR` | `:8081` | آدرس listen |
| `DATABASE_URL` | خالی | Postgres؛ وگرنه حافظه |
| `WF_USERS` | `alice,bob,cara,dan,manager,ceo` | کاربران |
| `WF_GROUP_LEGAL` | `bob,cara` | اعضای `legal` |
| `WF_GROUP_FINANCE` | `dan,cara` | اعضای `finance` |
| `WF_API_KEYS` | خالی | اگر ست شود همهٔ مسیرها جز `/health` و swagger کلید می‌خواهند |

جزئیات: [docs/usage.md](docs/usage.md)
