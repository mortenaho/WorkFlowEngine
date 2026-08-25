<div dir="rtl">

# مستند دیتابیس Workflow Engine

منبع حقیقت اسکیما، کلاس `PostgresStore` در `src/WorkflowEngine.Infrastructure/PostgresStore.cs` است. مهاجرت در اولین اتصال اجرا می‌شود؛ فایل SQL جدا یا ابزار migration خارجی وجود ندارد.

این سند هر جدول، ستون، ایندکس، الگوی کوئری و محدودیت عملیاتی را پوشش می‌دهد.

---

## ۱. نقش دیتابیس

انجین گراف BPMN ذخیره نمی‌کند. Postgres فقط سه موجودیت پایدار را نگه می‌دارد:

| موجودیت | جدول | معنی کسب‌وکاری |
|---------|------|----------------|
| تعریف فرایند | `definitions` | نوع فرایند (`purchase`، `employeeTermination`، …) |
| اجرای فرایند | `instances` | یک استارت یا یک ارجاع |
| کار کارتابل | `tasks` | کار ارجاع‌شده به شخص یا گروه |

هویت کاربر و عضویت گروه در دیتابیس نیست. آن‌ها از پورت `IDirectory` می‌آیند (`StaticDirectory` یا پیاده‌سازی LDAP/SSO شما).

دو پیاده‌سازی `IStore` وجود دارد:

| پیاده‌سازی | شرط | ماندگاری |
|------------|------|----------|
| `PostgresStore` | متغیر `DATABASE_URL` ست باشد | پایدار |
| `MemoryStore` | `DATABASE_URL` خالی | با خاموش شدن فرایند از بین می‌رود |

سرور در `Program.cs` یکی را انتخاب می‌کند. منطق کسب‌وکار (`Engine`) به هیچ‌کدام وابسته نیست.

---

## ۲. اتصال و مهاجرت

### رشته اتصال

متغیر محیط:

<div dir="ltr">

```
DATABASE_URL=postgres://workflow:workflow@postgres:5432/workflow?sslmode=disable
```

</div>

Compose همین مقدار را به سرویس `engine` می‌دهد. کاربر / رمز / دیتابیس پیش‌فرض:

| مورد | مقدار Compose |
|------|----------------|
| تصویر | `postgres:16-alpine` |
| کاربر | `workflow` |
| رمز | `workflow` |
| دیتابیس | `workflow` |
| پورت میزبان | `5432` |
| volume | `pgdata` |

اگر Postgres هنوز بالا نیامده باشد، سرور تا ۳۰ ثانیه با فاصلهٔ یک‌ثانیه‌ای Retry می‌کند.

### مهاجرت خودکار

`PostgresStore.Open(dsn)` بلافاصله `Migrate` را صدا می‌زند:

1. `CREATE TABLE IF NOT EXISTS` برای سه جدول و ایندکس‌ها
2. `ALTER TABLE … ADD COLUMN IF NOT EXISTS` برای ستون‌هایی که بعداً اضافه شده‌اند

مهاجرت idempotent است؛ روی دیتابیس خالی و روی دیتابیس قدیمی هر دو بی‌خطر اجرا می‌شود. تراکنش جدا دور کل DDL پیچیده نشده؛ هر دسته یک `ExecuteNonQuery` است.

ستون‌های جدید را همیشه هم در `Schema` و هم در `Alters` نگه دارید تا نصب تازه و ارتقای نصب قدیمی هر دو درست باشند.

---

## ۳. مدل مفهومی

<div dir="ltr">

```
tenant
  └── definition          (یک نوع فرایند؛ مثلاً purchase)
        └── instance ریشه  (خروجی Start؛ parent_instance_id = '')
              └── instance فرزند  (هر Refer یک ردیف جدید)
                    └── task(ها)  (کارتابل user / group)
```

</div>

قواعد مهم:

- `Start` تسک نمی‌سازد؛ فقط تعریف (در صورت نبود) و یک اینستنس ریشه می‌سازد.
- هر `Refer` یک اینستنس **جدید** می‌سازد. اگر `parentInstanceId` داده شود، فرزند به ریشه وصل می‌شود.
- تسک‌ها به اینستنس **ارجاع** وصل‌اند، نه لزوماً به اینستنس استارت.
- برای لیست کردن کارتابل یک فرایند ریشه، کوئری تسک‌ها هم `instance_id` و هم `parent_instance_id` را می‌بیند.

<div dir="ltr">

```mermaid
erDiagram
  definitions ||--o{ instances : "definition_id / definition_key"
  instances ||--o{ instances : "parent_instance_id"
  instances ||--o{ tasks : "instance_id"
  instances ||--o{ tasks : "parent_instance_id (denormalized)"

  definitions {
    text id PK
    text tenant_id
    text key
    text name
    int version
    jsonb graph
    boolean published
    timestamptz created_at
  }

  instances {
    text id PK
    text tenant_id
    text definition_id
    text definition_key
    text parent_instance_id
    text status
    jsonb vars
    text started_by
    int version
    timestamptz created_at
    timestamptz updated_at
  }

  tasks {
    text id PK
    text tenant_id
    text instance_id
    text parent_instance_id
    text definition_key
    text node_id
    text token_id
    text title
    text note
    text assignee_kind
    text assignee_id
    text claimed_by
    text assigned_by
    text status
    text group_mode
    text return_reason
    timestamptz created_at
    timestamptz updated_at
    timestamptz completed_at
  }
```

</div>

**قید خارجی (FOREIGN KEY) در اسکیما تعریف نشده است.** یکپارچگی ارجاعی را لایهٔ Application تضمین می‌کند. حذف آبشاری در دیتابیس وجود ندارد.

---

## ۴. شناسه‌ها، زمان، و چندمستأجری

### شناسه

`Ids.New()` شانزده بایت تصادفی رمزنگاری‌شده می‌سازد و به hex کوچک ۳۲ کاراکتری تبدیل می‌کند. همهٔ PKها (`definitions.id`، `instances.id`، `tasks.id`) از همین نوع‌اند. توالی (sequence) و UUID بومی Postgres استفاده نمی‌شود.

### زمان

همهٔ زمان‌ها `TIMESTAMPTZ` هستند. موتور با ساعت UTC کار می‌کند. هنگام خواندن، اگر درایور `DateTimeKind` را Unspecified برگرداند، `PostgresStore` آن را UTC فرض می‌کند.

### مستأجر (`tenant_id`)

هر سه جدول ستون `tenant_id TEXT NOT NULL DEFAULT 'default'` دارند.

- خالی یا null در کد به `'default'` نرمال می‌شود (`Tenant.Normalize`).
- در REST از هدر `X-Tenant-Id` می‌آید؛ بدون هدر همان `default` است.
- جداسازی در Application است، نه Row Level Security پستگرس.
- `GetInstance` / `GetTask` اگر `tenant_id` ردیف با مستأجر جاری فرق کند `ForbiddenTenant` می‌دهند.
- ایندکس لیست فرایندها `tenant_id` را در کلید دارند.

مستأجر جدول جدا ندارد؛ فقط یک برچسب روی ردیف‌هاست.

---

## ۵. جدول `definitions`

کاتالوگ نوع فرایند. گراف BPMN در مسیر اجرایی فعلی خوانده نمی‌شود.

