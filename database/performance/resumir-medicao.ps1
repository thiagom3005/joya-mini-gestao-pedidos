# performance/resumir-medicao.ps1 — resume as saídas do medir-ranking.sql (spec §9.6). Windows PowerShell 5.1 ou
# PowerShell 7. Não acessa banco: só lê os arquivos de saída do sqlcmd.
#
#   ./resumir-medicao.ps1 -Arquivos medicao-A.txt, medicao-B.txt, ... [-PastaPlanos pasta] [-Planos]
#
# Para cada passada (arquivo) e variante (marcador "### ROTULO | parte"): soma as leituras lógicas por tabela
# ("Table 'X'. ... logical reads N") e toma o tempo do bloco "SQL Server Execution Times" de maior tempo decorrido
# (a instrução medida; em sp_executesql, o bloco externo, que inclui o interno). A mediana das 3 rodadas vale para a
# passada; o consolidado é a mediana das medianas das passadas, com a faixa (mínimo..máximo) entre elas.
# Com -Planos, lista os operadores do plano real (linhas estimadas × reais) da primeira passada e, com -PastaPlanos,
# grava cada plano como .sqlplan.
param(
    [Parameter(Mandatory)] [string[]] $Arquivos,
    [string] $PastaPlanos,
    [switch] $Planos
)
$ErrorActionPreference = 'Stop'

function Ler-Secoes([string] $arquivo) {
    $secoes = [ordered]@{}
    $atual = $null
    foreach ($linha in [IO.File]::ReadAllLines((Resolve-Path $arquivo), [Text.Encoding]::UTF8)) {
        if ($linha -match '^### (.+?) \| (.+)$') { $atual = "$($matches[1])|$($matches[2])"; $secoes[$atual] = New-Object System.Collections.Generic.List[string]; continue }
        if ($linha -match '^### ') { $atual = $null; continue }
        if ($atual) { $secoes[$atual].Add($linha) }
    }
    $secoes
}

function Medir-Rodada($linhas) {
    $leituras = @{}
    $blocos = @()
    for ($i = 0; $i -lt $linhas.Count; $i++) {
        if ($linhas[$i] -match "^Table '([^']+)'\. Scan count \d+, logical reads (\d+)") {
            if (-not $leituras.ContainsKey($matches[1])) { $leituras[$matches[1]] = 0 }
            $leituras[$matches[1]] += [int]$matches[2]
        }
        if ($linhas[$i] -match 'SQL Server Execution Times:' -and $i + 1 -lt $linhas.Count -and
            $linhas[$i + 1] -match 'CPU time = (\d+) ms,\s+elapsed time = (\d+) ms') {
            $blocos += , @([int]$matches[1], [int]$matches[2])
        }
    }
    $maior = $blocos | Sort-Object { $_[1] } -Descending | Select-Object -First 1
    [pscustomobject]@{ Leituras = $leituras; Cpu = $maior[0]; Decorrido = $maior[1] }
}

function Mediana([double[]] $valores) { $ordenados = $valores | Sort-Object; $ordenados[[int][math]::Floor($ordenados.Count / 2)] }

$porVariante = [ordered]@{}
foreach ($arquivo in $Arquivos) {
    $secoes = Ler-Secoes $arquivo
    $rotulos = $secoes.Keys | ForEach-Object { $_.Split('|')[0] } | Select-Object -Unique
    foreach ($rotulo in $rotulos) {
        $rodadas = 1..3 | ForEach-Object { Medir-Rodada $secoes["$rotulo|rodada $_"] }
        if (-not $porVariante.Contains($rotulo)) { $porVariante[$rotulo] = @() }
        $porVariante[$rotulo] += [pscustomobject]@{
            Passada   = [IO.Path]::GetFileNameWithoutExtension($arquivo)
            Rodadas   = ($rodadas | ForEach-Object { "$($_.Cpu)/$($_.Decorrido)" }) -join ' '
            Cpu       = Mediana ($rodadas | ForEach-Object { $_.Cpu })
            Decorrido = Mediana ($rodadas | ForEach-Object { $_.Decorrido })
            Leituras  = (($rodadas[0].Leituras.Keys | Sort-Object | ForEach-Object {
                            $tabela = $_; ($rodadas | ForEach-Object { $_.Leituras[$tabela] } | Select-Object -Unique) -join '/' | ForEach-Object { "$tabela=$_" } }) -join ' ')
            Total     = ($rodadas[0].Leituras.Values | Measure-Object -Sum).Sum
        }
    }
}

