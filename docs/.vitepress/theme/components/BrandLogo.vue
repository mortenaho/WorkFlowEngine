<script setup lang="ts">
import { withBase } from 'vitepress'
</script>

<template>
  <a class="tf-brand" :href="withBase('/')" aria-label="TaskFlow — صفحهٔ اصلی مستندات">
    <img class="tf-brand-mark" :src="withBase('/logo-mark.svg')" alt="" width="36" height="36" />
    <span class="tf-brand-text">
      <span class="tf-brand-name">TaskFlow</span>
      <span class="tf-brand-badge">Docs</span>
    </span>
  </a>
</template>

<style scoped>
.tf-brand {
  display: inline-flex;
  align-items: center;
  gap: 10px;
  text-decoration: none;
  color: inherit;
  padding: 4px 0;
  transition: opacity 0.2s;
}

.tf-brand:hover {
  opacity: 0.9;
}

.tf-brand-mark {
  flex-shrink: 0;
  display: block;
  border-radius: 10px;
  box-shadow: 0 2px 12px rgba(79, 70, 229, 0.25);
}

.tf-brand-text {
  display: flex;
  align-items: center;
  gap: 8px;
  direction: ltr; /* brand name stays LTR */
}

.tf-brand-name {
  font-size: 1.15rem;
  font-weight: 800;
  letter-spacing: -0.03em;
  background: linear-gradient(135deg, var(--vp-c-brand-1) 0%, var(--tf-accent, #06b6d4) 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.tf-brand-badge {
  font-size: 0.65rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  padding: 3px 8px;
  border-radius: 6px;
  background: var(--vp-c-brand-soft);
  color: var(--vp-c-brand-1);
  border: 1px solid color-mix(in srgb, var(--vp-c-brand-1) 25%, transparent);
}
</style>