| ستون | نوع | پیش‌فرض | نقش |
|------|-----|---------|-----|
| `id` | `TEXT` PK | — | شناسهٔ پایدار تعریف |
| `tenant_id` | `TEXT NOT NULL` | `'default'` | جداسازی مستأجر |
| `key` | `TEXT NOT NULL` | — | کلید کسب‌وکاری (`purchase`). در API همان `processKey` / `definitionKey` است |
| `name` | `TEXT NOT NULL` | `''` | نام نمایشی؛ اگر خالی ثبت شود برابر `key` می‌شود |
| `version` | `INT NOT NULL` | `1` | رزرو؛ موتور نسخه را مدیریت نمی‌کند |
| `graph` | `JSONB NOT NULL` | `'{}'` | رزرو برای گراف آینده؛ INSERT فعلی همیشه `'{}'` می‌نویسد و SELECT آن را نمی‌خواند |
| `published` | `BOOLEAN NOT NULL` | `TRUE` | رزرو؛ فیلتر انتشار وجود ندارد |
| `created_at` | `TIMESTAMPTZ NOT NULL` | `NOW()` | زمان ایجاد |

### نوشتن

<div dir="ltr">

```sql
INSERT INTO definitions (id, tenant_id, key, name, version, graph, published, created_at)
VALUES ($1,$2,$3,$4,1,'{}'::jsonb,TRUE,$5)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name
```

</div>

فقط `name` در تداخل به‌روز می‌شود. `Start` اگر تعریفی برای `(tenant_id, key)` نباشد آن را می‌سازد. `Refer` تعریف موجود می‌خواهد؛ وگرنه خطا می‌دهد.

### خواندن

- با `id`
- با `(tenant_id, key)` مرتب‌شده با `created_at DESC LIMIT 1` — آخرین تعریف همان کلید

**قید UNIQUE روی `(tenant_id, key)` نیست.** اگر دو ردیف با یک کلید ساخته شود، کوئری جدیدترین را برمی‌گرداند.

---

## ۶. جدول `instances`

یک ردیف = یک اجرا. استارت و هر ارجاع هر کدام یک ردیف جدا هستند.

| ستون | نوع | پیش‌فرض | نقش |
|------|-----|---------|-----|
| `id` | `TEXT` PK | — | `instanceId` در API |
| `tenant_id` | `TEXT NOT NULL` | `'default'` | مستأجر |
| `definition_id` | `TEXT NOT NULL` | — | FK منطقی به `definitions.id` |
| `definition_key` | `TEXT NOT NULL` | — | کپی `definitions.key` برای کوئری بدون JOIN |
| `parent_instance_id` | `TEXT NOT NULL` | `''` | اینستنس ریشهٔ استارت. ریشه = رشتهٔ خالی، نه NULL |
| `status` | `TEXT NOT NULL` | — | ماشین وضعیت اینستنس |
| `vars` | `JSONB NOT NULL` | `'{}'` | پارامترهای فرایند (`parameters` در دامنه) |
| `started_by` | `TEXT NOT NULL` | — | شروع‌کننده / ارجاع‌دهنده |
| `version` | `INT NOT NULL` | `1` | رزرو؛ optimistic locking روی آن نیست |
| `created_at` | `TIMESTAMPTZ NOT NULL` | — | زمان ایجاد |
| `updated_at` | `TIMESTAMPTZ NOT NULL` | — | آخرین تغییر وضعیت یا پارامتر |

### وضعیت اینستنس (`status`)

| مقدار | ثابت دامنه | معنی |
|-------|------------|------|
| `running` | `InstanceStatus.Running` | هنوز باز است |
| `completed` | `InstanceStatus.Completed` | همهٔ تسک‌های همان اینستنس تمام شده، یا کل درخت با CompleteAndEnd بسته شده |

ارجاع روی اینستنس `completed` رد می‌شود.

طبقه‌بندی نمایشی لیست کاربر (در دیتابیس ذخیره نمی‌شود):

| حالت API | شرط |
|----------|------|
| `notStarted` | اینستنس `running` و هیچ تسکی ندارد (فقط Start خورده) |
| `open` | تسک دارد و اینستنس هنوز `completed` نیست |
| `closed` | `status = completed` |

### والد و فرزند

- ریشه: `parent_instance_id = ''`
- فرزند Refer: `parent_instance_id = <id اینستنس Start>`
- لیست اجراهای یک `processKey` فقط ریشه‌ها را می‌آورد: `parent_instance_id = ''`
- `ListChildInstances` فرزندان یک والد را با `ORDER BY created_at` می‌آورد
- CompleteAndEnd ریشه را پیدا می‌کند، تسک‌های باز درخت را `cancelled` می‌کند، سپس ریشه و همهٔ فرزندان را `completed` می‌کند

