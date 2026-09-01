import { defineConfig } from 'vitepress'

export default defineConfig({
  lang: 'fa-IR',
  title: 'TaskFlow',
  titleTemplate: ':title · TaskFlow',
  description: 'مستندات آموزشی موتور گردش کار TaskFlow — WorkFlowEngine',
  base: '/WorkFlowEngine/',
  cleanUrls: true,
  head: [
    ['link', { rel: 'icon', href: '/WorkFlowEngine/favicon.svg', type: 'image/svg+xml' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.googleapis.com' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' }],
    [
      'link',
      {
        href: 'https://fonts.googleapis.com/css2?family=Vazirmatn:wght@400;500;600;700;800&display=swap',
        rel: 'stylesheet',
      },
    ],
    ['meta', { name: 'theme-color', content: '#4F46E5' }],
  ],
  themeConfig: {
    logo: '/favicon.svg',
    siteTitle: 'TaskFlow Docs',
    nav: [
      { text: 'شروع سریع', link: '/getting-started' },
      { text: 'راهنما', link: '/usage' },
      { text: 'معماری', link: '/architecture' },
      { text: 'پایگاه داده', link: '/database' },
      {
        text: 'GitHub',
        link: 'https://github.com/mortenaho/WorkFlowEngine',
      },
    ],
    sidebar: [
      {
        text: 'آموزش',
        collapsed: false,
        items: [
          { text: 'صفحه اصلی', link: '/' },
          { text: 'شروع سریع', link: '/getting-started' },
        ],
      },
      {
        text: 'راهنمای استفاده',
        collapsed: false,
        link: '/usage',
        items: [
          { text: 'نمای کلی', link: '/usage' },
          { text: 'مفاهیم پایه', link: '/usage#۱-مفاهیم-پایه' },
          { text: 'SDK و میکروسرویس', link: '/usage#۲-استفاده-از-طریق-sdk-در-زبان-c' },
          { text: 'ارجاع موازی', link: '/usage#تخصیص-موازی-و-رفتن-خودکار-به-مرحله-بعد' },
          { text: 'REST API', link: '/usage#۳-راهنمای-وب‌سرویس-rest-api' },
          { text: 'Docker و env', link: '/usage#۴-پیکربندی-محیط-و-docker' },
        ],
      },
      {
        text: 'معماری',
        collapsed: false,
        link: '/architecture',
        items: [
          { text: 'لایه‌بندی', link: '/architecture#۱-ساختار-لایه‌بندی-پروژه' },
          { text: 'مدل دامنه', link: '/architecture#۲-مدل-مفهومی-گردش-کار' },
          { text: 'Engine و API', link: '/architecture#۴-رفتار-سرویس‌ها-و-جریان‌های-کاری' },
          { text: 'API Key و BFF', link: '/architecture#۶-معماری-پیشنهادی-استقرار-react-بک‌اند-و-کلید-api' },
        ],
      },
      {
        text: 'پایگاه داده',
        collapsed: false,
        link: '/database',
        items: [
          { text: 'ساختار کلی', link: '/database#۱-نقش-و-ساختار-پایگاه-داده' },
          { text: 'اتصال و مهاجرت', link: '/database#۲-نحوهٔ-اتصال-و-مهاجرت-خودکار' },
          { text: 'جداول', link: '/database#۳-مدل-مفهومی-و-روابط-موجودیت‌ها' },
          { text: 'سناریوی عملی', link: '/database#۱۲-سناریوی-عملی-تغییرات-داده‌ها' },
        ],
      },
    ],
    socialLinks: [
      { icon: 'github', link: 'https://github.com/mortenaho/WorkFlowEngine' },
    ],
    footer: {
      message: 'TaskFlow — موتور گردش کار WorkFlowEngine',
      copyright: 'Copyright © mortenaho',
    },
    docFooter: {
      prev: 'قبلی',
      next: 'بعدی',
    },
    outline: {
      label: 'در این صفحه',
      level: [2, 3],
    },
    search: {
      provider: 'local',
      options: {
        translations: {
          button: {
            buttonText: 'جستجو',
            buttonAriaLabel: 'جستجو در مستندات',
          },
          modal: {
            displayDetails: 'نمایش جزئیات',
            resetButtonTitle: 'پاک کردن',
            backButtonTitle: 'بازگشت',
            noResultsText: 'نتیجه‌ای یافت نشد',
            footer: {
              selectKey: 'انتخاب',
              selectText: 'برو',
              navigateUpKey: 'بالا',
              navigateDownKey: 'پایین',
              closeKey: 'بستن',
            },
          },
        },
      },
    },
  },
  markdown: {
    theme: {
      light: 'github-light',
      dark: 'github-dark',
    },
    mermaid: {
      theme: 'base',
      themeVariables: {
        fontFamily: 'Vazirmatn, Tahoma, sans-serif',
        fontSize: '14px',
        primaryColor: '#eef2ff',
        primaryBorderColor: '#6366f1',
        primaryTextColor: '#1e1b4b',
        secondaryColor: '#ecfeff',
        secondaryBorderColor: '#06b6d4',
        tertiaryColor: '#fef3c7',
        tertiaryBorderColor: '#f59e0b',
        lineColor: '#64748b',
        textColor: '#1e293b',
        mainBkg: '#eef2ff',
        nodeBorder: '#6366f1',
        clusterBkg: '#f8fafc',
        titleColor: '#1e293b',
        edgeLabelBackground: '#ffffff',
      },
      flowchart: {
        curve: 'basis',
        padding: 20,
        nodeSpacing: 50,
        rankSpacing: 60,
        htmlLabels: true,
      },
      sequence: {
        diagramMarginX: 20,
        diagramMarginY: 20,
        actorMargin: 60,
        width: 180,
        height: 50,
        boxMargin: 10,
        boxTextMargin: 5,
        noteMargin: 10,
        messageMargin: 40,
      },
    },
  },
})
