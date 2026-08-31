---
layout: home

hero:
  name: TaskFlow
  text: موتور گردش کار
  tagline: ارجاع و مدیریت فرایند بر پایه ASP.NET Core — بدون BPMN، با API سبک و سرراست
  image:
    src: /favicon.svg
    alt: TaskFlow
  actions:
    - theme: brand
      text: راهنمای استفاده
      link: /usage
    - theme: alt
      text: معماری سیستم
      link: /architecture
    - theme: alt
      text: مخزن GitHub
      link: https://github.com/mortenaho/WorkFlowEngine

features:
  - icon: 🚀
    title: شروع سریع
    details: با Start یک فرایند را آغاز کنید و با AssignTo کار را به کاربر، گروه یا چند نفر هم‌زمان ارجاع دهید.
  - icon: 📋
    title: کارتابل و Claim
    details: PendingTasks برای inbox، Claim/Unclaim برای تسک‌های گروهی، و Complete برای بستن هر مرحله.
  - icon: 🔀
    title: ارجاع موازی
    details: ToKind=users چند تسک موازی می‌سازد؛ CompleteAndAssignTo بعد از allCompleted خودکار مرحله بعد را می‌سازد.
  - icon: 🏗️
    title: Clean Architecture
    details: Domain، Application، Infrastructure و Server — با Postgres یا MemoryStore و SDK برای میکروسرویس‌ها.
  - icon: 🔐
    title: BFF و API Key
    details: React مستقیم به انجین وصل نمی‌شود؛ بک‌اند شما با X-API-Key و X-Actor-Id با TaskFlow.Server صحبت می‌کند.
  - icon: 🐘
    title: Postgres
    details: مهاجرت خودکار، سه جدول اصلی (definitions، instances، tasks) — جزئیات کامل در مستند پایگاه داده.
---

## مستندات

| سند | توضیح |
|-----|--------|
| [راهنمای استفاده](./usage) | API، SDK، Docker، ارجاع موازی، CompleteAndAssignTo |
| [معماری سیستم](./architecture) | لایه‌ها، Engine، اورکستریتور، امنیت |
| [پایگاه داده](./database) | اسکیما، کوئری‌ها، مهاجرت، عملیات |

## اجرای محلی

<div dir="ltr">

```bash
dotnet run --project src/TaskFlow.Server
dotnet test
```

</div>

Swagger: `http://127.0.0.1:8081/swagger`
