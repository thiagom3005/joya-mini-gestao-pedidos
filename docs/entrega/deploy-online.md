# Deploy online: avaliação de viabilidade

Avaliação da E12 (14/11/2026). **Nenhum deploy foi feito**, nenhum serviço foi contratado e nenhum passo abaixo foi
executado. Preços e franquias vêm de pesquisa em 14/11/2026 e precisam ser conferidos no site de cada provedor antes de
qualquer decisão; fontes no fim.

## Requisitos

A aplicação completa precisa de: frontend React servido pelo nginx, API .NET 10, SQL Server compatível com os scripts
de `database/`, banco persistente, HTTPS, credenciais só em segredos (nunca a senha padrão de desenvolvimento), nenhuma
porta SQL pública, criação automática do esquema e do seed, e uma URL pública para a avaliação.

Restrições que pesam em qualquer opção:

- **Imagem e memória**: a imagem do SQL Server roda só em Linux x86-64 e precisa de no mínimo 2 GB de memória.
- **Licença**: a edição Developer, usada no compose, é licenciada só para desenvolvimento e teste. Num ambiente público
  seria preciso a Express (gratuita em produção; até 10 GB por banco e limites de CPU e memória, suficientes aqui).
- **Sem autenticação** (fora do escopo, PRE-01): numa URL pública, qualquer pessoa cria pedidos e consome estoque.
- **Scripts**: `02` e `03` usam `USE JoyaPedidos` e o `01` usa `CREATE DATABASE`, que o Azure SQL Database não aceita
  como estão.

## Alternativas

| Opção | Custo (a conferir) | Compatibilidade | Segurança | Persistência | Tempo | Risco para a entrega |
|---|---|---|---|---|---|---|
| **A. VPS x86-64 com Docker Compose** (ex.: 2 vCPU, 4 GB) e proxy HTTPS (Caddy) | Pago. Em fontes de terceiros, cerca de € 6 por mês para 4 GB, com disponibilidade instável desse plano; planos regulares em torno de € 20 | Total: mesmas imagens, mesmos scripts e o mesmo `db-init`; só um arquivo de override | Firewall só 22/80/443, SQL sem porta, senha forte num `.env` só no servidor; a administração do servidor é sua | Volume Docker; backup é responsabilidade sua | 1 a 2 h | Baixo a médio |
| **B. Azure Container Apps + Azure SQL Database (oferta gratuita, serverless)** | Pode ficar em zero dentro das franquias mensais (Container Apps: 180 mil vCPU-segundos; Azure SQL: 100 mil vCore-segundos e 32 GB por banco), mas exige assinatura com cartão e configuração cuidadosa | Parcial: scripts precisam de versão sem `USE` e `CREATE DATABASE`; o `db-init` vira um job | HTTPS e segredos nativos; o Azure SQL tem endpoint público por padrão (fechá-lo exige rede privada, com custo) | Gerenciada | 3 a 5 h | Médio a alto |
| **C. AWS (App Runner ou ECS) + RDS for SQL Server Express** | O nível gratuito do RDS para SQL Server Express (750 h por mês, 12 meses) vale só para contas criadas antes de 15/07/2025; fora disso, cobrança por hora | Boa (Express); `db-init` vira tarefa | Rede privada (VPC) e segredos nativos, com mais peças para configurar | Gerenciada | 3 a 5 h | Médio a alto |
| **D. PaaS genérico (Render, Railway, Fly.io) com SQL Server em container** | Pago (máquina com 2 GB ou mais e volume) | Boa, com o mesmo container | Rede privada varia por provedor | Volume do provedor | 2 a 4 h | Médio |

Não há, nesta pesquisa, uma opção comprovadamente gratuita que rode SQL Server compatível com os scripts sem
adaptação.

## Recomendação

**Não publicar antes do envio.** O prazo é 15/11/2026, 16h, o diferencial é opcional e toda opção gera custo, exige
adaptar scripts ou abre a aplicação sem autenticação para escrita pública. O risco para a entrega supera o benefício.

Se o candidato decidir publicar mesmo assim, a opção **A** é a de menor risco: reaproveita o compose validado, não
muda código e pode ser desligada logo depois da avaliação. Ela depende de autorização para contratar o servidor.

## Passos da opção A (não executados)

1. **Ação do candidato**: contratar uma VPS Linux x86-64 (Ubuntu 24.04, 4 GB de RAM ou mais) e, se quiser, um domínio.
2. No servidor: acesso SSH só por chave, firewall liberando apenas 22, 80 e 443, atualizações automáticas e Docker
   Engine com o plugin Compose.
3. Clonar o repositório (público) e criar um `.env` **só no servidor**, com `MSSQL_SA_PASSWORD` gerada aleatoriamente
   (nunca `Joya_Dev_2026_Inseguro`; sem `$`, aspas ou `;`).
4. Criar um override de produção, sem segredos, com:
   - `sqlserver`: `MSSQL_PID: Express`;
   - `api` e `web`: sem portas publicadas (`ports: !reset []`);
   - um serviço `caddy` com imagem fixada, publicando 80 e 443, com `reverse_proxy web:80` para o domínio (HTTPS
     automático) e volume para os certificados.
5. Subir com projeto próprio: `docker compose -p joya-prod -f docker-compose.yml -f <override> up --build -d`. O
   `db-init` cria o esquema e o seed como no ambiente local.
6. Validar: `https://<domínio>/` com a listagem, `https://<domínio>/api/health` com 200, criação de um pedido, e
   `docker compose -p joya-prod ps` sem porta 1433 publicada.
7. Operação: decidir como conter escritas públicas (por exemplo, autenticação básica no Caddy só durante a avaliação),
   fazer backup do volume e desligar o servidor depois da avaliação. Um reset do seed só com
   `docker compose -p joya-prod down -v`.
8. Melhorias para produção real, fora do escopo atual: login de banco de privilégio mínimo para a API em vez do `sa`
   (RSK-10) e autenticação na aplicação.

## Fontes

- [Azure SQL Database: oferta gratuita (Microsoft Learn)](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer)
- [Azure Container Apps: preços](https://azure.microsoft.com/en-us/pricing/details/container-apps/)
- [Diferenças de T-SQL do Azure SQL Database (Microsoft Learn)](https://learn.microsoft.com/en-us/azure/azure-sql/database/transact-sql-tsql-differences-sql-server?view=azuresql)
- [RDS for SQL Server: preços](https://aws.amazon.com/rds/sqlserver/pricing/) e [nível gratuito do RDS](https://aws.amazon.com/rds/free/)
- [Requisitos do SQL Server no Linux (Microsoft Learn)](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-setup)
- [Edições do SQL Server 2022 (Microsoft Learn)](https://learn.microsoft.com/en-us/sql/sql-server/editions-and-components-of-sql-server-2022)
- [Preços da Hetzner Cloud em 2026 (fonte de terceiros)](https://www.bitdoze.com/hetzner-cloud-cost-optimized-plans/)
