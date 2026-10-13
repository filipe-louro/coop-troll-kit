# Por engine — qual caminho seguir

| Engine | Como reconhecer | Caminho | Custo |
|---|---|---|---|
| **Unity Mono** | `*_Data\Managed\Assembly-CSharp.dll` | ILSpy lê quase o código original → `ganchos.json` + mod em C# | baixo |
| **Unity IL2CPP** | `GameAssembly.dll` + `*_Data\il2cpp_data` | Cpp2IL para ler; patch em memória/disco por RVA (x64) | alto |
| **Unreal** | `.pak`, `*-Shipping.exe` | save (GVAS), CheatManager se compilado, memória | alto |
| **Godot** | `.pck` | extrair `.gdc` (bytecode) | médio |

## Unity Mono (o caminho do kit)
`novo-jogo.ps1` → `varrer.ps1` → ler `varredura.md` → escrever ganchos + mod → `instalar.ps1`.
- O `Assembly-CSharp.dll` **não** fica travado com o jogo aberto (Mono carrega em memória), mas a DLL
  do mod **fica**. Mod novo = reiniciar o jogo (o `instalar.ps1` espera sozinho).
- Unity antiga: `Input` está em `UnityEngine.InputLegacyModule.dll`.
- Unity 6: `FindObjectsByType(FindObjectsSortMode)` está obsoleto — use a sobrecarga sem parâmetro.
- Prefab ≠ instância: propriedades preenchidas no `Awake` vêm `null` no prefab — use `GetComponent`.

## Unity IL2CPP (visto no SOS OPS — `jogos/sos-ops/`)
- Il2CppDumper não lê metadata v39 (Unity 6). Cpp2IL:
  `--game-path <pasta> --exe-name "<exe>" --output-as dll_il_recovery --use-processor attributeinjector --output-to <dir>`
  (é `--output-to`). O atributo `[FieldOffset]` vem **antes** do campo; genéricos abertos saem 0x0.
- Arquivo ↔ memória: `offset_no_arquivo = RVA − (VirtualAddress − PointerToRawData)` da seção `.text`.
- Patch em memória: comparar os 16 bytes vivos com o arquivo **antes** de escrever.
- Ganchos x64: `jmp rel32` para bloco alocado a < 2 GB do módulo; se o bloco passar de 127 bytes,
  saltos condicionais `rel32`.
- Objetos vivos: achar `Il2CppClass` pelo nome (+0x10 nome, +0x18 namespace), varrer o heap pelo
  ponteiro de klass — **incluindo `MEM_MAPPED`** — e validar por back-pointer.
- Endereço guardado pode morrer (troca de mapa): reconferir o klass **antes de cada escrita**. O GC
  (Boehm) sobrescreve o início do objeto liberado. Escrever em objeto morto = crash nativo.
