import DefaultTheme from 'vitepress/theme'
import { onMounted, watch, nextTick } from 'vue'
import { useRoute } from 'vitepress'
import mermaid from 'mermaid'
import CustomLayout from './components/CustomLayout.vue'
import './custom.css'

mermaid.initialize({
  startOnLoad: false,
  theme: 'base',
  securityLevel: 'loose',
  fontFamily: 'Vazirmatn, Tahoma, sans-serif',
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
})

let renderCounter = 0

async function renderMermaidDiagrams() {
  const blocks = document.querySelectorAll<HTMLElement>(
    '.VPDoc pre code.language-mermaid, .VPDoc .language-mermaid code',
  )

  for (const block of blocks) {
    const pre = block.closest('pre')
    const wrapper = pre?.closest('.language-mermaid') as HTMLElement | null
    if (!pre || !wrapper || wrapper.dataset.mermaidDone === 'true') continue

    const source = block.textContent?.trim() ?? ''
    if (!source) continue

    wrapper.dataset.mermaidDone = 'true'
    const id = `mermaid-diagram-${++renderCounter}`

    try {
      const { svg } = await mermaid.render(id, source)
      const container = document.createElement('div')
      container.className = 'mermaid'
      container.innerHTML = svg
      wrapper.replaceWith(container)
    } catch (err) {
      console.error('[TaskFlow docs] Mermaid render failed:', err)
      wrapper.dataset.mermaidDone = 'error'
    }
  }
}

export default {
  extends: DefaultTheme,
  Layout: CustomLayout,
  setup() {
    const route = useRoute()

    onMounted(() => {
      watch(
        () => route.path,
        () => nextTick(() => renderMermaidDiagrams()),
        { immediate: true },
      )
    })
  },
}
