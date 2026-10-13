# Varre o codigo decompilado (jogos/<slug>/.src) atras dos pontos que TODO mod de trollagem
# precisa e grava um relatorio curto em jogos/<slug>/varredura.md. Le o relatorio, nao o codigo.
#
#   pwsh kit/scripts/varrer.ps1 muck
param([Parameter(Mandatory)][string]$Slug, [int]$Max = 12)
$kit = Resolve-Path "$PSScriptRoot\..\.."
$src = "$kit\jogos\$Slug\.src"
if (-not (Test-Path $src)) { throw "rode antes: novo-jogo.ps1 (falta $src)" }
$arqs = Get-ChildItem $src -Recurse -Filter *.cs

# categoria -> regex. Ordem = ordem de prioridade na investigacao.
$cats = [ordered]@{
    "1. Cheats de dev ja existentes (interruptor pronto = menos trabalho)" = '(?i)\b(cheat|godmode|god_mode|devmode|isdev|debugmode|noclip|console|commands?)\w*\b'
    "2. Chat e comandos (onde pendurar /comandos)"                          = '(?i)(void \w*(ChatCommand|SendMessage|SendChat|IsServerCommand|OnSubmit)\w*\(|StartsWith\("/"\)|\[0\] == ''/'')'
    "3. Dano no jogador local (imortal/resistencia)"                       = '(?i)void (Take|Handle|Deal|Apply)Damage\w*\(|void (Damage|Hit|Hurt)\(int'
    "4. Dano causado (multiplicador)"                                      = '(?i)(void \w*(DamageMob|HitPlayer|HitCreature|MeleeAttack|PlayerHit)\w*\(|ServerRpc.*\n.*Damage)'
    "5. Rede: quem decide (Server*/Rpc/SyncVar)"                           = '(?i)(\[ServerRpc|\[ObserversRpc|\[TargetRpc|\[ClientRpc|\[Command\]|SyncVar<|NetworkVariable<|class Server(Send|Handle)\b)'
    "6. Jogadores (lista, local, nome)"                                    = '(?i)(static .*List<\w*Player\w*>|static .*Dictionary<int, ?\w*Player\w*>|static \w*Player\w* (LocalPlayer|Instance|localPlayer)|SteamName|username)'
    "7. Spawn de inimigos/itens"                                           = '(?i)void \w*(Spawn|Drop)\w*(Mob|Creature|Enemy|Item|Powerup|AtPosition)\w*\('
    "8. Dinheiro/recursos"                                                 = '(?i)\b(money|coins?|gold|currency|cash)\b'
    "9. Update sempre ativo (para Tick/teclas)"                            = '(?i)private void Update\(\)'
    "10. Explosao / teleporte / knockback"                                 = '(?i)void \w*(Explode|Explosion|Teleport|Knockback|Launch)\w*\('
}
$out = @("# Varredura -- $Slug", "", "Gerado por kit/scripts/varrer.ps1 em $(Get-Date -f 'yyyy-MM-dd HH:mm'). Ate $Max hits por categoria.", "")
foreach ($c in $cats.Keys) {
    $hits = $arqs | Select-String -Pattern $cats[$c] | Select-Object -First $Max
    $out += "## $c", ""
    if (-not $hits) { $out += "_(nada)_", ""; continue }
    foreach ($h in $hits) { $out += ('- `{0}:{1}` {2}' -f $h.Filename, $h.LineNumber, ($h.Line.Trim() -replace '\s+', ' ').Substring(0, [Math]::Min(140, $h.Line.Trim().Length))) }
    $out += ""
}
$rel = "$kit\jogos\$Slug\varredura.md"
$out | Set-Content $rel -Encoding utf8
Write-Host "relatorio: $rel ($((Get-Content $rel).Count) linhas)"
