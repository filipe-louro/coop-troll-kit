// __MOD__ -- comandos de chat/teclas para __JOGO__.
// Gerado pelo coop-troll-kit (kit/scripts/novo-jogo.ps1). Preencha os TODO com o que o
// varrer.ps1 achou e registre cada gancho em ../ganchos.json.
//
// Convencao do kit (padrao em todos os jogos):
//   Comando(string)  -> chamado pelo gancho do chat; true = era nosso, o jogo nao trata
//   Tick()           -> chamado todo frame (gancho em algum Update sempre ativo): teclas
//   Dano(int)        -> dano que VOCE recebe (gancho no metodo de dano do jogador local)
//   DanoCausado(int) -> dano que VOCE causa (gancho no envio do golpe / no servidor)
//   Msg(string)      -> escreve SO no seu chat
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace __MOD__
{
    public static class Mod
    {
        static bool _imortal;
        static float _multDano = 1f;

        // ---------- ganchos ----------

        public static int Dano(int dano) => _imortal ? 0 : dano;

        public static int DanoCausado(int dano) =>
            _multDano != 1f ? Mathf.Max(1, (int)Mathf.Min(dano * _multDano, 10000000f)) : dano;

        public static bool Comando(string texto)
        {
            try { return Executa(texto); }
            catch (Exception e) { Debug.LogException(e); Msg("erro: " + e.Message); return true; }
        }

        public static void Tick()
        {
            try
            {
                // TODO: retornar se o jogador estiver digitando no chat
                if (Input.GetKeyDown(KeyCode.F9)) SetDano(_multDano * 2f);
                else if (Input.GetKeyDown(KeyCode.F10)) SetDano(_multDano / 2f);
                else if (Input.GetKeyDown(KeyCode.F11)) Executa("/imortal");
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ---------- comandos ----------

        static readonly Dictionary<string, Action<string[]>> Comandos = new Dictionary<string, Action<string[]>>
        {
            ["ajuda"] = a => Msg("/imortal  /dano N  /jogadores  -- teclas: F9 dano x2, F10 dano /2, F11 imortal"),
            ["imortal"] = a => { _imortal = !_imortal; Msg("Imortal (so voce): " + (_imortal ? "LIGADO" : "desligado")); },
            ["dano"] = a => SetDano(a.Length > 0 && float.TryParse(a[0], out var f) && f > 0f ? f : 1f),
            ["jogadores"] = a => Msg(string.Join("\n", Amigos().Select((p, i) => $"{i + 1}: {Nome(p)}"))),
            // TODO trollagens (so host): ["matar"], ["bomba"], ["mob"], ["troll"] ...
        };

        static bool Executa(string texto)
        {
            if (string.IsNullOrEmpty(texto) || texto[0] != '/') return false;
            var partes = texto.Substring(1).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 0 || !Comandos.TryGetValue(partes[0].ToLowerInvariant(), out var acao)) return false;
            acao(partes.Skip(1).ToArray());
            return true;
        }

        static void SetDano(float m)
        {
            _multDano = Mathf.Clamp(m, 0.01f, 100000f);
            Msg(_multDano == 1f ? "Seu dano: normal" : $"Seu dano: x{_multDano:0.##}");
        }

        // ---------- alvos (troque object pelo tipo de jogador do jogo) ----------

        static List<object> Amigos() => new List<object>();   // TODO: todos os jogadores menos o local
        static string Nome(object p) => p?.ToString() ?? "?";  // TODO: nome do jogador

        static int _proximo;

        // sem argumento: alterna entre os vivos; "todos"; numero de /jogadores; ou comeco do nome
        static void ParaCada(string[] args, Action<object> acao)
        {
            var amigos = Amigos();
            if (amigos.Count == 0) { Msg("Nenhum amigo na partida."); return; }
            List<object> alvos;
            string q = string.Join(" ", args).ToLowerInvariant();
            if (q == "") alvos = new List<object> { amigos[_proximo++ % amigos.Count] };
            else if (q == "todos") alvos = amigos;
            else if (int.TryParse(q, out var i) && i >= 1 && i <= amigos.Count) alvos = new List<object> { amigos[i - 1] };
            else alvos = amigos.Where(p => Nome(p).ToLowerInvariant().StartsWith(q)).ToList();
            if (alvos.Count == 0) { Msg("Nao achei esse amigo. Veja /jogadores."); return; }
            foreach (var p in alvos) acao(p);
        }

        // ---------- saida ----------

        static void Msg(string s)
        {
            // TODO: metodo do jogo que escreve SO no chat local (nao o que envia pela rede)
            Debug.Log("[__MOD__] " + s);
        }
    }

    // MonoBehaviour para timers (/troll, atrasos)
    public class Runner : MonoBehaviour
    {
        static Runner _i;
        static Runner I
        {
            get
            {
                if (!_i) { var go = new GameObject("__MOD__Runner"); DontDestroyOnLoad(go); _i = go.AddComponent<Runner>(); }
                return _i;
            }
        }
        public static Coroutine Inicia(IEnumerator e) => I.StartCoroutine(e);
        public static void Para(Coroutine c) { if (_i && c != null) _i.StopCoroutine(c); }
    }
}
