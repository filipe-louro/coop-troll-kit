# Compila o mod, gera o Assembly-CSharp patchado (via ganchos.json) e instala.
# Se o jogo estiver aberto, deixa um processo esperando ele fechar e instala sozinho.
#
#   pwsh kit/scripts/instalar.ps1 muck            # compila + patcha + instala
#   pwsh kit/scripts/instalar.ps1 muck -Remover   # volta ao original
param([Parameter(Mandatory)][string]$Slug, [switch]$Remover)
$ErrorActionPreference = "Stop"
$kit = Resolve-Path "$PSScriptRoot\..\.."
$dir = "$kit\jogos\$Slug"
$cfgTxt = (Get-Content "$dir\ganchos.json" -Raw) -replace '(?m)^\s*//.*$', ''
$cfg = $cfgTxt | ConvertFrom-Json
$M = $cfg.managed -replace '/', '\'
$proc = (Get-ChildItem (Split-Path (Split-Path $M)) -Filter *.exe | Where-Object Name -notmatch 'Crash' | Select-Object -First 1).BaseName
$modNome = [IO.Path]::GetFileName($cfg.modDll)

if ($Remover) {
    if (Get-Process $proc -ErrorAction SilentlyContinue) { throw "Feche o jogo antes." }
    Copy-Item "$M\Assembly-CSharp.orig.dll" "$M\Assembly-CSharp.dll" -Force
    Remove-Item "$M\$modNome" -ErrorAction SilentlyContinue
    "Original restaurado."; return
}

# update da Steam deixa o .orig velho: se o Assembly atual nao e o nosso patch, ele e o novo original
$pronto = "$dir\pronto\Assembly-CSharp.dll"
if ((Test-Path $pronto) -and (Test-Path "$M\Assembly-CSharp.orig.dll") -and
    (Get-FileHash "$M\Assembly-CSharp.dll").Hash -ne (Get-FileHash $pronto).Hash -and
    (Get-FileHash "$M\Assembly-CSharp.dll").Hash -ne (Get-FileHash "$M\Assembly-CSharp.orig.dll").Hash) {
    Write-Host "Assembly-CSharp mudou (update?) -- renovando o .orig"
    Copy-Item "$M\Assembly-CSharp.dll" "$M\Assembly-CSharp.orig.dll" -Force
}

New-Item -ItemType Directory -Force "$dir\pronto" | Out-Null
dotnet build (Get-ChildItem "$dir\mod" -Filter *.csproj)[0].FullName -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "mod nao compilou" }
dotnet run --project "$kit\kit\Patcher" -c Release -- "$dir\ganchos.json"
if ($LASTEXITCODE -ne 0) { throw "patch falhou -- nada foi trocado" }
Copy-Item (Join-Path $dir $cfg.modDll) "$dir\pronto\$modNome" -Force

$copia = {
    param($dir, $M, $modNome)
    Copy-Item "$dir\pronto\$modNome" "$M\$modNome" -Force -ErrorAction Stop
    Copy-Item "$dir\pronto\Assembly-CSharp.dll" "$M\Assembly-CSharp.dll" -Force -ErrorAction Stop
}
try {
    & $copia $dir $M $modNome
    "Instalado. Reinicie o jogo se ele estava aberto."
} catch {
    # DLL do mod ja carregada = travada. Espera o jogo fechar num processo separado.
    $script = @"
while (Get-Process '$proc' -ErrorAction SilentlyContinue) { Start-Sleep 3 }
Start-Sleep 2
Copy-Item '$dir\pronto\$modNome' '$M\$modNome' -Force
Copy-Item '$dir\pronto\Assembly-CSharp.dll' '$M\Assembly-CSharp.dll' -Force
"`$(Get-Date -f 'dd/MM HH:mm:ss') instalado" | Add-Content '$dir\pronto\instalacao.log'
"@
    $tmp = "$dir\pronto\instalar_ao_fechar.ps1"
    $script | Set-Content $tmp -Encoding utf8
    Start-Process pwsh -WindowStyle Hidden -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$tmp`""
    "Jogo aberto: a nova versao entra sozinha quando voce fechar o jogo (log em pronto\instalacao.log)."
}
