<script setup lang="ts">
import DefaultTheme from 'vitepress/theme'
import { useData } from 'vitepress'
import HomeSections from './HomeSections.vue'
import BrandLogo from './BrandLogo.vue'

const { frontmatter } = useData()
const Layout = DefaultTheme.Layout
</script>

<template>
  <Layout>
    <template #nav-bar-title-before>
      <BrandLogo />
    </template>

    <template v-if="frontmatter.layout === 'home'" #home-features-after>
      <HomeSections />
    </template>
  </Layout>
</template>