عمق درخت در مدل فعلی یک سطح است: فرزند به ریشه اشاره می‌کند، نه به ارجاع میانی.

### پارامترها (`vars`)

ستون JSONB است؛ در دامنه `ProcessInstance.Parameters`.

- `Start` دیکشنری ورودی را می‌نویسد.
- `Refer` پارامتر همان ارجاع را روی اینستنس **فرزند** می‌نویسد.
- `CompleteTask` اگر `parameters` بفرستد، با `Vars.Merge` روی اینستنس همان تسک ادغام می‌شود (کلید تکراری بازنویسی می‌شود).

سریال‌سازی با `System.Text.Json` است. خواندن هم `string`، هم `JsonDocument` و هم `JsonElement` را پوشش می‌دهد.

### به‌روزرسانی

<div dir="ltr">

```sql
UPDATE instances SET status=$2, vars=$3::jsonb, updated_at=$4 WHERE id=$1
```

</div>

اگر هیچ ردیفی عوض نشود `NotFound` پرتاب می‌شود. `definition_*` و `started_by` و `parent_instance_id` پس از INSERT عوض نمی‌شوند.

---

## ۷. جدول `tasks`

آیتم کارتابل. هویت گیرنده اینجا ذخیره می‌شود؛ عضویت گروه نه.

| ستون | نوع | پیش‌فرض | نقش |
|------|-----|---------|-----|
| `id` | `TEXT` PK | — | `taskId` در API |
| `tenant_id` | `TEXT NOT NULL` | `'default'` | مستأجر |
| `instance_id` | `TEXT NOT NULL` | — | اینستنس ارجاع (نه لزوماً ریشه) |
| `parent_instance_id` | `TEXT NOT NULL` | `''` | کپی والد برای لیست تسک‌های یک فرایند ریشه بدون JOIN |
| `definition_key` | `TEXT NOT NULL` | `''` | کپی کلید فرایند |
| `node_id` | `TEXT NOT NULL` | `''` | رزرو گراف؛ موتور نمی‌نویسد |
| `token_id` | `TEXT NOT NULL` | `''` | رزرو توکن اجرا؛ موتور نمی‌نویسد |
| `title` | `TEXT NOT NULL` | `''` | عنوان ارجاع (مثلاً «بررسی حقوقی») |
| `note` | `TEXT NOT NULL` | `''` | یادداشت تکمیل |
| `assignee_kind` | `TEXT NOT NULL` | — | `user` یا `group` (پس از نرمال‌سازی `users`) |
| `assignee_id` | `TEXT NOT NULL` | — | شناسهٔ شخص یا گروه |
| `claimed_by` | `TEXT NOT NULL` | `''` | کسی که تسک را برداشته یا تکمیل کرده |
| `assigned_by` | `TEXT NOT NULL` | `''` | ارجاع‌دهنده |
| `status` | `TEXT NOT NULL` | — | ماشین وضعیت تسک |
| `group_mode` | `TEXT NOT NULL` | `''` | رزرو؛ موتور نمی‌نویسد |
| `return_reason` | `TEXT NOT NULL` | `''` | رزرو؛ موتور نمی‌نویسد |
| `created_at` | `TIMESTAMPTZ NOT NULL` | — | ایجاد |
| `updated_at` | `TIMESTAMPTZ NOT NULL` | — | آخرین انتقال وضعیت |
| `completed_at` | `TIMESTAMPTZ` nullable | `NULL` | زمان تکمیل؛ برای `done` پر می‌شود |

`SaveTask` این ستون‌ها را می‌نویسد: `id, tenant_id, instance_id, parent_instance_id, definition_key, title, note, assignee_kind, assignee_id, assigned_by, claimed_by, status, created_at, updated_at, completed_at`.

