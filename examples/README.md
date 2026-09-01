<div dir="rtl">

# نمونه‌ها و سناریوها

اجرای سرور REST با دستور `dotnet run --project src/TaskFlow.Server` روی پورت `:8081` (پروفایل پیش‌فرض `Development` است و بدون `WF_API_KEYS` کار می‌کند). اگر سرور را با Docker یا محیط Production اجرا می‌کنید، کلید را در `WF_API_KEY` یا `WF_API_KEYS` بگذارید تا `curl.sh` آن را به‌صورت `X-API-Key` بفرستد. این اسکریپت معادل بک‌اند است؛ کلید را در React کپی نکنید. معماری پیشنهادی: [docs/architecture.md](../docs/architecture.md#api-key-architecture).

<div dir="ltr">

```bash
./examples/curl.sh
dotnet run --project examples/Scenario
```

</div>

## سناریوی خرید: موازی → حقوقی

`sara` فرایند `purchase` را شروع می‌کند و هم‌زمان به `mortenaho` و `tina` ارجاع می‌دهد — با `onAllCompleted` برای مرحلهٔ حقوقی. هر نفر فقط `CompleteTask` می‌زند؛ وقتی **هر دو** تمام شدند، موتور خودکار ارجاع گروهی به `legal` را می‌سازد. بعد `mortenaho` آن را Claim و Complete می‌کند.

### دیاگرام ساده

<div dir="ltr">

```mermaid
flowchart TD
    start([sara: Start purchase]) --> parallel["AssignTo موازی به mortenaho و tina"]
    parallel --> a[mortenaho تمام می‌کند]
    parallel --> b[tina تمام می‌کند]
    a --> gate{هر دو تمام شدند؟}
    b --> gate
    gate -->|"هنوز نه"| wait[Next خالی می‌ماند]
    gate -->|"بله"| legal["AssignTo خودکار به گروه legal"]
    legal --> claim[mortenaho Claim می‌کند]
    claim --> done([Complete])

    style parallel fill:#C2E5FF,stroke:#3DADFF
    style gate fill:#FFECBD,stroke:#FFC943
    style legal fill:#CDF4D3,stroke:#66D575
```

</div>

### نکتهٔ مهم

موتور بعد از هر `CompleteTask` event داخلی می‌فرستد. اگر `onAllCompleted` در AssignTo موازی تعریف شده باشد، `ParallelJoinHandler` بعد از join خودکار مرحله بعد را می‌سازد. الگوی BFF: [docs/usage.md](../docs/usage.md#نمونه-bff-یک-endpoint-برای-همهٔ-تسک‌های-موازی).

</div>
