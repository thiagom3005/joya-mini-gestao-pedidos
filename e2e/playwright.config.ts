import { defineConfig, devices } from '@playwright/test'
import { URL_BASE } from './apoio/ambiente.ts'

/**
 * E2E da spec §10.5 contra o stack do Docker Compose (projeto isolado `joya-e12`).
 * - `api-real`: navegador, nginx, API e SQL Server reais; nenhuma requisição é interceptada.
 * - `simulado`: o catálogo vem da API real, mas o POST /api/pedidos é interceptado (page.route) para provar
 *   comportamentos do frontend que dependem de tempo ou de falhas; nada é gravado no banco.
 */
export default defineConfig({
  testDir: './testes',
  globalSetup: './apoio/preparacao-global.ts',
  // Os testes compartilham o banco do ambiente isolado e comparam a tela com a API: execução em série.
  fullyParallel: false,
  workers: 1,
  // Sem novas tentativas: uma falha intermitente precisa aparecer, não ser mascarada.
  retries: 0,
  forbidOnly: Boolean(process.env['CI']),
  timeout: 30_000,
  expect: { timeout: 10_000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: URL_BASE,
    locale: 'pt-BR',
    timezoneId: 'America/Sao_Paulo',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'api-real', testDir: './testes/api-real', use: { ...devices['Desktop Chrome'] } },
    { name: 'simulado', testDir: './testes/simulado', use: { ...devices['Desktop Chrome'] } },
  ],
})
