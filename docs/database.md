<div dir="rtl">

# مستندات پایگاه داده (Database Documentation)

مرجع اصلی ساختار و اسکیمای پایگاه داده، کلاس `PostgresStore` در مسیر `src/WorkflowEngine.Infrastructure/Persistence/PostgresStore.cs` است. عملیات مهاجرت (Migration) به‌صورت خودکار در نخستین اتصال انجام می‌پذیرد و نیازی به ابزار مهاجرت خارجی یا فایل‌های مجزای SQL وجود ندارد.

این مستند ساختار جداول، ستون‌ها، شاخص‌ها (Indexes)، الگوهای کوئری و محدودیت‌های عملیاتی را به تفصیل تشریح می‌کند.

---

## ۱. نقش و ساختار پایگاه داده

این سیستم نیازی به ذخیره‌سازی دیاگرام‌های پیچیدهٔ گرافیکی ندارد و در Postgres صرفاً سه موجودیت اصلی پایدار نگهداری می‌شوند:

| موجودیت | نام جدول | مفهوم و نقش کسب‌وکاری |
|---------|----------|------------------------|
| تعریف فرایند | `definitions` | تعریف نوع فرایند (مانند `purchase` یا `employeeTermination`) |
| نمونهٔ اجرا | `instances` | رکورد یک اجرای مستقل (شروع فرایند یا ثبت ارجاع) |
| وظیفهٔ کارتابل | `tasks` | وظیفهٔ ارجاع‌شده به شخص یا گروه |

اطلاعات هویتی کاربران و عضویت در گروه‌ها در دیتابیس ذخیره نمی‌شوند؛ این داده‌ها از طریق رابط `IDirectory` (پیاده‌سازی `StaticDirectory` یا سرویس‌های سازمانی مثل LDAP/SSO) تأمین می‌گردند.

دو پیاده‌سازی برای رابط `IStore` فراهم شده است:

| پیاده‌سازی | شرط فعال‌سازی | ماندگاری داده‌ها |
|------------|---------------|------------------|
| `PostgresStore` | تنظیم بودن متغیر `DATABASE_URL` | دائمی و پایدار |
| `MemoryStore` | خالی بودن متغیر `DATABASE_URL` | موقت در حافظه (با توقف برنامه پاک می‌شود) |

در زمان راه‌اندازی سرور (`Program.cs`) پیاده‌سازی مناسب به‌صورت خودکار انتخاب می‌شود؛ لایهٔ منطق کسب‌وکار (`Engine`) هیچ وابستگی مستقیمی به نوع ذخیره‌ساز ندارد.

---

## ۲. نحوهٔ اتصال و مهاجرت خودکار

### رشتهٔ اتصال (Connection String)

متغیر محیطی:

<div dir="ltr">

```
DATABASE_URL=postgres://workflow:workflow@postgres:5432/workflow?sslmode=disable
```

</div>

تنظیمات پیش‌فرض سرویس در Docker Compose:

| پارامتر | مقدار در Docker Compose |
|---------|--------------------------|
| تصویر دیتابیس | `postgres:16-alpine` |
| نام کاربری | `workflow` |
| کلمهٔ عبور | `workflow` |
| نام پایگاه داده | `workflow` |
| پورت سیستم میزبان | `5432` |
| حجم ذخیره‌سازی (Volume) | `pgdata` |

در صورت عدم دسترسی اولیه به Postgres در هنگام شروع سرویس، سرور تا ۳۰ بار با فواصل ۱ ثانیه‌ای تلاش مجدد (Retry) انجام می‌دهد.

### سازوکار مهاجرت خودکار (Migration)

متد `PostgresStore.Open(dsn)` در اولین اجرا بلافاصله متد `Migrate` را فراخوانی می‌کند:

