import { defineConfig } from 'vitepress'

export default defineConfig({
  lang: 'fa-IR',
  title: 'TaskFlow',
  titleTemplate: ':title · TaskFlow',
  description: 'مستندات فنی موتور گردش کار TaskFlow — WorkFlowEngine',
  base: '/WorkFlowEngine/',
  cleanUrls: true,
  head: [
    ['link', { rel: 'icon', href: '/WorkFlowEngine/favicon.svg', type: 'image/svg+xml' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.googleapis.com' }],
    ['link', { rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: '' }],
    [
      'link',
      {
        href: 'https://fonts.googleapis.com/css2?family=Vazirmatn:wght@400;500;600;700&display=swap',
        rel: 'stylesheet',
      },
    ],
  ],
  themeConfig: {
    logo: '/favicon.svg',
    nav: [
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
        text: 'مقدمه',
        items: [{ text: 'صفحه اصلی', link: '/' }],
      },
      {
        text: 'راهنمای استفاده',
        link: '/usage',
        items: [
          { text: 'نمای کلی', link: '/usage' },
          { text: 'SDK و میکروسرویس', link: '/usage#sdk-و-میکروسرویس' },
          { text: 'ارجاع موازی', link: '/usage#تخصیص-موازی-و-رفتن-خودکار-به-مرحله-بعد' },
          { text: 'پیکربندی Docker', link: '/usage#۴-پیکربندی-محیط-و-docker' },
        ],
      },
      {
        text: 'معماری',
        link: '/architecture',
        items: [
          { text: 'لایه‌بندی', link: '/architecture#۱-ساختار-لایه‌بندی-پروژه' },
          { text: 'مدل دامنه', link: '/architecture#۲-مدل-دامنه' },
          { text: 'Engine و API', link: '/architecture#۳-engine-و-api' },
          { text: 'API Key و BFF', link: '/architecture#api-key-architecture' },
        ],
      },
      {
        text: 'پایگاه داده',
        link: '/database',
        items: [
          { text: 'ساختار کلی', link: '/database#۱-نقش-و-ساختار-پایگاه-داده' },
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
      label: 'فهرست مطالب',
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
  },
})
