#!/bin/bash
# Aplica, em ordem lexical, os scripts *.sql da raiz de /database (spec §4.6). Subpastas nunca são aplicadas.
# Usado pelo serviço db-init do Docker Compose e pelos testes de integração (mesmo mecanismo nos dois).
#
# Uso:   aplicar-scripts.sh <servidor>          (ex.: sqlserver no compose, localhost dentro do container de teste)
# Senha: lida pelo sqlcmd da variável de ambiente SQLCMDPASSWORD, para nunca aparecer na linha de comando.
#
# sqlcmd: -C confia no certificado autoassinado do container; -b encerra com erro no primeiro erro de SQL;
#         -f 65001 lê os scripts como UTF-8 (nomes com acento).
set -euo pipefail

servidor="${1:?informe o servidor, ex.: sqlserver}"
: "${SQLCMDPASSWORD:?defina SQLCMDPASSWORD com a senha do sa}"

shopt -s nullglob
scripts=(/database/*.sql)
if [ ${#scripts[@]} -eq 0 ]; then
  echo "Nenhum script .sql encontrado em /database." >&2
  exit 1
fi

for script in "${scripts[@]}"; do
  echo "Aplicando ${script##*/}..."
  /opt/mssql-tools18/bin/sqlcmd -C -b -f 65001 -S "$servidor" -U sa -i "$script"
done

echo "Scripts aplicados com sucesso (${#scripts[@]} arquivo(s))."