۱. اجرای دستورات `CREATE TABLE IF NOT EXISTS` و `CREATE INDEX IF NOT EXISTS` برای ساخت جداول و شاخص‌ها.  
۲. اجرای دستورات `ALTER TABLE … ADD COLUMN IF NOT EXISTS` برای اطمینان از وجود ستون‌های الحاقی در نسخه‌های ارتقایافته.  

این عملیات کاملاً پایا (Idempotent) بوده و اجرای مکرر آن روی دیتابیس خالی یا پایگاه‌های دادهٔ قدیمی بدون ریسک است. دستورات DDL در قالب یک تراکنش واحد سراسری اجرا نمی‌شوند؛ بلکه هر بخش با یک دستور `ExecuteNonQuery` مستقل به اجرا درمی‌آید.

---

## ۳. مدل مفهومی و روابط موجودیت‌ها

<div dir="ltr">

```
سازمان (tenant_id)
  └── definition          (یک نوع فرایند؛ مثلاً purchase)
        └── instance ریشه  (خروجی متد Start با parent_instance_id = '')
              └── instance فرزند  (هر عملیات Refer یک ردیف جدید)
                    └── task(ها)  (وظایف کارتابل user یا group)
```

</div>

**قواعد ساختاری مهم:**

- متد `Start` هیچ تسکی ایجاد نمی‌کند؛ صرفاً تعریف فرایند (در صورت نبود) و یک نمونهٔ اجرای ریشه می‌سازد.
- هر عملیات `Refer` یک نمونهٔ اجرای **جدید** ایجاد می‌کند؛ در صورت ارسال `parentInstanceId`، این نمونه به ریشه متصل می‌گردد.
- وظایف (`tasks`) مستقیماً به نمونهٔ اجرای **ارجاع** متصل هستند.
- جهت دریافت کلیهٔ وظایف یک فرایند، کوئری جستجو هر دو فیلد `instance_id` و `parent_instance_id` را بررسی می‌کند.

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

**نکته:** به‌منظور حفظ سادگی و کارایی، قید کلید خارجی فیزیکی (FOREIGN KEY) در سطح پایگاه داده تعریف نشده است و یکپارچگی ارجاعی در سطح لایهٔ `Application` تضمین می‌گردد.

---

## ۴. شناسه‌ها، زمان، و جداسازی سازمان‌ها

### فرمت شناسه‌ها (Identifiers)

شناسه‌ها توسط متد `Ids.New()` با تولید ۱۶ بایت تصادفی امن و تبدیل آن به رشتهٔ هگزادسیمال کوچک ۳۲ کاراکتری تولید می‌شوند. کلیهٔ کلیدهای اصلی (`definitions.id`، `instances.id` و `tasks.id`) از این نوع هستند و از توالی‌های عددی (Sequence) یا نوع UUID بومی دیتابیس استفاده نمی‌شود.

### مدیریت زمان (Timestamps)

تمام ستون‌های زمانی از نوع `TIMESTAMPTZ` بوده و بر پایهٔ ساعت هماهنگ جهانی (UTC) ذخیره و بازیابی می‌شوند.

### جداسازی داده‌های سازمانی (`tenant_id`)

هر سه جدول دارای ستون `tenant_id TEXT NOT NULL DEFAULT 'default'` هستند:

- مقادیر خالی یا تهی به‌طور خودکار به `'default'` تبدیل می‌شوند (`Tenant.Normalize`).
- در لایهٔ REST از طریق هدر `X-Tenant-Id` دریافت می‌گردد.
- تفکیک داده‌ها در سطح لایهٔ منطق برنامه (Application) مدیریت می‌شود.
- در صورت عدم تطابق سازمان در زمان دریافت اطلاعات، خطای `ForbiddenTenant` صادر می‌شود.
- ایندکس‌های جستجو شامل فیلد `tenant_id` هستند تا سرعت بازیابی بهینه باشد.

---

## ۵. جدول تعاریف فرایند (`definitions`)

کاتالوگ انواع فرایندها در این جدول نگهداری می‌شود.

