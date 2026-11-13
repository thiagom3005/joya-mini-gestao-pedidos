import { LucideProvider } from 'lucide-react'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { App } from './App.tsx'
import './estilos.css'

const raiz = document.getElementById('raiz')
if (raiz === null) {
  throw new Error('Elemento #raiz não encontrado em index.html.')
}

// Tamanho padrão dos ícones (escala 16/20/24): os de 16 e 24 informam `size` no próprio uso.
createRoot(raiz).render(
  <StrictMode>
    <LucideProvider size={20}>
      <App />
    </LucideProvider>
  </StrictMode>,
)
