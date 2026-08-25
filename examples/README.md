<div dir="rtl">

# نمونه‌ها و سناریوها

اجرای سرور REST با دستور `dotnet run --project src/WorkflowEngine.Server` روی پورت `:8081` (پروفایل پیش‌فرض `Development` است و بدون `WF_API_KEYS` کار می‌کند). اگر سرور را با Docker یا محیط Production اجرا می‌کنید، کلید را در `WF_API_KEY` یا `WF_API_KEYS` بگذارید تا `curl.sh` آن را به‌صورت `X-API-Key` بفرستد. این اسکریپت معادل بک‌اند است؛ کلید را در React کپی نکنید. معماری پیشنهادی: [docs/architecture.md](../docs/architecture.md#api-key-architecture).

<div dir="ltr">

```bash
./examples/curl.sh
dotnet run --project examples/Scenario
```

</div>

**شرح سناریو:**
کاربر `alice` فرایند خرید (`purchase`) را آغاز می‌کند و آن را به‌صورت هم‌زمان به دو کاربر `bob` و `cara` ارجاع می‌دهد. با تکمیل وظیفه توسط هر دو نفر، وضعیت `allCompleted` برابر `true` می‌شود. سپس یک ارجاع گروهی به تیم حقوقی (`legal`) ثبت شده و کاربر `bob` وظیفه را به خود تخصیص داده (Claim) و تکمیل می‌کند.

</div>
