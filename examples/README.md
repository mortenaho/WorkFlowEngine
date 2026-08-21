# نمونه‌ها

سناریوی خاتمه همکاری (خروجی JSON هر گام):

```bash
go run ./examples/scenario
```

سرور REST: `go run ./cmd/server` روی `:8081`.

```bash
./examples/curl.sh
```

سناریو: alice فرایند خرید را شروع می‌کند، به bob و cara ارجاع می‌دهد، کارتابل را می‌بیند، هر دو complete می‌کنند، `allCompleted` true می‌شود. سپس یک ارجاع گروهی به `legal`.
