# نمونه‌ها

سرور REST: `dotnet run --project src/WorkflowEngine.Server` روی `:8081`.

```bash
./examples/curl.sh
dotnet run --project examples/Scenario
```

سناریو: alice فرایند خرید را شروع می‌کند، به bob و cara ارجاع می‌دهد، هر دو complete می‌کنند، `allCompleted` true می‌شود. سپس یک ارجاع گروهی به `legal` که bob کلیم و complete می‌کند.