### نوع گیرنده (`assignee_kind`)

| مقدار ذخیره‌شده | ورودی Refer | نتیجه |
|-----------------|-------------|--------|
| `user` | `to.kind = user` | یک تسک برای همان `assignee_id` |
| `group` | `to.kind = group` | یک تسک؛ همهٔ اعضای گروه در کارتابل می‌بینند |
| `user` (چند ردیف) | `to.kind = users` | یک تسک به‌ازای هر id، همه روی **یک** `instance_id` |

مقدار `users` در جدول ذخیره نمی‌شود؛ موتور قبل از INSERT آن را به چند ردیف `user` تبدیل می‌کند.

گروه خالی در Directory هنگام Refer خطا می‌دهد (`EmptyGroup`). خود جدول تسک اعضای گروه را نگه نمی‌دارد.

### وضعیت تسک (`status`)

<div dir="ltr">

```
        claim                 complete
  open ──────► claimed ──────► done
    ▲             │
    └─ unclaim ───┘

  open / claimed ──completeAndEnd──► cancelled
```

</div>

| مقدار | ثابت | چه زمانی |
|-------|------|----------|
| `open` | `TaskStatus.Open` | تازه ارجاع شده، یا unclaim شده |
| `claimed` | `TaskStatus.Claimed` | یک نفر تسک گروهی (یا اختیاری شخصی) را برداشته |
| `done` | `TaskStatus.Done` | تکمیل موفق |
| `cancelled` | `TaskStatus.Cancelled` | CompleteAndEnd بقیهٔ تسک‌های باز درخت را باطل کرده |

قوانین عمل:

- تسک **شخصی**: همان `assignee_id` می‌تواند بدون claim هم complete کند. اگر claimed باشد فقط `claimed_by` complete می‌کند.
- تسک **گروهی**: عضو گروه باید اول claim کند؛ complete بدون claim خطا (`NotClaimed`) است. نفر دوم روی claim، `AlreadyClaimed` می‌گیرد.
- Unclaim فقط توسط همان `claimed_by` و فقط از وضعیت `claimed`.

`TransitionTask` فقط این فیلدها را UPDATE می‌کند: `status, note, updated_at, completed_at, claimed_by`.

---

## ۸. ایندکس‌ها

همه `CREATE INDEX IF NOT EXISTS` هستند.

| نام | جدول | ستون‌ها | کاربرد |
|-----|------|---------|--------|
| `tasks_instance_idx` | `tasks` | `(instance_id)` | تسک‌های یک اینستنس ارجاع |
| `tasks_assignee_idx` | `tasks` | `(assignee_kind, assignee_id, status)` | کارتابل شخص/گروه |
| `tasks_parent_idx` | `tasks` | `(parent_instance_id)` | تسک‌های کل درخت یک ریشه |
| `instances_process_idx` | `instances` | `(tenant_id, definition_key, parent_instance_id)` | لیست اجراهای یک processKey (ریشه‌ها با `parent=''`) |
| `instances_initiator_idx` | `instances` | `(tenant_id, started_by, parent_instance_id)` | فرایندهای یک کاربر |

PK روی `id` ایندکس B-tree جدا می‌سازد. ایندکس روی `definitions (tenant_id, key)` نیست؛ حجم کاتالوگ معمولاً کوچک است.

---

## ۹. الگوی کوئری‌ها

| متد `IStore` | SQL مفهومی |
|--------------|------------|
| `GetDefinitionByKey` | `WHERE tenant_id=$1 AND key=$2 ORDER BY created_at DESC LIMIT 1` |
| `GetInstance` | `WHERE id=$1` |
| `UpdateInstance` | `UPDATE … SET status, vars, updated_at WHERE id=$1` |
| `ListRootInstances` | `WHERE tenant_id AND definition_key AND parent_instance_id='' ORDER BY created_at DESC` |
| `ListRootInstancesByInitiator` | `WHERE tenant_id AND started_by AND parent_instance_id='' ORDER BY created_at DESC` |
| `ListChildInstances` | `WHERE parent_instance_id=$1 ORDER BY created_at` |
| `GetTask` | `WHERE id=$1` |
| `TransitionTask` | `SELECT … FOR UPDATE` سپس `UPDATE` داخل یک تراکنش |
| `ListTasks` | فیلتر پویا؛ همیشه `ORDER BY created_at` |

