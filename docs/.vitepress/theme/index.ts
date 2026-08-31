import DefaultTheme from 'vitepress/theme'
import { onMounted, watch, nextTick } from 'vue'
import { useRoute } from 'vitepress'
import './custom.css'

export default {
  ...DefaultTheme,
  setup() {
    const route = useRoute()

    onMounted(() => {
      watch(
        () => route.path,
        () => nextTick(() => styleMermaidBlocks()),
        { immediate: true },
      )
    })
  },
}

/** Add visual polish after VitePress renders mermaid SVGs. */
function styleMermaidBlocks() {
  document.querySelectorAll('.VPDoc .language-mermaid').forEach((el) => {
    if (el.querySelector('svg')) {
      el.classList.add('mermaid-rendered')
    }
  })
}
