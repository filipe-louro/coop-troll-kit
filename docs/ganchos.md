# ganchos.json — referência

O `kit/Patcher` lê `jogos/<jogo>/ganchos.json`, abre o `Assembly-CSharp.orig.dll` (backup sem patch)
e grava o patchado em `saida`. Nada do código do jogo é apagado: só **inserimos** chamadas para a
DLL do mod. Se qualquer gancho falhar, nada é gravado.

```jsonc
{
  "managed": "C:/.../Jogo_Data/Managed",          // pasta das DLLs do jogo
  "modDll": "mod/bin/Release/net472/XMod.dll",    // relativo ao ganchos.json
  "modClasse": "XMod.Mod",
  "saida": "pronto/Assembly-CSharp.dll",          // instalar.ps1 copia daqui
  "ganchos": [ ... ]
}
```

## Os 4 tipos

| Tipo | Código inserido | Uso típico |
|---|---|---|
| `no-inicio` | `Mod.F(args);` | `Tick()` em um `Update` sempre ativo (teclas) |
| `interceptar` | `if (Mod.F(args)) return X;` | comandos de chat: o mod trata e o jogo não |
| `filtrar-arg` | `p = Mod.F(extras..., p, extrasDepois...);` (+ `if (p == 0) return;`) | dano recebido/causado, resultado de sorteio |
| `trocar-chamada` | troca `call Alvo()` por uma constante | destravar `IsDev`/`CheatsEnabled` só naquele método |

Campos:
- `classe` — nome simples ou completo do tipo. `metodo` — nome exato; `"prefixo": true` para nomes
  gerados (`RpcLogic___HitCreature___215526726`).
- `args` / `extras` / `extrasDepois` — `"arg:<nome>"`, `"this"`, `"this.<campo>"`.
- `interceptar`: `depoisDaChamada` insere logo após uma chamada (ex.: depois de `ClearMessage`);
  `retorno`: `void` | `true` | `false` | `null` — tem de bater com o retorno do método.
- `filtrar-arg`: `retornarSeZero` só em método `void` (ex.: dano 0 = ignora).
- `trocar-chamada`: só chamadas **estáticas sem parâmetro** (getters); `esperado` aborta se o número
  de trocas mudar (protege contra update do jogo).

## Exemplos reais
- `jogos/muck/ganchos.json` — chat local, `Update`, dano recebido, 3 envios de dano.
- `jogos/how-to-fish/ganchos.json` — comando com retorno `bool`, destrava `CheatsEnabled`, dano no
  servidor com "quem bateu" como extra, roleta com campo do objeto como `extrasDepois`.

## Verificar sempre
Decompile o patchado e compare com o original: só as classes tocadas podem diferir
(o ILSpy às vezes mostra método de extensão de outro jeito — mesmo IL, ignore).