| نام ستون | نوع داده | مقدار پیش‌فرض | توضیحات و کاربرد |
|----------|----------|---------------|-------------------|
| `id` | `TEXT` PK | — | شناسهٔ یکتای تعریف |
| `tenant_id` | `TEXT NOT NULL` | `'default'` | شناسهٔ سازمان |
| `key` | `TEXT NOT NULL` | — | کلید شناسهٔ فرایند (مانند `purchase`) |
| `name` | `TEXT NOT NULL` | `''` | نام نمایشی فرایند (در صورت خالی بودن برابر `key` تنظیم می‌شود) |
| `version` | `INT NOT NULL` | `1` | شمارهٔ نسخه (رزرو برای توسعه‌های آتی) |
| `graph` | `JSONB NOT NULL` | `'{}'` | دیاگرام ساختار فرایند (رزرو برای توسعه‌های آتی) |
| `published` | `BOOLEAN NOT NULL` | `TRUE` | وضعیت انتشار فرایند |
| `created_at` | `TIMESTAMPTZ NOT NULL` | `NOW()` | زمان ایجاد رکورد |

### دستور ثبت (Insert / Update)

<div dir="ltr">

```sql
INSERT INTO definitions (id, tenant_id, key, name, version, graph, published, created_at)
VALUES ($1, $2, $3, $4, 1, '{}'::jsonb, TRUE, $5)
ON CONFLICT (id) DO UPDATE SET name = EXCLUDED.name;
```

</div>

---

## ۶. جدول نمونه‌های اجرا (`instances`)

هر ردیف نمایندهٔ یک نمونهٔ اجرایی (شروع ریشه یا یک ارجاع جدید) است.

| نام ستون | نوع داده | مقدار پیش‌فرض | توضیحات و کاربرد |
|----------|----------|---------------|-------------------|
| `id` | `TEXT` PK | — | شناسهٔ نمونهٔ اجرا (`instanceId`) |
| `tenant_id` | `TEXT NOT NULL` | `'default'` | شناسهٔ سازمان |
| `definition_id` | `TEXT NOT NULL` | — | شناسهٔ تعریف فرایند مربوطه |
| `definition_key` | `TEXT NOT NULL` | — | کلید تعریف فرایند جهت بهینه‌سازی کوئری‌ها بدون نیاز به JOIN |
| `parent_instance_id` | `TEXT NOT NULL` | `''` | شناسهٔ نمونهٔ ریشه (برای نمونه‌های ریشه برابر رشتهٔ خالی است) |
| `status` | `TEXT NOT NULL` | — | وضعیت اجرا (`running` یا `completed`) |
| `vars` | `JSONB NOT NULL` | `'{}'` | پارامترها و متغیرهای فرایند به‌صورت JSON |
| `started_by` | `TEXT NOT NULL` | — | شناسهٔ ایجادکننده یا ارجاع‌دهنده |
| `version` | `INT NOT NULL` | `1` | شمارهٔ نسخهٔ رکورد |
| `created_at` | `TIMESTAMPTZ NOT NULL` | — | زمان شروع نمونهٔ اجرا |
| `updated_at` | `TIMESTAMPTZ NOT NULL` | — | زمان آخرین به‌روزرسانی |

### وضعیت‌های اجرا (`status`)

- `running`: فرایند در حال اجراست و کارهای ناتمام دارد.
- `completed`: کلیهٔ وظایف ارجاع مربوطه یا کل درخت فرایند خاتمه یافته است.

### ساختار درختی والد و فرزند

- نمونهٔ ریشه: مقدار `parent_instance_id` برابر رشتهٔ خالی `''` است.
- نمونهٔ ارجاع: مقدار `parent_instance_id` برابر با شناسهٔ نمونهٔ ریشه است.
- عملیات `CompleteAndEnd` با یافتن ریشه، تمام وظایف باز زیرمجموعه را لغو (`cancelled`) کرده و وضعیت تمام نمونه‌های زیرمجموعه را به `completed` تغییر می‌دهد.

