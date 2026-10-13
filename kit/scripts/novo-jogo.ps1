# Comeca um jogo novo no kit: acha a instalacao, detecta a engine, cria jogos/<slug>/ a partir
# do modelo e (se Unity Mono) decompila o Assembly-CSharp em jogos/<slug>/.src (fora do git).
#
#   pwsh kit/scripts/novo-jogo.ps1 "Muck"
#   pwsh kit/scripts/novo-jogo.ps1 "How to Fish" -Pasta "D:\jogos\How to Fish"
param(
    [Parameter(Mandatory)][string]$Nome,
    [string]$Pasta
)
$ErrorActionPreference = "Stop"
$kit = Resolve-Path "$PSScriptRoot\..\.."
$slug = ($Nome.ToLower() -replace '[^a-z0-9]+', '-').Trim('-')
$dest = "$kit\jogos\$slug"

# 1. acha o jogo: processo aberto > bibliotecas da Steam > Desktop\games
if (-not $Pasta) {
    $p = Get-Process | Where-Object { $_.Path -and $_.ProcessName -like "*$Nome*" } | Select-Object -First 1
    if ($p) { $Pasta = Split-Path $p.Path }
}
if (-not $Pasta) {
    $libs = @("C:\Program Files (x86)\Steam\steamapps")
    $vdf = "C:\Program Files (x86)\Steam\steamapps\libraryfolders.vdf"
    if (Test-Path $vdf) { Select-String $vdf '"path"\s+"(.+)"' | ForEach-Object { $libs += ($_.Matches[0].Groups[1].Value -replace '\\\\', '\') + "\steamapps" } }
    $cands = $libs | ForEach-Object { Get-ChildItem "$_\common" -Directory -ErrorAction SilentlyContinue } |
        Where-Object Name -like "*$Nome*"
    $cands += Get-ChildItem "$env:USERPROFILE\Desktop\games" -Directory -ErrorAction SilentlyContinue | Where-Object Name -like "*$Nome*"
    if ($cands) { $Pasta = $cands[0].FullName }
}
if (-not $Pasta) { throw "Nao achei '$Nome'. Passe -Pasta <pasta do jogo>." }

# 2. engine
$exe = Get-ChildItem $Pasta -Recurse -Depth 2 -Filter *.exe | Where-Object { $_.Name -notmatch 'Crash|Unins|Setup|Redist' } | Select-Object -First 1
$data = Get-ChildItem (Split-Path $exe.FullName) -Directory -Filter "*_Data" | Select-Object -First 1
$engine = "desconhecida"
if ($data -and (Test-Path "$($data.FullName)\Managed\Assembly-CSharp.dll")) { $engine = "unity-mono" }
elseif ($data -and (Test-Path "$($data.FullName)\il2cpp_data")) { $engine = "unity-il2cpp" }
elseif (Get-ChildItem $Pasta -Recurse -Depth 4 -Filter *.pak -ErrorAction SilentlyContinue | Select-Object -First 1) { $engine = "unreal" }
elseif (Get-ChildItem $Pasta -Recurse -Depth 2 -Filter *.pck -ErrorAction SilentlyContinue | Select-Object -First 1) { $engine = "godot" }
$rede = @()
if ($data) {
    $m = "$($data.FullName)\Managed"
    foreach ($par in @(@("FishNet*", "FishNet"), @("Mirror*", "Mirror"), @("Unity.Netcode*", "Netcode for GameObjects"),
                      @("Photon*", "Photon"), @("Facepunch*", "Facepunch/Steam P2P"), @("Riptide*", "Riptide"))) {
        if (Get-ChildItem $m -Filter "$($par[0]).dll" -ErrorAction SilentlyContinue) { $rede += $par[1] }
    }
}
$unity = if ($exe) { (Get-Item (Join-Path (Split-Path $exe.FullName) "UnityPlayer.dll") -ErrorAction SilentlyContinue).VersionInfo.FileVersion } else { "" }
$aviso = Get-ChildItem $Pasta -Recurse -Depth 2 -Include "EasyAntiCheat*", "BattlEye*", "*BEService*" -ErrorAction SilentlyContinue | Select-Object -First 1

Write-Host "jogo:   $Pasta"
Write-Host "engine: $engine $unity"
Write-Host "rede:   $($rede -join ', ')"
if ($aviso) { Write-Host "ANTI-CHEAT detectado ($($aviso.Name)) -- PARE: o kit nao e para jogos com anti-cheat." -ForegroundColor Red; exit 1 }

# 3. pasta do jogo no kit
New-Item -ItemType Directory -Force "$dest\mod" | Out-Null
$mod = ($Nome -replace '[^A-Za-z0-9]', '') + "Mod"
if ($engine -eq "unity-mono") {
    $managed = "$($data.FullName)\Managed"
    if (-not (Test-Path "$managed\Assembly-CSharp.orig.dll")) { Copy-Item "$managed\Assembly-CSharp.dll" "$managed\Assembly-CSharp.orig.dll" }
    if (-not (Test-Path "$dest\mod\Mod.cs")) {
        (Get-Content "$kit\kit\ModTemplate\Mod.cs" -Raw).Replace("__MOD__", $mod).Replace("__JOGO__", $Nome) | Set-Content "$dest\mod\Mod.cs" -Encoding utf8
        (Get-Content "$kit\kit\ModTemplate\Mod.csproj" -Raw).Replace("__MOD__", $mod).Replace("__MANAGED__", $managed) | Set-Content "$dest\mod\$mod.csproj" -Encoding utf8
    }
    if (-not (Test-Path "$dest\ganchos.json")) {
@"
{
  // $Nome -- preencha os ganchos (veja docs/ganchos.md)
  "managed": "$($managed -replace '\\','/')",
  "modDll": "mod/bin/Release/net472/$mod.dll",
  "modClasse": "$mod.Mod",
  "saida": "pronto/Assembly-CSharp.dll",
  "ganchos": [
  ]
}
"@ | Set-Content "$dest\ganchos.json" -Encoding utf8
    }
    if (-not (Test-Path "$dest\.src")) {
        Write-Host "decompilando (1-2 min)..."
        New-Item -ItemType Directory -Force "$dest\.src" | Out-Null
        ilspycmd -p -o "$dest\.src" "$managed\Assembly-CSharp.orig.dll" *> $null
    }
}
if (-not (Test-Path "$dest\DESCOBERTAS.md")) {
    (Get-Content "$kit\docs\DESCOBERTAS-modelo.md" -Raw).Replace("__JOGO__", $Nome).Replace("__ENGINE__", "$engine $unity").
        Replace("__REDE__", ($rede -join ', ')).Replace("__PASTA__", $Pasta).Replace("__DATA__", (Get-Date -f 'yyyy-MM-dd')) |
        Set-Content "$dest\DESCOBERTAS.md" -Encoding utf8
}
Write-Host "pronto: $dest"
if ($engine -eq "unity-mono") { Write-Host "proximo: pwsh kit/scripts/varrer.ps1 $slug" }
else { Write-Host "engine ${engine}: o caminho Mono nao se aplica -- veja docs/engines.md" }

