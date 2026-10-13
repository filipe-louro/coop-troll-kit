// MuckMod -- comandos de chat para o Muck (sem Wand/BepInEx).
//
// Assembly-CSharp.dll patchado (Patcher) em 5 pontos:
//   ChatBox.ChatCommand          -> depois do ClearMessage(): if (Mod.Comando(msg)) return;
//   PlayerStatus.HandleDamage    -> dano = Mod.Dano(dano); 0 = ignora        (SO o seu jogador)
//   ClientSend.PlayerDamageMob   -> dano = Mod.DanoCausado(dano)             (seus golpes em mob)
//   ClientSend.PlayerHit         -> dano = Mod.DanoCausado(dano)             (seus golpes em jogador)
//   ClientSend.PlayerHitObject   -> dano = Mod.DanoColeta(dano)              (arvore, pedra, minerio)
//
// No Muck vida, inventario e power-ups sao de CADA PC: o que e "para voce" roda no seu jogo.
// O que mexe nos amigos (dano, mobs, itens no chao) usa os pacotes do servidor -> so o HOST.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace MuckMod
{
    public static class Mod
    {
        static bool _imortal;
        static float _resistencia = 1f, _multDano = 1f, _multColeta = 1f;

        // ---------- hooks ----------

        public static int Dano(int dano)
        {
            if (_imortal) return 0;
            if (_resistencia > 1f)
            {
                float f = dano / _resistencia;
                int n = (int)f;
                if (UnityEngine.Random.value < f - n) n++;
                return n;
            }
            return dano;
        }

        public static int DanoCausado(int dano) => _multDano != 1f ? Mathf.Max(1, (int)Mathf.Min(dano * _multDano, 10000000f)) : dano;
        public static int DanoColeta(int dano) => _multColeta > 1f ? (int)Mathf.Min(dano * _multColeta, 10000000f) : dano;

        public static bool Comando(string texto)
        {
            try { return Executa(texto); }
            catch (Exception e)
            {
                Debug.LogException(e);
                Msg("<color=red>erro no comando:</color> " + e.Message);
                return true;
            }
        }

        // ---------- comandos ----------

        static readonly string[] Nossos =
        {
            "ajuda", "imortal", "resistente", "dano", "coleta", "folego", "velocidade", "pulo", "curar",
            "item", "itens", "powerup", "powerups", "jogadores", "bater", "matar", "mob", "mobs",
            "chefe", "chuva", "troll", "limpar", "ouro", "bomba", "teclas"
        };

        static bool Executa(string texto)
        {
            if (string.IsNullOrEmpty(texto) || texto[0] != '/') return false;
            string[] partes = texto.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0) return false;
            string cmd = partes[0].ToLowerInvariant();
            if (!Nossos.Contains(cmd)) return false;          // deixa o jogo tratar (/seed /kill /kick ...)
            string[] args = partes.Skip(1).ToArray();

            switch (cmd)
            {
                case "ajuda": Ajuda(); break;
                // ---- voce (funciona mesmo sem ser host) ----
                case "imortal":
                    _imortal = !_imortal;
                    Msg("Imortal (so voce): " + (_imortal ? "<color=green>LIGADO</color>" : "desligado"));
                    break;
                case "resistente": _resistencia = Num(args, 1f); Msg(_resistencia > 1f ? $"Voce aguenta {_resistencia}x mais dano" : "Resistencia normal"); break;
                case "dano": SetDano(args.Length > 0 && float.TryParse(args[0], out var md) && md > 0f ? md : 1f); break;
                case "coleta": _multColeta = Num(args, 1f); Msg(_multColeta > 1f ? $"Arvores/pedras/minerio caem com x{_multColeta} de dano" : "Coleta normal"); break;
                case "folego": Folego(); break;
                case "velocidade": Velocidade(Num(args, 1f)); break;
                case "pulo": Pulo(Num(args, 1f)); break;
                case "curar": Curar(); break;
                case "item": Item(args); break;
                case "itens": Msg("<b>Itens:</b> " + string.Join(", ", ItemManager.Instance.allItems.Values.Select(i => Chave(i.name)).OrderBy(x => x))); break;
                case "powerups": Msg("<b>Power-ups:</b> " + string.Join(", ", ItemManager.Instance.allPowerups.Values.Select(p => Chave(p.name)).OrderBy(x => x))); break;
                case "mobs": Msg("<b>Mobs:</b> " + string.Join(", ", MobSpawner.Instance.allMobs.Select(m => Chave(m.name) + (m.boss ? "*" : "")))+ "   (* = chefe)"); break;
                case "jogadores": ListaJogadores(); break;
                case "ouro": Item(new[] { "coin", args.Length > 0 ? args[0] : "500" }); break;
                case "teclas": Teclas(); break;
                // ---- precisam ser HOST ----
                default:
                    if (!LocalClient.serverOwner) { Msg("So o HOST da partida pode usar /" + cmd + "."); break; }
                    switch (cmd)
                    {
                        case "powerup": Powerup(args); break;
                        case "bater":
                        {
                            int d = args.Length > 0 && int.TryParse(args[0], out var x) ? x : 30;
                            ParaCada(args.Skip(1).ToArray(), p => Bater(p, d));
                            break;
                        }
                        case "matar": ParaCada(args, p => Bater(p, 99999)); break;
                        case "mob":
                        {
                            if (args.Length == 0) { Msg("Uso: /mob <nome> [qtd] [amigo]   (/mobs lista os nomes)"); break; }
                            int qtd = args.Length > 1 && int.TryParse(args[1], out var q) ? Mathf.Clamp(q, 1, 30) : 1;
                            ParaCada(args.Skip(qtd > 1 || (args.Length > 1 && int.TryParse(args[1], out _)) ? 2 : 1).ToArray(), p => Mob(args[0], qtd, p, false));
                            break;
                        }
                        case "chefe": ParaCada(args, p => Mob(null, 1, p, true)); break;
                        case "chuva": ParaCada(args, Chuva); break;
                        case "bomba": ParaCada(args, Bomba); break;
                        case "troll": Troll(args); break;
                        case "limpar": Limpar(); break;
                    }
                    break;
            }
            return true;
        }

        static void Ajuda()
        {
            Msg("<b>MuckMod</b> -- para voce (qualquer um):\n" +
                "/imortal  /resistente N  /dano N (0.5 = metade, 10 = x10)  /coleta N  /curar\n" +
                "/ouro [qtd]  /teclas (atalhos)\n" +
                "/folego (stamina e fome infinitas)  /velocidade N  /pulo N\n" +
                "/item <nome> [qtd]  (/itens lista)\n" +
                "So o HOST:\n" +
                "/powerup <nome> [qtd] (cai no seu pe; /powerups lista)\n" +
                "/jogadores  /bater [dano] [amigo]  /matar [amigo]\n" +
                "/bomba [amigo] (explosao + morte)  /mob <nome> [qtd] [amigo] (/mobs lista)  /chefe [amigo]  /chuva [amigo]\n" +
                "/troll <segundos> | off   /limpar (mata os mobs criados)\n" +
                "[amigo] = numero do /jogadores, comeco do nome ou 'todos'; vazio = alterna");
        }

        static void SetDano(float m)
        {
            _multDano = Mathf.Clamp(m, 0.01f, 100000f);
            Msg(_multDano == 1f ? "Seu dano: normal" : $"Seu dano em mobs e jogadores: <b>x{_multDano:0.##}</b>");
        }

        // ---------- teclas (chamado todo frame pelo ChatBox.Update patchado) ----------
        public static void Tick()
        {
            try
            {
                if (ChatBox.Instance && ChatBox.Instance.typing) return;
                if (Input.GetKeyDown(KeyCode.F5)) Host(() => ParaCada(new string[0], Bomba));
                else if (Input.GetKeyDown(KeyCode.F6)) Host(() => ParaCada(new string[0], p =>
                {
                    var normais = MobSpawner.Instance.allMobs.Where(t => !t.boss).ToList();
                    Mob(normais[UnityEngine.Random.Range(0, normais.Count)].name, 1, p, false);
                }));
                else if (Input.GetKeyDown(KeyCode.F7) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))) Host(() => ParaCada(new string[0], p => Mob(null, 1, p, true)));
                else if (Input.GetKeyDown(KeyCode.F8)) Item(new[] { "coin", "500" });
                else if (Input.GetKeyDown(KeyCode.F9)) SetDano(_multDano * 2f);
                else if (Input.GetKeyDown(KeyCode.F10)) SetDano(_multDano / 2f);
                else if (Input.GetKeyDown(KeyCode.F11)) Executa("/imortal");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        static void Host(Action a)
        {
            if (!LocalClient.serverOwner) { Msg("So o HOST pode usar essa tecla."); return; }
            a();
        }

        static void Teclas()
        {
            Msg("<b>Teclas</b> (fora do chat):\n" +
                "F5 bomba no proximo amigo (host)   F6 bicho perto dele (host)   Shift+F7 chefe nele (host, nunca o Bob)\n" +
                "F8 +500 de ouro   F9 dano x2   F10 dano /2   F11 imortal liga/desliga");
        }

        // "bomba": efeito de explosao para todos verem + morte do amigo
        static int _efeitoExplosao = -2;

        static void Bomba(PlayerManager p)
        {
            var pc = PowerupCalculations.Instance;
            if (_efeitoExplosao == -2 && pc)
            {
                var efs = pc.onHitEffects;
                _efeitoExplosao = -1;
                for (int i = 0; i < efs.Length; i++)
                    if (efs[i] && efs[i].name.ToLowerInvariant().Contains("xplo")) { _efeitoExplosao = i; break; }
                Debug.Log("[MuckMod] efeitos: " + string.Join(", ", efs.Select((e, i) => i + "=" + (e ? e.name : "null"))));
            }
            Vector3 pos = p.transform.position;
            if (_efeitoExplosao >= 0)
            {
                pc.SpawnOnHitEffect(_efeitoExplosao, owner: false, pos, 0);
                ServerSend.SpawnEffect(_efeitoExplosao, pos, LocalClient.instance.myId);
            }
            ServerSend.HitPlayer(p.id, 99999, 0f, p.id, 0, pos);
            Msg($"BUM em {p.username}");
        }

        static float Num(string[] a, float padrao) => a.Length > 0 && float.TryParse(a[0], out var f) && f >= 1f ? f : padrao;
        static string Chave(string s) => (s ?? "").Replace(" ", "").ToLowerInvariant();

        // ---------- voce ----------

        static Coroutine _folego;

        static void Folego()
        {
            if (_folego != null) { Runner.Para(_folego); _folego = null; Msg("Stamina e fome normais"); return; }
            _folego = Runner.Inicia(LoopFolego());
            Msg("Stamina e fome infinitas (so voce): <color=green>LIGADO</color>");
        }

        static IEnumerator LoopFolego()
        {
            while (true)
            {
                var ps = PlayerStatus.Instance;
                if (ps) { ps.stamina = ps.maxStamina; ps.hunger = ps.maxHunger; }
                yield return new WaitForSeconds(0.25f);
            }
        }

        static readonly Dictionary<string, float> _origMov = new Dictionary<string, float>();
        const BindingFlags Priv = BindingFlags.Instance | BindingFlags.NonPublic;

        static void Escala(string campo, float fator)
        {
            var pm = PlayerMovement.Instance;
            var f = typeof(PlayerMovement).GetField(campo, Priv);
            if (!pm || f == null) return;
            if (!_origMov.ContainsKey(campo)) _origMov[campo] = (float)f.GetValue(pm);
            f.SetValue(pm, _origMov[campo] * fator);
        }

        static void Velocidade(float n)
        {
            foreach (var c in new[] { "moveSpeed", "maxWalkSpeed", "maxRunSpeed", "maxSpeed", "swimSpeed" }) Escala(c, n);
            Msg(n > 1f ? $"Velocidade x{n} (so voce)" : "Velocidade normal");
        }

        static void Pulo(float n)
        {
            Escala("jumpForce", n);
            Msg(n > 1f ? $"Pulo x{n} (so voce)" : "Pulo normal");
        }

        static void Curar()
        {
            var ps = PlayerStatus.Instance;
            if (!ps) return;
            ps.hp = ps.maxHp; ps.shield = ps.maxShield;
            ps.stamina = ps.maxStamina; ps.hunger = ps.maxHunger;
            Msg("Vida, escudo, stamina e fome cheios");
        }

        static InventoryItem AchaItem(string nome)
        {
            string k = Chave(nome);
            var todos = ItemManager.Instance.allItems.Values;
            return todos.FirstOrDefault(i => Chave(i.name) == k) ?? todos.FirstOrDefault(i => Chave(i.name).StartsWith(k));
        }

        static void Item(string[] args)
        {
            if (args.Length == 0) { Msg("Uso: /item <nome> [qtd]   (/itens lista os nomes)"); return; }
            int qtd = args.Length > 1 && int.TryParse(args.Last(), out var q) ? Mathf.Clamp(q, 1, 9999) : 1;
            string nome = string.Join("", args.Length > 1 && int.TryParse(args.Last(), out _) ? args.Take(args.Length - 1) : args);
            var modelo = AchaItem(nome);
            if (!modelo) { Msg($"Nao achei o item \"{nome}\". Veja /itens."); return; }
            var it = ScriptableObject.CreateInstance<InventoryItem>();
            it.Copy(modelo, qtd);
            InventoryUI.Instance.AddItemToInventory(it);
            Msg($"+{qtd} {modelo.name} no seu inventario");
        }

        // ---------- host ----------

        static void Powerup(string[] args)
        {
            if (args.Length == 0) { Msg("Uso: /powerup <nome> [qtd]   (/powerups lista)"); return; }
            int qtd = args.Length > 1 && int.TryParse(args.Last(), out var q) ? Mathf.Clamp(q, 1, 50) : 1;
            string k = Chave(string.Join("", args.Length > 1 && int.TryParse(args.Last(), out _) ? args.Take(args.Length - 1) : args));
            var p = ItemManager.Instance.allPowerups.FirstOrDefault(kv => Chave(kv.Value.name) == k);
            if (p.Value == null) p = ItemManager.Instance.allPowerups.FirstOrDefault(kv => Chave(kv.Value.name).StartsWith(k));
            if (p.Value == null) { Msg("Nao achei esse power-up. Veja /powerups."); return; }
            Vector3 pos = PlayerMovement.Instance.transform.position + Vector3.up;
            for (int i = 0; i < qtd; i++)
            {
                int id = ItemManager.Instance.GetNextId();
                Vector3 pi = pos + new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f));
                ItemManager.Instance.DropPowerupAtPosition(p.Key, pi, id);
                ServerSend.DropPowerupAtPosition(p.Key, id, pi);
            }
            Msg($"{qtd}x {p.Value.name} no seu pe -- pegue");
        }

        static List<PlayerManager> Amigos() =>
            GameManager.players.Values.Where(p => p && p.id != LocalClient.instance.myId && !p.disconnected).ToList();

        static void ListaJogadores()
        {
            var a = Amigos();
            Msg(a.Count == 0 ? "Nenhum amigo na partida." :
                string.Join("\n", a.Select((p, i) => $"{i + 1}: {p.username}{(p.dead ? " (morto)" : "")}")));
        }

        static int _proximo;

        static void ParaCada(string[] args, Action<PlayerManager> acao)
        {
            var amigos = Amigos();
            if (amigos.Count == 0) { Msg("Nenhum amigo na partida."); return; }
            List<PlayerManager> alvos;
            if (args.Length == 0)
            {
                var vivos = amigos.Where(p => !p.dead).ToList();
                if (vivos.Count == 0) { Msg("Todos os amigos estao mortos."); return; }
                alvos = new List<PlayerManager> { vivos[_proximo++ % vivos.Count] };
            }
            else
            {
                string q = string.Join(" ", args).ToLowerInvariant();
                if (q == "todos") alvos = amigos.Where(p => !p.dead).ToList();
                else if (int.TryParse(q, out var i) && i >= 1 && i <= amigos.Count) alvos = new List<PlayerManager> { amigos[i - 1] };
                else alvos = amigos.Where(p => (p.username ?? "").ToLowerInvariant().StartsWith(q)).ToList();
            }
            if (alvos.Count == 0) { Msg("Nao achei esse amigo. Veja /jogadores."); return; }
            foreach (var p in alvos) acao(p);
        }

        // O PC do amigo aplica com DealDamage(..., ignoreProtection: true). O "autor" vai como o
        // proprio amigo: o jogo so usa isso para uma conquista -- nao aponta voce.
        static void Bater(PlayerManager p, int dano)
        {
            ServerSend.HitPlayer(p.id, dano, 0.5f, p.id, 0, p.transform.position);
            Msg(dano >= 99999 ? $"{p.username} morreu do nada" : $"{p.username} levou {dano} de dano");
        }

        static readonly List<int> _criados = new List<int>();

        static bool EhChefeFinal(MobType t) => t && t.mobPrefab && t.mobPrefab.GetComponent<BobMob>();

        static void Mob(string nome, int qtd, PlayerManager p, bool chefe)
        {
            var tipos = MobSpawner.Instance.allMobs;
            MobType tipo;
            if (chefe)
            {
                // nunca o Bob: e o chefe final -- mata-lo encerra a partida (Dragon -> GameOver)
                var chefes = tipos.Where(t => t.boss && !EhChefeFinal(t)).ToList();
                tipo = chefes.Count > 0 ? chefes[UnityEngine.Random.Range(0, chefes.Count)] : null;
            }
            else
            {
                string k = Chave(nome);
                tipo = tipos.FirstOrDefault(t => Chave(t.name) == k) ?? tipos.FirstOrDefault(t => Chave(t.name).StartsWith(k));
            }
            if (!tipo) { Msg($"Nao achei o mob \"{nome}\". Veja /mobs."); return; }
            if (EhChefeFinal(tipo)) { Msg("Esse e o chefe final (Bob): mata-lo ENCERRA a partida. Nao vou criar."); return; }
            for (int i = 0; i < qtd; i++)
            {
                Vector3 pos = p.transform.position + new Vector3(UnityEngine.Random.Range(-6f, 6f), 2f, UnityEngine.Random.Range(-6f, 6f));
                int id = MobManager.Instance.GetNextId();
                MobSpawner.Instance.ServerSpawnNewMob(id, tipo.id, pos, 1f, 1f, chefe ? global::Mob.BossType.BossNight : global::Mob.BossType.None);
                _criados.Add(id);
            }
            Msg($"{qtd}x {tipo.name} em cima de {p.username}");
        }

        // chuva de itens inuteis em volta do amigo (pedra, madeira...)
        static void Chuva(PlayerManager p)
        {
            var itens = ItemManager.Instance.allItems.Keys.ToList();
            for (int i = 0; i < 25; i++)
            {
                int item = itens[UnityEngine.Random.Range(0, itens.Count)];
                int id = ItemManager.Instance.GetNextId();
                Vector3 pos = p.transform.position + new Vector3(UnityEngine.Random.Range(-4f, 4f), 6f + i * 0.3f, UnityEngine.Random.Range(-4f, 4f));
                ItemManager.Instance.DropItemAtPosition(item, 1, pos, id);
                ServerSend.DropItemAtPosition(item, 1, id, pos);
            }
            Msg($"Chuva de itens em {p.username}");
        }

        static void Limpar()
        {
            int n = 0;
            foreach (int id in _criados.ToList())
            {
                if (MobManager.Instance.mobs.TryGetValue(id, out var m) && m && !m.GetComponent<BobMob>())
                {
                    ClientSend.PlayerDamageMob(id, 99999999, 1f, 0, m.transform.position);
                    n++;
                }
            }
            _criados.Clear();
            Msg($"{n} mob(s) criados eliminados");
        }

        static Coroutine _troll;

        static void Troll(string[] args)
        {
            if (_troll != null) { Runner.Para(_troll); _troll = null; }
            if (args.Length == 0 || !float.TryParse(args[0], out var seg) || seg <= 0)
            {
                Msg("Trollagem automatica desligada. Uso: /troll <segundos>");
                return;
            }
            _troll = Runner.Inicia(LoopTroll(seg));
            Msg($"Trollagem automatica a cada {seg:0} s, alternando os amigos");
        }

        static IEnumerator LoopTroll(float seg)
        {
            var acoes = new Action<PlayerManager>[]
            {
                p => Bater(p, 25),
                p => Mob("goblin", 2, p, false),
                p => Mob(null, 1, p, false),
                Chuva,
            };
            while (true)
            {
                yield return new WaitForSeconds(seg);
                try
                {
                    var a = acoes[UnityEngine.Random.Range(0, acoes.Length)];
                    ParaCada(new string[0], p =>
                    {
                        if (a == acoes[2])   // mob aleatorio nao-chefe
                        {
                            var normais = MobSpawner.Instance.allMobs.Where(t => !t.boss).ToList();
                            Mob(normais[UnityEngine.Random.Range(0, normais.Count)].name, 1, p, false);
                        }
                        else a(p);
                    });
                }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        static void Msg(string s)
        {
            try { ChatBox.Instance.AppendMessage(-1, "<color=#7fd4ff>" + s + "<color=white>", ""); } catch { }
            Debug.Log("[MuckMod] " + s);
        }
    }

    public class Runner : MonoBehaviour
    {
        static Runner _i;

        static Runner I
        {
            get
            {
                if (!_i)
                {
                    var go = new GameObject("MuckModRunner");
                    DontDestroyOnLoad(go);
                    _i = go.AddComponent<Runner>();
                }
                return _i;
            }
        }

        public static Coroutine Inicia(IEnumerator e) => I.StartCoroutine(e);
        public static void Para(Coroutine c) { if (_i && c != null) _i.StopCoroutine(c); }
    }
}


