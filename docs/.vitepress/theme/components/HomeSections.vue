<script setup lang="ts">
const steps = [
  {
    num: '۱',
    title: 'مفاهیم پایه',
    desc: 'processKey، instanceId، تسک و کارتابل را بشناسید.',
    link: '/usage#۱-مفاهیم-پایه',
    time: '۵ دقیقه',
  },
  {
    num: '۲',
    title: 'اجرای سرور',
    desc: 'TaskFlow.Server را محلی یا با Docker بالا بیاورید.',
    link: '/getting-started#اجرای-سرور',
    time: '۱۰ دقیقه',
  },
  {
    num: '۳',
    title: 'SDK در C#',
    desc: 'میکروسرویس‌ها با TaskFlow.Client به سرور مشترک وصل می‌شوند.',
    link: '/usage#۲-استفاده-از-طریق-sdk-در-زبان-c',
    time: '۲۰ دقیقه',
  },
  {
    num: '۴',
    title: 'ارجاع موازی',
    desc: 'onAllCompleted و join خودکار بعد از تکمیل همه.',
    link: '/usage#تخصیص-موازی-و-رفتن-خودکار-به-مرحله-بعد',
    time: '۱۵ دقیقه',
  },
]

const docs = [
  {
    icon: '📘',
    title: 'راهنمای استفاده',
    desc: 'API، SDK، Docker، سناریوهای عملی و نمونه‌کد',
    link: '/usage',
    tag: 'اصلی',
  },
  {
    icon: '🏗️',
    title: 'معماری سیستم',
    desc: 'لایه‌ها، Engine، امنیت BFF و کلید API',
    link: '/architecture',
    tag: 'معماری',
  },
  {
    icon: '🐘',
    title: 'پایگاه داده',
    desc: 'اسکیما Postgres، مهاجرت و کوئری‌ها',
    link: '/database',
    tag: 'دیتابیس',
  },
  {
    icon: '⚡',
    title: 'شروع سریع',
    desc: 'از صفر تا اولین فرایند در چند دستور',
    link: '/getting-started',
    tag: 'جدید',
  },
]

const apis = [
  { method: 'POST', path: '/v1/processes/start', label: 'شروع فرایند' },
  { method: 'POST', path: '/v1/assignments', label: 'ارجاع کار' },
  { method: 'GET', path: '/v1/tasks?user=…', label: 'کارتابل' },
  { method: 'POST', path: '/v1/tasks/{id}/complete', label: 'تکمیل' },
]
</script>

<template>
  <div class="tf-home">
    <section class="tf-section">
      <div class="tf-section-head">
        <span class="tf-eyebrow">مسیر یادگیری</span>
        <h2>از صفر تا استقرار در چهار گام</h2>
        <p>هر مرحله به بخش مرتبط در مستندات لینک دارد — بدون پرش تصادفی بین صفحات.</p>
      </div>
      <div class="tf-steps">
        <a v-for="step in steps" :key="step.num" :href="step.link" class="tf-step">
          <div class="tf-step-num">{{ step.num }}</div>
          <div class="tf-step-body">
            <div class="tf-step-meta">
              <h3>{{ step.title }}</h3>
              <span class="tf-badge">{{ step.time }}</span>
            </div>
            <p>{{ step.desc }}</p>
          </div>
          <span class="tf-step-arrow" aria-hidden="true">←</span>
        </a>
      </div>
    </section>

    <section class="tf-section">
      <div class="tf-section-head">
        <span class="tf-eyebrow">مستندات</span>
        <h2>همهٔ موضوعات در یک نگاه</h2>
      </div>
      <div class="tf-cards">
        <a v-for="doc in docs" :key="doc.link" :href="doc.link" class="tf-card">
          <div class="tf-card-top">
            <span class="tf-card-icon">{{ doc.icon }}</span>
            <span class="tf-tag">{{ doc.tag }}</span>
          </div>
          <h3>{{ doc.title }}</h3>
          <p>{{ doc.desc }}</p>
        </a>
      </div>
    </section>

    <section class="tf-section tf-split">
      <div class="tf-panel tf-panel-code">
        <span class="tf-eyebrow">اجرای محلی</span>
        <h2>دو دستور، آمادهٔ تست</h2>
        <pre dir="ltr"><code># سرور
dotnet run --project src/TaskFlow.Server

# تست‌ها
dotnet test</code></pre>
        <p class="tf-hint">Swagger: <code dir="ltr">http://127.0.0.1:8081/swagger</code></p>
      </div>
      <div class="tf-panel tf-panel-api">
        <span class="tf-eyebrow">REST API</span>
        <h2>نقطه‌های ورودی اصلی</h2>
        <ul class="tf-api-list">
          <li v-for="api in apis" :key="api.path">
            <span class="tf-method" :data-m="api.method">{{ api.method }}</span>
            <code dir="ltr">{{ api.path }}</code>
            <span class="tf-api-label">{{ api.label }}</span>
          </li>
        </ul>
        <a href="/usage#۳-راهنمای-وب‌سرویس-rest-api" class="tf-link">مشاهدهٔ همهٔ endpointها ←</a>
      </div>
    </section>

    <section class="tf-cta">
      <div class="tf-cta-inner">
        <div>
          <h2>آماده‌اید اولین فرایند را بسازید؟</h2>
          <p>با راهنمای شروع سریع در کمتر از ۱۵ دقیقه یک Start → AssignTo → Complete کامل کنید.</p>
        </div>
        <div class="tf-cta-actions">
          <a href="/getting-started" class="tf-btn tf-btn-primary">شروع سریع</a>
          <a href="https://github.com/mortenaho/WorkFlowEngine" class="tf-btn tf-btn-ghost" target="_blank" rel="noreferrer">مخزن GitHub</a>
        </div>
      </div>
    </section>
  </div>
</template>
