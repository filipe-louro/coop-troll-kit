# SOS OPS — descobertas

| Campo | Valor |
|---|---|
| Data | 2026-09-29 |
| Engine | Unity 6000.3 **IL2CPP** (metadata v39) |
| Rede | Unity Netcode for GameObjects (host = servidor) |
| Anti-cheat | nenhum (EOS é só lobby/voz) |
| Status | god mode, pontos, dinheiro, trolls usados em jogo. Tudo por **RVA** — vale só para a versão de 29/09/2026 |

Caminho IL2CPP: ver `docs/engines.md`. Sem ganchos.json (o patcher Cecil não se aplica).

## 1. Autoridade
| Estado | Quem decide | Consequência |
|---|---|---|
| Vida / dano | host (`PlayerCombat.ApplyDamage`) | patch no host vale para todos; separar por `IsLocalPlayer` (+0x38) |
| Dinheiro/pontos Lootbound | host (`LootRunController` NetworkVariables) | |
| Movimento | `ClientNetworkTransform` (dono) | host **não** lança amigo |
| Roupas | requisitos locais (`Requirement.IsUnlocked`) | patch de disco libera para você |

## 2. Interruptores existentes
`Requirement.AlwaysUnlocked` (static bool) — libera toda customização.

## 3. Pontos de patch (RVA; arquivo = RVA − 0x1A00)
| O quê | Onde | Como |
|---|---|---|
| God mode (todos) | `PlayerCombat.ApplyDamage` 0x302A6E0 | 1º byte → `C3` |
| Só você imortal / dano ×N | ganchos em 5 `ApplyDamage` | `cmp byte [rcx+0x38],0` (local?) ; Damager em `[rdx+0x20]`, Amount float em `[rdx]` |
| Pulo infinito | `PlayerController.FixedUpdate` 0x302F093 (`ja` coyote) | se apertou no ar: zera `+0x104` e salta p/ 0x302F150 |
| Roupas (disco) | 5 `IsUnlocked`/`MoveNext` | `jne`→`jmp` / `xor edx,edx`→`mov dl,1` |
| Amigo morre / bomba | gancho em `PlayerCombat.Update` 0x302D1B0 | chama `ApplyDamage` com `DamageInfo` próprio / `BaseExplode.Explode` 0x2FB2380 |

## 4. Offsets de objeto
`PlayerCombat`: `_maxHealth` +0xA0, `_netHealth` +0xC8, `IsLocalPlayer` +0x38, `_isDead` +0xF0.
`NetworkVariable<int>`: valor +0x68; back-pointer `m_NetworkBehaviour` +0x20.
`LootRunController`: `_netMoney` +0xE0, `_netGlobalScore` +0xC8. `DamageInfo`: Amount +0x0, Source +0x1C, Damager +0x20.

## 6. Pegadinhas
- `Kill()` põe `Source = Unknown` → morte "desconhecida". Usar `ApplyDamage` com Source/Damager reais.
- Trainer que guarda endereço **derrubou o jogo** ao trocar de mapa: reconferir klass antes de cada escrita.
- Busca de objetos só em `MEM_PRIVATE` achava objetos velhos: os da cena atual estavam em `MEM_MAPPED`.
- Busca de 16 classes pelo nome levava 10+ min → cache de klass por PID.

## 7. Não feito / limites
- Lançar/teleportar amigo (movimento é do dono).
- Velocidade: buffs nativos vivem num `Dictionary` vazio sem buff.
