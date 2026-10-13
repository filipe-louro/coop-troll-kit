# How to Fish — descobertas

| Campo | Valor |
|---|---|
| Data | 2026-10-02 |
| Engine | Unity 6000.4 Mono |
| Rede | FishNet (servidor autoritativo; host = servidor) |
| Anti-cheat | nenhum |
| Status | mod usado em jogo (faca, arma, iscas, trolls confirmados no log). **Código do mod perdido** depois — só os ganchos e estas descobertas ficaram; reescrever a partir daqui |

## 1. Autoridade
| Estado | Quem decide | Consequência |
|---|---|---|
| Vida | servidor (`PlayerVitals.TakeDamage`, `IsServerInitialized`) | gancho no host vale para todos; filtrar por `Player.LocalPlayer` |
| Dano causado | **quem bate** calcula e manda `Server.HitCreature/HitPlayer` (ServerRpc sem ownership) | multiplicar no `RpcLogic___` com "quem bateu" |
| Upgrades de arma | SyncVar **por item** | melhora só a sua arma |
| Inventário extra / iscas / fome | por jogador (`PlayerInventory._extraSlots/_ownedBaits`, `PlayerVitals._syncedFullness`) | só você |
| Movimento | dono, mas `Player.RPCTeleport` é **TargetRpc** | host consegue lançar amigo para o alto |

## 2. Interruptores que o jogo já tem
`DazedCommands.IsServerCommand`: ~25 comandos (`/spawn /addmoney /oneshot /godmode /killboss /allskins /nextisland`...)
travados por `ClientSettings.CheatsEnabled`, que só liga para 7 SteamIDs dos devs (`SteamManager.IsDev`).
Trocar **só** a chamada `get_CheatsEnabled` dentro desse método por `1` libera sem efeitos colaterais
(ligar o `CheatsEnabled` global mexeria em save/ilhas/dinheiro).

## 3. Ganchos (`ganchos.json`)
| # | Tipo | Classe.Método | Chama |
|---|---|---|---|
| 1 | interceptar (retorno `true`) | `DazedCommands.IsServerCommand` | `Comando(fullCommand)` |
| 2 | trocar-chamada | `IsServerCommand` → `get_CheatsEnabled` = 1 | — |
| 3 | filtrar-arg + retornarSeZero | `PlayerVitals.TakeDamage(amount)`, extra `this._player` | `Dano(Player, int)` |
| 4 | filtrar-arg | `Server.RpcLogic___HitCreature*(P_2)`, extra `P_1` (quem) | `DanoCausado(Player, int)` |
| 5 | filtrar-arg | `Server.RpcLogic___HitPlayer*(P_1)`, extra `P_5` (quem) | `DanoCausado(Player, int)` |
| 6 | filtrar-arg | `CasinoManager.ServerRouletteResult(winColor)`, depois `this._curBetColor` | `Roleta(BetColor, BetColor)` |

## 4. Comandos que existiam
`/faca /arma /tudo /bolsos /isca [infinita] /saciado /imortal /resistente /dano /cassino ganhar|perder|off`
· trolls `/jogadores /matar /fogo /veneno /voar /bomba /criatura /lista /limpar /troll`.

## 5. APIs
- Afiação: `Melee._syncedSharpnessIndex` (SyncVar público) → máx de `_sharpnessUpgrades` (privado).
- Arma de fogo: `Attachments._syncedBulletIndex`; `SetAttachment(info)` (mesmo caminho da loja) para
  laser/pente; mira/cano = o mais caro em `_attachmentCosts`.
- Bolsos: `PlayerInventory.UnlockExtraPocket(max)`; servidor aceita até 5 extras.
- Iscas: `PlayerInventory._ownedBaits` (SyncList; índice i = isca i+1 de `GameInfo.AllBaits`).
- Fome: `PlayerVitals.RestoreFullness(100)` (zera também o contador de "comeu agora").
- Trolls: `Vitals.ApplyNewFire/ApplyNewPoison`, `TakeDamage(9999, ..., ignoreInvulnerability: true)`,
  `Player.RPCTeleport(owner, pos, rot)`, `GameInfo._nameToSpawnable` + `Server.Instance.Spawn(go)`,
  dinamite `GameInfo.GetSpawnable("dynamite")` + `Explosive.ForceExplode(null, instant)`.
- Matar criatura: `Creature.ServerChangeHp(999999)`.

## 6. Pegadinhas
- `/godmode` e `/oneshot` originais são **globais** (checagem no servidor para todos).
- `/killallcreatures` **não mata**: marca o diário todo como pescado no save.
- `Item.Explosive` é preenchido no `Awake`: no prefab vem `null` → `GetComponent<Explosive>()`.
- `HtfMod.dll` fica travada com o jogo aberto; o `Assembly-CSharp` não. As duas dependem uma da
  outra: instalar **juntas** ao fechar o jogo.
- Roleta: a bolinha é física; o gancho troca só o resultado. Não sabe quem apostou → vale para todos.