### فیلتر تسک (`TaskFilter`)

شرایط AND می‌شوند. اگر فیلدی خالی باشد اعمال نمی‌شود.

| فیلد فیلتر | شرط SQL |
|------------|---------|
| `TenantId` | `tenant_id = $n` |
| `InstanceId` | `instance_id = $n OR parent_instance_id = $n` |
| `Status` | `status = $n` |
| `Statuses` | `status = ANY($n)` |
| `ClaimedBy` | `claimed_by = $n` |
| `GroupId` | `assignee_kind='group' AND assignee_id=$n` |
| `UserId` بدون گروه | `assignee_kind='user' AND assignee_id=$n` |
| `UserId` + `GroupIds` | شخص **یا** هر کدام از گروه‌هایش |

کارتابل کاربر دو کوئری است که در حافظه ادغام می‌شوند:

1. تسک‌های `open` شخصی + گروهی که عضو است
2. تسک‌های `claimed` که `claimed_by` همان کاربر است

به همین دلیل بعد از claim، تسک از کارتابل بقیهٔ اعضای گروه خارج می‌شود و فقط در کارتابل claimant می‌ماند.

---

## ۱۰. همزمانی

تکمیل، claim و unclaim از `TransitionTask` می‌گذرند:

<div dir="ltr">

```sql
BEGIN;
SELECT … FROM tasks WHERE id=$1 FOR UPDATE;
-- اگر status در allowed نباشد → ErrNotOpen
UPDATE tasks SET status, note, updated_at, completed_at, claimed_by WHERE id=$1;
COMMIT;
```

</div>

`FOR UPDATE` ردیف را تا پایان تراکنش قفل می‌کند. دو complete همزمان روی یک تسک: یکی موفق، دومی `NotOpen`. دو claim همزمان روی تسک گروهی: یکی `claimed`، دومی `AlreadyClaimed`.

`MemoryStore` همین قرارداد را با `lock` روی کل استور شبیه‌سازی می‌کند.

به‌روزرسانی اینستنس قفل سطری ندارد. CompleteAndEnd چند تسک را پشت‌سرهم transition می‌کند؛ اگر یکی وسط کار `NotOpen` شود نادیده گرفته می‌شود (کس دیگری همان لحظه complete کرده).

---

## ۱۱. آنچه در دیتابیس نیست

| موضوع | کجاست |
|--------|--------|
| کاربر، گروه، عضویت | `IDirectory` (متغیرهای `WF_USERS` / `WF_GROUP_*` یا پیاده‌سازی خودتان) |
| کلید API | متغیر `WF_API_KEYS` |
| نشست / رمز عبور | ندارد؛ هویت درخواست = هدر `X-Actor-Id` |
| تاریخچهٔ audit جدا | ندارد؛ وضعیت جاری روی همان ردیف است |
| پیوست فایل | ندارد |
| قید FK / UNIQUE مرکب / CHECK | ندارد |
| Row Level Security | ندارد |

برای گزارش «چه کسی چه زمانی claim کرد» فقط `claimed_by` + `updated_at` همان ردیف را دارید، نه جدول رویداد.

---

## ۱۲. ستون‌های رزرو / میراثی

اسکیما برای مسیر BPMN آینده ستون دارد که موتور فعلی نمی‌خواند و عمداً با پیش‌فرض پر می‌کند:

| جدول | ستون | وضعیت فعلی |
|------|------|------------|
| `definitions` | `version`, `graph`, `published` | INSERT مقدار ثابت می‌گذارد؛ SELECT دامنه آن‌ها را ندارد |
| `instances` | `version` | همیشه `1`؛ قفل خوش‌بینانه روی آن نیست |
| `tasks` | `node_id`, `token_id`, `group_mode`, `return_reason` | در INSERT تسک نمی‌آیند؛ پیش‌فرض جدول `''` است |

اگر مصرف‌کنندهٔ خارجی مستقیم SQL بزند، این ستون‌ها را منبع حقیقت رفتار انجین ندانید.

---

## ۱۳. نگاشت دامنه ↔ جدول

| کلاس دامنه | جدول | تفاوت نام |
|------------|------|-----------|
| `Definition` | `definitions` | `Key` ↔ `key` |
| `ProcessInstance` | `instances` | `Parameters` ↔ `vars` |
| `WorkflowTask` | `tasks` | بقیه هم‌نام |

`IStore` پورت Application است. هیچ لایهٔ ORM نیست؛ Npgsql با پارامتر موقعیتی `$1, $2, …`.

---

## ۱۴. مثال عملی

سناریو: Alice فرایند `purchase` را شروع می‌کند، به گروه `legal` ارجاع می‌دهد، Bob claim و complete می‌کند.

### ۱) Start

`definitions` (اگر نبود):

| id | tenant_id | key | name |
|----|-----------|-----|------|
| `def…` | `default` | `purchase` | `purchase` |

`instances` ریشه:

| id | parent_instance_id | status | started_by | vars |
|----|--------------------|--------|------------|------|
| `root…` | `''` | `running` | `alice` | `{"amount": 150000000}` |

تسک ساخته نمی‌شود. طبقه‌بندی این اجرا: `notStarted`.

### ۲) Refer به گروه legal

`instances` فرزند:

| id | parent_instance_id | status | started_by |
|----|--------------------|--------|------------|
| `ref…` | `root…` | `running` | `alice` |

`tasks`:

| id | instance_id | parent_instance_id | assignee_kind | assignee_id | status | claimed_by |
|----|-------------|--------------------|---------------|-------------|--------|------------|
| `t1…` | `ref…` | `root…` | `group` | `legal` | `open` | `''` |

Bob و Cara هر دو این تسک را در کارتابل می‌بینند (Directory می‌گوید هر دو عضو `legal`اند).

### ۳) Claim توسط Bob

| status | claimed_by |
|--------|------------|
| `claimed` | `bob` |

کارتابل Cara خالی می‌شود. Cara اگر claim کند `409 / AlreadyClaimed`.

### ۴) Complete توسط Bob

تسک: `status=done`، `completed_at` پر، `note` ذخیره می‌شود. اینستنس `ref…` چون تنها تسک‌ش تمام شده → `completed`. ریشه `root…` همچنان `running` می‌ماند مگر CompleteAndEnd صدا شود.

اگر به‌جای گروه، `to.kind=users` با `["alice","bob"]` بود، دو ردیف تسک `user` روی **یک** `instance_id` ساخته می‌شد و `GET …/completion` هر دو را با `allCompleted` گزارش می‌کرد.

---

## ۱۵. DDL مرجع

همان متنی که `PostgresStore` در مهاجرت اجرا می‌کند:

<div dir="ltr">