'Rodadas por passada (CPU/decorrido em ms, 3 rodadas medidas):'
foreach ($rotulo in $porVariante.Keys) {
    '{0,-58} {1}' -f $rotulo, (($porVariante[$rotulo] | ForEach-Object { "$($_.Passada): $($_.Rodadas)" }) -join ' | ')
}
''
'Consolidado (mediana das medianas das passadas; faixa = mínimo..máximo das medianas):'
$porVariante.Keys | ForEach-Object {
    $dados = $porVariante[$_]
    [pscustomobject]@{
        Variante    = $_
        CpuMs       = Mediana $dados.Cpu
        CpuFaixa    = '{0}..{1}' -f ($dados.Cpu | Measure-Object -Minimum).Minimum, ($dados.Cpu | Measure-Object -Maximum).Maximum
        DecorridoMs = Mediana $dados.Decorrido
        DecFaixa    = '{0}..{1}' -f ($dados.Decorrido | Measure-Object -Minimum).Minimum, ($dados.Decorrido | Measure-Object -Maximum).Maximum
        Leituras    = ($dados.Leituras | Select-Object -Unique) -join ' ; '
        Total       = ($dados.Total | Select-Object -Unique) -join ','
    }
} | Format-Table -AutoSize | Out-String -Width 400

if (-not $Planos) { return }

$secoes = Ler-Secoes $Arquivos[0]
$rotulos = $secoes.Keys | ForEach-Object { $_.Split('|')[0] } | Select-Object -Unique
if ($PastaPlanos) { New-Item -ItemType Directory -Force $PastaPlanos | Out-Null }
$ns = @{ s = 'http://schemas.microsoft.com/sqlserver/2004/07/showplan' }
foreach ($rotulo in $rotulos) {
    $xml = $secoes["$rotulo|plano real"] | Where-Object { $_ -match '^\s*<ShowPlanXML' -and $_ -match 'ItensPedido' } | Select-Object -Last 1
    if (-not $xml) { "SEM PLANO: $rotulo"; continue }
    $xml = $xml.Trim()
    if ($PastaPlanos) {
        $nome = ($rotulo -replace '[^A-Za-z0-9]+', '-').Trim('-').ToLowerInvariant() + '.sqlplan'
        [IO.File]::WriteAllText((Join-Path (Resolve-Path $PastaPlanos) $nome), $xml, (New-Object Text.UTF8Encoding $false))
    }
    [xml] $plano = $xml
    $consulta = (Select-Xml -Xml $plano -Namespace $ns -XPath '//s:QueryPlan' | Select-Object -First 1).Node
    $memoria = $consulta.MemoryGrantInfo
    "==== $rotulo"
    '  DOP={0} memoria concedida={1} KB feedback="{2}" compilacao={3} ms' -f $consulta.DegreeOfParallelism, $memoria.GrantedMemory, $memoria.IsMemoryGrantFeedbackAdjusted, $consulta.CompileTime
    foreach ($parametro in (Select-Xml -Xml $plano -Namespace $ns -XPath '//s:ParameterList/s:ColumnReference')) {
        '  parametro {0} {1} compilado={2}' -f $parametro.Node.Column, $parametro.Node.ParameterDataType, $parametro.Node.ParameterCompiledValue
    }
    foreach ($aviso in (Select-Xml -Xml $plano -Namespace $ns -XPath '//s:Warnings/*')) { '  AVISO: ' + $aviso.Node.LocalName }
    foreach ($no in (Select-Xml -Xml $plano -Namespace $ns -XPath '//s:RelOp')) {
        $op = $no.Node
        $reais = 0; $lidas = 0; $temLidas = $false; $modo = ''
        foreach ($t in $op.RunTimeInformation.RunTimeCountersPerThread) {
            $reais += [double]$t.ActualRows
            if ($t.ActualRowsRead) { $lidas += [double]$t.ActualRowsRead; $temLidas = $true }
            $modo = $t.ActualExecutionMode
        }
        $objeto = $op.ChildNodes | ForEach-Object { $_.ChildNodes } | Where-Object { $_.LocalName -eq 'Object' } | Select-Object -First 1
        '  [{0,2}] {1,-22} {2,-22} {3,-5} est={4,10:N1} real={5,9}{6} {7}' -f $op.NodeId, $op.PhysicalOp, $op.LogicalOp, $modo,
            [double]$op.EstimateRows, $reais, $(if ($temLidas) { " lidas=$lidas" } else { '' }),
            $(if ($objeto) { "$($objeto.Table).$($objeto.Index)" } else { '' })
    }
}
