import DefaultTheme from 'vitepress/theme'
import { onMounted, watch, nextTick } from 'vue'
import { useRoute } from 'vitepress'
import mermaid from 'mermaid'
import './custom.css'

mermaid.initialize({
  startOnLoad: false,
  theme: 'base',
  securityLevel: 'loose',
  fontFamily: 'Vazirmatn, Tahoma, sans-serif',
  themeVariables: {
    fontFamily: 'Vazirmatn, Tahoma, sans-serif',
    fontSize: '14px',
    primaryColor: '#e8f4ff',
    primaryBorderColor: '#3DADFF',
    primaryTextColor: '#1a1a2e',
    secondaryColor: '#e8f5e9',
    secondaryBorderColor: '#66D575',
    tertiaryColor: '#fff8e1',
    tertiaryBorderColor: '#FFC943',
    lineColor: '#5f6368',
    textColor: '#1a1a2e',
    mainBkg: '#e8f4ff',
    nodeBorder: '#3DADFF',
    clusterBkg: '#f8f9fa',
    titleColor: '#1a1a2e',
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
  ...DefaultTheme,
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