---

## ۷. جدول وظایف کارتابل (`tasks`)

هر ردیف نشان‌دهندهٔ یک وظیفهٔ اختصاص‌یافته به کاربر یا گروه است.

| نام ستون | نوع داده | مقدار پیش‌فرض | توضیحات و کاربرد |
|----------|----------|---------------|-------------------|
| `id` | `TEXT` PK | — | شناسهٔ یکتای وظیفه (`taskId`) |
| `tenant_id` | `TEXT NOT NULL` | `'default'` | شناسهٔ سازمان |
| `instance_id` | `TEXT NOT NULL` | — | شناسهٔ نمونهٔ ارجاع وابسته |
| `parent_instance_id` | `TEXT NOT NULL` | `''` | شناسهٔ نمونهٔ ریشه جهت فیلتر آسان کل وظایف فرایند |
| `definition_key` | `TEXT NOT NULL` | `''` | کلید تعریف فرایند |
| `node_id` | `TEXT NOT NULL` | `''` | شناسهٔ گره در گراف (رزرو) |
| `token_id` | `TEXT NOT NULL` | `''` | شناسهٔ توکن اجرایی (رزرو) |
| `title` | `TEXT NOT NULL` | `''` | عنوان ارجاع و موضوع کار |
| `note` | `TEXT NOT NULL` | `''` | توضیحات یا یادداشت ثبت‌شده هنگام تکمیل |
| `assignee_kind` | `TEXT NOT NULL` | — | نوع انتساب (`user` یا `group`) |
| `assignee_id` | `TEXT NOT NULL` | — | شناسهٔ کاربر یا گروه منتسب |
| `claimed_by` | `TEXT NOT NULL` | `''` | شناسهٔ کاربری که وظیفه را به خود اختصاص داده است |
| `assigned_by` | `TEXT NOT NULL` | `''` | شناسهٔ کاربر ارجاع‌دهنده |
| `status` | `TEXT NOT NULL` | — | وضعیت وظیفه (`open`, `claimed`, `done`, `cancelled`) |
| `group_mode` | `TEXT NOT NULL` | `''` | حالت گروهی (رزرو) |
| `return_reason` | `TEXT NOT NULL` | `''` | دلیل برگشت کار (رزرو) |
| `created_at` | `TIMESTAMPTZ NOT NULL` | — | زمان تخصیص وظیفه |
| `updated_at` | `TIMESTAMPTZ NOT NULL` | — | زمان آخرین تغییر وضعیت |
| `completed_at` | `TIMESTAMPTZ` nullable | `NULL` | زمان دقیق تکمیل وظیفه |

### چرخهٔ حیات وضعیت وظیفه (`status`)

<div dir="ltr">

```
        claim                 complete
  open ──────► claimed ──────► done
    ▲             │
    └─ unclaim ───┘

  open / claimed ──completeAndEnd──► cancelled
```

</div>

| وضعیت | ثابت معادل در کد | توضیحات |
|-------|-------------------|----------|
| `open` | `TaskStatus.Open` | وظیفه تازه ایجاد شده و هنوز در حال انتظار است |
| `claimed` | `TaskStatus.Claimed` | وظیفهٔ گروهی توسط یک عضو رزرو و تحویل گرفته شده است |
| `done` | `TaskStatus.Done` | وظیفه با موفقیت انجام و تکمیل شده است |
| `cancelled` | `TaskStatus.Cancelled` | وظیفه با دستور پایان فرایند لغو گردیده است |

---

## ۸. شاخص‌های پایگاه داده (Indexes)

تمامی شاخص‌ها به‌صورت `CREATE INDEX IF NOT EXISTS` ایجاد می‌گردند:

