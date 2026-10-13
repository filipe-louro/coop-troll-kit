# coop-troll-kit

Kit para criar mods de jogos co-op (benefícios para você + trollagens nos amigos) com um agente de IA.
Responda em **português**. Para qualquer pedido de mod/cheat/trollagem, siga a skill `modar-jogo`.

## Mapa
- `kit/Patcher` — patcher genérico (Mono.Cecil) guiado por `jogos/<jogo>/ganchos.json`.
- `kit/ModTemplate` — esqueleto do mod (C#, net472, compila contra as DLLs do jogo).
- `kit/scripts` — `novo-jogo.ps1` → `varrer.ps1` → `instalar.ps1`.
- `docs/` — `ganchos.md` (formato), `engines.md` (Mono/IL2CPP/Unreal/Godot), `DESCOBERTAS-modelo.md`.
- `jogos/<jogo>/` — `DESCOBERTAS.md` (sempre ler primeiro), `ganchos.json`, `mod/`.
  `.src/` (código decompilado) e `pronto/` (DLLs geradas) **nunca** vão para o git.

## Limites
Só partidas privadas com amigos, jogos sem anti-cheat. Nunca commitar DLL de jogo nem código decompilado.
