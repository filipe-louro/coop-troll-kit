# coop-troll-kit 🎣💣

Kit para criar **mods de jogos co-op** — benefícios para você e trollagens nos seus amigos — junto com
um **agente de IA** (Claude Code). Em vez de investigar cada jogo do zero, o agente segue um roteiro
padronizado e registra as descobertas num formato fixo, para que o próximo jogo (ou o próximo pedido)
custe pouco.

> ⚠️ **Uso**: partidas **privadas** entre amigos, em jogos **sem anti-cheat**. Não use em matchmaking
> com desconhecidos nem em jogos com EAC/BattlEye/Vanguard — além de estragar o jogo dos outros, dá ban.
> Este repositório não contém nenhum arquivo dos jogos: só o kit, os mods e as descobertas.

## O que faz
- **Comandos de chat que só você vê** (`/imortal`, `/dano 10`, `/ouro 1000`, `/bomba`, `/mob`...) e **teclas**.
- **Patcher genérico**: você descreve os pontos de gancho em JSON; ele injeta as chamadas no
  `Assembly-CSharp.dll` (Unity Mono) com Mono.Cecil, com backup e verificação.
- **Roteiro para o agente** (`.claude/skills/modar-jogo`): reconhecer → varrer → escrever → instalar → registrar.

## Como usar (com Claude Code)
```
cd coop-troll-kit
claude
> quero modar o <jogo>: imortal só pra mim, dano ajustável, dinheiro, e bomba/bicho nos amigos com teclas
```
O agente roda os scripts abaixo e lê só os relatórios.

## Na mão
Requisitos: Windows, PowerShell 7, .NET SDK 8+, `ilspycmd` (`dotnet tool install -g ilspycmd`).
```powershell
pwsh kit/scripts/novo-jogo.ps1 "Muck"     # acha o jogo, detecta engine, cria jogos/muck/, decompila
pwsh kit/scripts/varrer.ps1 muck          # relatório curto: chat, dano, rede, spawn, cheats de dev...
# escreva jogos/muck/ganchos.json e jogos/muck/mod/Mod.cs
pwsh kit/scripts/instalar.ps1 muck        # compila, patcha, instala (espera o jogo fechar se preciso)
pwsh kit/scripts/instalar.ps1 muck -Remover
```

## Jogos documentados
| Jogo | Engine | Status | Destaques |
|---|---|---|---|
| [Muck](jogos/muck/DESCOBERTAS.md) | Unity Mono | ✅ mod completo | vida/inventário por PC; servidor confia no dano do cliente |
| [How to Fish](jogos/how-to-fish/DESCOBERTAS.md) | Unity 6 Mono + FishNet | 📄 descobertas + ganchos | ~25 comandos de dev já existiam, travados por SteamID |
| [SOS OPS](jogos/sos-ops/DESCOBERTAS.md) | Unity 6 IL2CPP | 📄 descobertas | patch por RVA; host não consegue mover o amigo |

## Estrutura
```
kit/Patcher/        patcher genérico (ganchos.json)
kit/ModTemplate/    esqueleto do mod
kit/scripts/        novo-jogo, varrer, instalar
docs/               formato dos ganchos, guia por engine, modelo de DESCOBERTAS
jogos/<jogo>/       DESCOBERTAS.md, ganchos.json, mod/
.claude/skills/     roteiro do agente
```

## Contribuir com um jogo
Abra um PR com `jogos/<jogo>/` contendo `DESCOBERTAS.md` (no formato do modelo), `ganchos.json` e
`mod/`. **Nunca** inclua DLLs do jogo nem código decompilado.

Licença: MIT.
