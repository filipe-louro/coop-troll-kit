---
name: modar-jogo
description: Use quando o usuario pedir mod, cheat, trollagem de amigos, imortal, dano, dinheiro, spawn ou comandos de chat em um jogo co-op. Roteiro padronizado do coop-troll-kit (Unity Mono via ganchos.json; IL2CPP/Unreal/Godot via docs/engines.md).
---

# Modar um jogo co-op (roteiro do kit)

Objetivo: chegar ao mod funcionando **lendo o mínimo de código**. Cada passo tem uma saída curta;
leia a saída, não o código inteiro.

## 0. Antes de tudo
- Já existe `jogos/<slug>/DESCOBERTAS.md`? **Leia primeiro** — pode não precisar investigar nada.
- Jogo com anti-cheat (EAC, BattlEye, Vanguard) ou matchmaking com desconhecidos: **pare** e diga
  que o kit é só para partidas privadas entre amigos.

## 1. Reconhecer (1 comando)
`pwsh kit/scripts/novo-jogo.ps1 "<nome>"` → engine, rede, pasta `jogos/<slug>/` com modelo, `.orig`
e código decompilado em `.src/` (fora do git). Engine ≠ unity-mono → `docs/engines.md`.

## 2. Varrer (1 comando)
`pwsh kit/scripts/varrer.ps1 <slug>` → `jogos/<slug>/varredura.md` (~100 linhas, 10 categorias).
Leia o relatório e abra **só** os arquivos/linhas citados. Ordem de prioridade:
1. **Cheats de dev já existentes** — destravar é mais barato que escrever (ex.: How to Fish).
2. **Chat** — onde o `/comando` é tratado localmente (vira gancho `interceptar`).
3. **Autoridade** — para cada estado (vida, dano, inventário, inimigos, movimento): cada PC ou servidor?
   Isso decide o que é "só para você" (local) e o que exige ser host (pacote do servidor).
4. Dano recebido / dano causado / `Update` sempre ativo / spawn / dinheiro.

## 3. Escrever
- `jogos/<slug>/ganchos.json` — formato em `docs/ganchos.md` (4 tipos). Copie o padrão de
  `jogos/muck/ganchos.json`.
- `jogos/<slug>/mod/Mod.cs` — o modelo já tem `Comando/Tick/Dano/DanoCausado/ParaCada/Runner`.
  Preencha `Msg` (chat **local**), `Amigos`, `Nome` e os comandos.
- Padrões que o usuário sempre quer: `/imortal`, `/dano N` (aceita < 1), dinheiro, F9/F10 dano,
  F11 imortal; trolls do host com alvo alternado (`ParaCada`), `/troll <seg>`, `/limpar`.
- Mensagens **só no chat local**; nada que denuncie quem fez (autor de dano = o próprio alvo/ambiente).

## 4. Instalar e verificar
`pwsh kit/scripts/instalar.ps1 <slug>` — compila, patcha via `ganchos.json`, instala (ou espera o
jogo fechar). Verifique: decompilar `pronto/Assembly-CSharp.dll` e comparar com `.src/` → só as
classes tocadas mudam. Depois de o usuário jogar, procure `[<Mod>]` e exceções no `Player.log`
(`%USERPROFILE%\AppData\LocalLow\<estudio>\<jogo>\Player.log`).

## 5. Registrar (obrigatório)
Atualize `jogos/<slug>/DESCOBERTAS.md` no formato de `docs/DESCOBERTAS-modelo.md`: autoridade,
interruptores, ganchos, comandos, APIs com assinaturas exatas, pegadinhas, limites. É isso que
torna o próximo pedido barato.

## Regras de custo
- Não releia `.src/` inteiro; use `varredura.md` + `Grep` dirigido.
- Um pedido grande de uma vez > vários pequenos (cada mudança no mod = reiniciar o jogo).
- Respostas curtas no chat para perguntas simples ("como abre o chat?").
