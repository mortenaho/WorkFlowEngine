<script setup lang="ts">
import DefaultTheme from 'vitepress/theme'
import { useData } from 'vitepress'
import HomeSections from './HomeSections.vue'

const { frontmatter } = useData()
const Layout = DefaultTheme.Layout
</script>

<template>
  <Layout>
    <template v-if="frontmatter.layout === 'home'" #home-features-after>
      <HomeSections />
    </template>
  </Layout>
</template>