```sql
CREATE TABLE IF NOT EXISTS definitions (
  id TEXT PRIMARY KEY,
  tenant_id TEXT NOT NULL DEFAULT 'default',
  key TEXT NOT NULL,
  name TEXT NOT NULL DEFAULT '',
  version INT NOT NULL DEFAULT 1,
  graph JSONB NOT NULL DEFAULT '{}'::jsonb,
  published BOOLEAN NOT NULL DEFAULT TRUE,
  created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS instances (
  id TEXT PRIMARY KEY,
  tenant_id TEXT NOT NULL DEFAULT 'default',
  definition_id TEXT NOT NULL,
  definition_key TEXT NOT NULL,
  parent_instance_id TEXT NOT NULL DEFAULT '',
  status TEXT NOT NULL,
  vars JSONB NOT NULL DEFAULT '{}'::jsonb,
  started_by TEXT NOT NULL,
  version INT NOT NULL DEFAULT 1,
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL
);

CREATE TABLE IF NOT EXISTS tasks (
  id TEXT PRIMARY KEY,
  tenant_id TEXT NOT NULL DEFAULT 'default',
  instance_id TEXT NOT NULL,
  parent_instance_id TEXT NOT NULL DEFAULT '',
  definition_key TEXT NOT NULL DEFAULT '',
  node_id TEXT NOT NULL DEFAULT '',
  token_id TEXT NOT NULL DEFAULT '',
  title TEXT NOT NULL DEFAULT '',
  note TEXT NOT NULL DEFAULT '',
  assignee_kind TEXT NOT NULL,
  assignee_id TEXT NOT NULL,
  claimed_by TEXT NOT NULL DEFAULT '',
  assigned_by TEXT NOT NULL DEFAULT '',
  status TEXT NOT NULL,
  group_mode TEXT NOT NULL DEFAULT '',
  return_reason TEXT NOT NULL DEFAULT '',
  created_at TIMESTAMPTZ NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL,
  completed_at TIMESTAMPTZ
);

CREATE INDEX IF NOT EXISTS tasks_instance_idx ON tasks(instance_id);
CREATE INDEX IF NOT EXISTS tasks_assignee_idx ON tasks(assignee_kind, assignee_id, status);
CREATE INDEX IF NOT EXISTS tasks_parent_idx ON tasks(parent_instance_id);
CREATE INDEX IF NOT EXISTS instances_process_idx ON instances(tenant_id, definition_key, parent_instance_id);
CREATE INDEX IF NOT EXISTS instances_initiator_idx ON instances(tenant_id, started_by, parent_instance_id);
```

</div>

---

## ۱۶. عملیات و نگهداری

| موضوع | توصیه |
|-------|--------|
| پشتیبان | volume `pgdata` یا `pg_dump` استاندارد روی دیتابیس `workflow` |
| تست یکپارچه | `PostgresStoreTests` فقط اگر `DATABASE_URL` ست باشد اجرا می‌شود؛ وگرنه silent skip |
| پاک‌سازی تست | تست‌ها `tenant_id` تصادفی (`test-` + ۸ hex) می‌گذارند؛ می‌توانید همان‌ها را DELETE کنید |
| مقیاس کارتابل | ایندکس `tasks_assignee_idx` مسیر اصلی Inbox است |
| مقیاس لیست فرایند | `instances_process_idx` و `instances_initiator_idx` |
| امنیت اتصال | Compose با `sslmode=disable` است؛ در محیط واقعی SSL و رمز جدا |
| رمز پیش‌فرض Compose | فقط توسعه؛ برای تولید عوض شود |

کوئری‌های مفید عملیاتی:

<div dir="ltr">

```sql
-- اجراهای ریشه یک فرایند
SELECT id, status, started_by, created_at
FROM instances
WHERE tenant_id = 'default'
  AND definition_key = 'purchase'
  AND parent_instance_id = '';

-- کارتابل باز یک شخص (بدون عضویت گروه؛ گروه را Directory می‌داند)
SELECT id, title, status, assignee_kind, assignee_id
FROM tasks
WHERE tenant_id = 'default'
  AND status IN ('open', 'claimed')
  AND (
    (assignee_kind = 'user' AND assignee_id = 'bob')
    OR claimed_by = 'bob'
  );

-- درخت یک استارت
SELECT id, parent_instance_id, status, started_by
FROM instances
WHERE id = :root OR parent_instance_id = :root;
```

</div>

---

## ۱۷. ارتباط با بقیهٔ مستندات

| سند | محتوا |
|-----|--------|
| [architecture.md](architecture.md) | لایه‌ها، سرویس‌ها، همزمانی در سطح موتور |
| [usage.md](usage.md) | SDK و REST |
| [README.md](../README.md) | راه‌اندازی سریع |

</div>
