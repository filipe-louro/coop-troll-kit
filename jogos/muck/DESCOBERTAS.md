# Muck — descobertas

| Campo | Valor |
|---|---|
| Data | 2026-10-13 |
| Pasta | `steamapps\common\Muck` (Steam, grátis) |
| Engine | Unity 2019.4.19 Mono |
| Rede | própria (`Server*/Client*`) sobre Steam P2P (Facepunch) |
| Anti-cheat | nenhum |
| Status | **instalado e verificado** (patch gerado pelo kit = patch manual, byte a byte na decompilação) |

## 1. Autoridade
| Estado | Quem decide | Consequência |
|---|---|---|
| Vida do jogador | **cada PC** (`PlayerStatus.Instance`, só o local) | imortal/resistência funcionam até como cliente |
| Dano causado | **cliente** manda o número (`ClientSend.PlayerDamageMob/PlayerHit/PlayerHitObject`); o servidor confia | multiplicador no envio |
| Inventário / power-ups / ouro | **cada PC** (`InventoryUI`, `PowerupInventory`) | `/item`, `/ouro` locais |
| Inimigos | host (`MobSpawner.ServerSpawnNewMob` spawna local + manda `MobSpawn`) | host cria bicho em qualquer lugar |
| Dano em amigo | host manda `ServerSend.HitPlayer`; o PC dele faz `DealDamage(ignoreProtection: true)` | host mata/fere amigo |
| Movimento | cada PC | não dá para lançar amigo |

## 2. Interruptores que o jogo já tem
Chat com `/seed /ping /debug /kill /kick` (`ChatBox.commands`). Comandos com `/` **nunca são enviados**
(`ChatBox.SendMessage` desvia para `ChatCommand`). `/debug` abre o console de rede.

## 3. Ganchos (`ganchos.json`)
| # | Tipo | Classe.Método | Chama | Por quê |
|---|---|---|---|---|
| 1 | interceptar (depois de `ClearMessage`) | `ChatBox.ChatCommand` | `Comando` | comandos do mod |
| 2 | no-inicio | `ChatBox.Update` | `Tick` | teclas F5–F11 |
| 3 | filtrar-arg + retornarSeZero | `PlayerStatus.HandleDamage(damageTaken)` | `Dano` | imortal/resistência |
| 4–5 | filtrar-arg | `ClientSend.PlayerDamageMob/PlayerHit(damage)` | `DanoCausado` | `/dano`, F9/F10 |
| 6 | filtrar-arg | `ClientSend.PlayerHitObject(damage)` | `DanoColeta` | `/coleta` |

## 4. Comandos e teclas
Para você: `/imortal /resistente N /dano N /coleta N /folego /velocidade N /pulo N /curar /item /itens /ouro`.
Host: `/powerup /powerups /jogadores /bater /matar /bomba /mob /mobs /chefe /chuva /troll /limpar`.
Teclas: F5 bomba · F6 bicho · F7 chefe (host) · F8 +500 ouro · F9/F10 dano ×2/÷2 · F11 imortal.

## 5. APIs usadas
- Chat local: `ChatBox.Instance.AppendMessage(-1, texto, "")`; digitando: `ChatBox.Instance.typing`.
- Jogadores: `GameManager.players` (`Dictionary<int, PlayerManager>`), `PlayerManager.id/username/dead/disconnected`;
  local: `LocalClient.instance.myId`; host: `LocalClient.serverOwner`.
- Itens: `ItemManager.Instance.allItems` (`InventoryItem.name` é campo `new string name`), cópia com
  `ScriptableObject.CreateInstance<InventoryItem>().Copy(modelo, qtd)` → `InventoryUI.Instance.AddItemToInventory`. Ouro = item `"Coin"`.
- Drop para todos: `ItemManager.DropItemAtPosition(id, qtd, pos, objId)` local **+**
  `ServerSend.DropItemAtPosition(id, qtd, objId, pos)` (este exclui o host). Power-up: `DropPowerupAtPosition` idem.
- Mobs: `MobSpawner.Instance.allMobs` (`MobType.name`, `.boss`, `.id`); `ServerSpawnNewMob(MobManager.Instance.GetNextId(), tipo, pos, 1, 1, BossType)`.
- Matar mob: `ClientSend.PlayerDamageMob(id, 99999999, 1, 0, pos)`.
- Efeito visível a todos: `PowerupCalculations.Instance.SpawnOnHitEffect(i, false, pos, 0)` + `ServerSend.SpawnEffect(i, pos, myId)`.
- Movimento: campos privados de `PlayerMovement.Instance`: `moveSpeed maxWalkSpeed maxRunSpeed maxSpeed swimSpeed jumpForce`.

## 6. Pegadinhas
- `Input` fica em `UnityEngine.InputLegacyModule.dll` (Unity 2019) — sem a referência, não compila.
- "Autor" do `HitPlayer` só é usado para uma conquista; mandar o próprio amigo como autor não aponta você.
- O efeito de explosão é escolhido pelo nome (contém "xplo"); a lista sai no log na 1ª `/bomba` — **não verificado em jogo**.

- **Bob é o chefe final**: matá-lo encerra a partida (`Dragon` → `GameOver`). Identificar por `mobPrefab.GetComponent<BobMob>()`.
  `/chefe`/F7 nunca o sorteiam e `/limpar` nunca o mata (aprendido do jeito difícil: F7 + `/limpar` zerou o jogo).
- F7 (chefe) exige **Shift** — tecla única perto de F5/F6 é fácil de apertar sem querer.

## 7. Não feito / limites
- Lançar/teleportar amigo: movimento é de cada PC, não há pacote para isso.

