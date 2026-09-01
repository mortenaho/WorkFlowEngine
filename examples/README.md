<div dir="rtl">

# نمونه‌ها

سرور: `dotnet run --project src/TaskFlow.Server` روی `:8081`. برای `curl.sh` در Production کلید را در `WF_API_KEY` بگذارید.

<div dir="ltr">

```bash
./examples/curl.sh
dotnet run --project examples/Scenario
```

</div>

## اپ خاتمه همکاری (Next.js)

پیاده‌سازی کامل با UI و BFF: [github.com/mortenaho/employee-termination-app](https://github.com/mortenaho/employee-termination-app)

مرورگر فقط با `/api` همان اپ صحبت می‌کند؛ `X-API-Key` سمت سرور Next.js فرستاده می‌شود. فرایند `employeeTermination` شامل ارجاع موازی با `onAllCompleted` (زیرساخت / شبکه / مالی → امنیت) و `complete-and-end` در انتهاست. منطق مراحل در `lib/application/termination-flow.ts` است، نه داخل انجین.

```bash
# ترمینال ۱
dotnet run --project src/TaskFlow.Server

# ترمینال ۲
cd employee-termination-app && cp .env.local.example .env.local && npm install && npm run dev
```

## خرید: موازی → حقوقی (in-process)

`sara` فرایند `purchase` را start می‌کند و به `mortenaho` و `tina` ارجاع می‌دهد (`onAllCompleted` → گروه `legal`). بعد از join، `mortenaho` تسک حقوقی را claim و complete می‌کند.

<div dir="ltr">

```mermaid
flowchart TD
    start([Start purchase]) --> parallel[AssignTo mortenaho + tina]
    parallel --> a[mortenaho complete]
    parallel --> b[tina complete]
    a --> gate{both done?}
    b --> gate
    gate --> legal[AssignTo legal]
    legal --> done([Claim + complete])
```

</div>

BFF: [docs/usage.md#bff](../docs/usage.md#bff)

</div>
