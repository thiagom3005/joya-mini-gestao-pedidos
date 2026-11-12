---
paths:
  - "frontend/**"
---

# Regras do frontend (React + TypeScript + Vite)

Referência: spec §6.

- TypeScript `strict`; sem `any` implícito ou explícito. Tipos dos contratos espelham a spec §5 em `src/tipos/`.
- Página única, sem router e sem biblioteca de estado global ou de componentes. Estado local com hooks.
- Todas as chamadas passam pelo cliente HTTP tipado em `src/api/`, com caminhos relativos `/api/...`.
  Decisões de erro usam o `codigo` do Problem Details, nunca o texto da mensagem.
- Datas exibidas com `Intl.DateTimeFormat('pt-BR', { timeZone: 'America/Sao_Paulo', ... })`; moeda com
  `Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })`.
- O total do modal é **estimativa** em centavos inteiros; o valor oficial vem da API.
- Modal com `<dialog>` nativo; botão de envio desabilitado durante a requisição.
- Acessibilidade: todo campo com `<label>`; erros com `role="alert"`; sucesso com `aria-live="polite"`.
  Seletores de teste preferem papéis e rótulos acessíveis.
- Nomes de componentes, funções e variáveis em pt-BR, sem acento.