| نام شاخص | جدول | ستون‌ها | هدف و کاربرد |
|-----------|------|---------|---------------|
| `tasks_instance_idx` | `tasks` | `(instance_id)` | بازیابی سریع وظایف متعلق به یک ارجاع |
| `tasks_assignee_idx` | `tasks` | `(assignee_kind, assignee_id, status)` | بهینه‌سازی کوئری‌های کارتابل وظایف کاربر و گروه |
| `tasks_parent_idx` | `tasks` | `(parent_instance_id)` | بازیابی وظایف مرتبط با کل درخت یک فرایند |
| `instances_process_idx` | `instances` | `(tenant_id, definition_key, parent_instance_id)` | دریافت فهرست اجراهای ریشه برای یک نوع فرایند |
| `instances_initiator_idx` | `instances` | `(tenant_id, started_by, parent_instance_id)` | دریافت فهرست فرایندهای آغازشده توسط کاربر |

---

## ۹. الگوهای کوئری و متدهای لایهٔ داده

| متد در رابط `IStore` | ساختار کوئری معادل SQL |
|----------------------|------------------------|
| `GetDefinitionByKey` | `WHERE tenant_id=$1 AND key=$2 ORDER BY created_at DESC LIMIT 1` |
| `GetInstance` | `WHERE id=$1` |
| `UpdateInstance` | `UPDATE instances SET status=$2, vars=$3::jsonb, updated_at=$4 WHERE id=$1` |
| `ListRootInstances` | `WHERE tenant_id=$1 AND definition_key=$2 AND parent_instance_id='' ORDER BY created_at DESC` |
| `ListRootInstancesByInitiator` | `WHERE tenant_id=$1 AND started_by=$2 AND parent_instance_id='' ORDER BY created_at DESC` |
| `ListChildInstances` | `WHERE parent_instance_id=$1 ORDER BY created_at` |
| `GetTask` | `WHERE id=$1` |
| `TransitionTask` | قفل سطری با `SELECT … FOR UPDATE` و سپس `UPDATE` در تراکنش واحد |
| `ListTasks` | اعمال فیلترهای پویا با ترتیب `ORDER BY created_at` |

---

## ۱۰. مدیریت همزمانی در پایگاه داده

عملیات تغییر وضعیت تسک‌ها (شامل تکمیل، رزرو و لغو رزرو) از طریق متد `TransitionTask` انجام می‌شود:

<div dir="ltr">

```sql
BEGIN;
SELECT id, status, ... FROM tasks WHERE id = $1 FOR UPDATE;
-- در صورت عدم تطابق وضعیت فعلی با وضعیت‌های مجاز، خطای NotOpen بازگردانده می‌شود
UPDATE tasks SET status = $2, note = $3, updated_at = $4, completed_at = $5, claimed_by = $6 WHERE id = $1;
COMMIT;
```

</div>

دستور `FOR UPDATE` رکورد وظیفه را تا پایان تراکنش قفل می‌کند تا از پدیدهٔ Race Condition در اقدامات هم‌زمان جلوگیری شود.

---

## ۱۱. مواردی که در دیتابیس ذخیره نمی‌شوند

- **احراز هویت و جداول کاربران:** از طریق رابط `IDirectory` تأمین می‌گردد.
- **کلیدهای امنیتی API:** از متغیرهای محیطی (`WF_API_KEYS`) خوانده می‌شود.
- **جدول وقایع تاریخی (Audit Log):** تاریخچهٔ گام‌به‌گام ذخیره نمی‌شود و صرفاً آخرین وضعیت در رکورد تسک (`claimed_by`, `updated_at`, `completed_at`) موجود است.
- **پیوست‌ها و فایل‌ها:** دیتابیس صرفاً فراداده‌های متنی و JSON را نگهداری می‌کند.

---

## ۱۲. سناریوی عملی تغییرات داده‌ها

**سناریو:** کاربر Alice فرایند `purchase` را شروع کرده و کار را به گروه `legal` ارجاع می‌دهد؛ سپس Bob وظیفه را تحویل گرفته و تکمیل می‌کند.

### ۱. شروع فرایند (Start)

یک رکورد در جدول `instances` درج می‌شود (تسک ساخته نمی‌شود و وضعیت مفهومی اجرا `notStarted` است):

| id | parent_instance_id | status | started_by | vars |
|----|--------------------|--------|------------|------|
| `root_1` | `''` | `running` | `alice` | `{"amount": 150000000}` |

### ۲. ارجاع کار به گروه حقوقی (Refer)

یک رکورد فرزند در جدول `instances` و یک رکورد تسک در جدول `tasks` ایجاد می‌شود:

| جدول | id | instance_id | parent_instance_id | assignee_kind | assignee_id | status |
|------|----|-------------|--------------------|---------------|-------------|--------|
| `instances` | `ref_1` | — | `root_1` | — | — | `running` |
| `tasks` | `task_1` | `ref_1` | `root_1` | `group` | `legal` | `open` |

کلیهٔ اعضای گروه حقوقی (مثلاً Bob و Cara) تسک را در کارتابل خود مشاهده می‌کنند.

### ۳. تحویل گرفتن وظیفه توسط Bob (Claim)

فیلد `status` به `claimed` و `claimed_by` به `bob` تغییر می‌یابد. تسک از کارتابل Cara خارج شده و چنانچه وی تلاش به کلیم کند، خطای `409 Conflict` دریافت خواهد کرد.

### ۴. تکمیل وظیفه توسط Bob (Complete)

وضعیت تسک به `done` تغییر کرده و مقدار `completed_at` ثبت می‌شود. نمونهٔ اجرای ارجاع (`ref_1`) به دلیل تکمیل تمام وظایفش به وضعیت `completed` منتقل می‌گردد؛ در حالی که نمونهٔ ریشه (`root_1`) تا زمان دستور پایان همچنان در وضعیت `running` باقی می‌ماند.

---

## ۱۳. راهنمای عملیات و نگهداری

| موضوع | توصیه و دستورالعمل |
|-------|-------------------|
| تهیهٔ نسخهٔ پشتیبان (Backup) | تهیهٔ پشتیبان از Volume مربوط به `pgdata` یا اجرای استاندارد دستور `pg_dump` |
| تست‌های یکپارچگی پایگاه داده | کلاس `PostgresStoreTests` در صورت تنظیم متغیر `DATABASE_URL` اجرا می‌شود |
| امنیت ارتباطات پایگاه داده | در محیط‌های تولیدی (Production) فعال‌سازی رمزنگاری TLS/SSL و استفاده از کلمات عبور پیچیده الزامی است |

کوئری‌های کاربردی برای پایش عملیاتی:

<div dir="ltr">

```sql
-- دریافت نمونه‌های اجرای ریشه برای یک نوع فرایند مشخص
SELECT id, status, started_by, created_at
FROM instances
WHERE tenant_id = 'default'
  AND definition_key = 'purchase'
  AND parent_instance_id = '';

-- دریافت وظایف باز یک کاربر (شامل وظایف فردی و وظایف رزروشده)
SELECT id, title, status, assignee_kind, assignee_id
FROM tasks
WHERE tenant_id = 'default'
  AND status IN ('open', 'claimed')
  AND (
    (assignee_kind = 'user' AND assignee_id = 'bob')
    OR claimed_by = 'bob'
  );

-- مشاهدهٔ ساختار درختی یک فرایند (ریشه و تمام ارجاع‌های فرزند)
SELECT id, parent_instance_id, status, started_by, created_at
FROM instances
WHERE id = :root_id OR parent_instance_id = :root_id;
```

</div>

---

## ۱۴. پیوند با سایر مستندات

- [معماری سیستم](architecture.md)
- [راهنمای جامع استفاده و API](usage.md)
- [راهنمای راه‌اندازی سریع](../README.md)

</div>
