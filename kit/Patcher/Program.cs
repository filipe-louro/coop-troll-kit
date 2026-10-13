// Patcher generico para jogos Unity Mono (Mono.Cecil), guiado por um ganchos.json.
//
//   dotnet run --project kit/Patcher -c Release -- <ganchos.json>
//
// O ganchos.json diz onde estao as DLLs, qual e a classe do mod e a lista de ganchos.
// Sempre le o <Assembly>.orig.dll (backup criado na primeira vez) e grava em "saida".
// Tipos de gancho (todos inserem codigo; nada do jogo e apagado):
//
//   no-inicio      -> chama Mod.<chamar>(args...) no inicio do metodo (retorno descartado)
//   interceptar    -> if (Mod.<chamar>(args...)) return <retorno>;  (no inicio ou depois de uma chamada)
//   filtrar-arg    -> <param> = Mod.<chamar>(extras..., <param>, extrasDepois...);   opcional: if (<param> == 0) return;
//   trocar-chamada -> troca TODA chamada a <alvo> dentro do metodo por uma constante int/bool
//
// args/extras: "arg:<nome>" (parametro), "this", "this.<campo>".
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

class Gancho
{
    public string tipo { get; set; }          // no-inicio | interceptar | filtrar-arg | trocar-chamada
    public string classe { get; set; }        // tipo do jogo (ex.: "ChatBox", "Namespace.Classe")
    public string metodo { get; set; }        // nome (ou prefixo, se "prefixo": true)
    public bool prefixo { get; set; }
    public string chamar { get; set; }        // metodo estatico da classe do mod
    public List<string> args { get; set; } = new();
    public string depoisDaChamada { get; set; } // interceptar: inserir logo depois desta chamada
    public string retorno { get; set; }       // interceptar: "true" | "false" | "void" | "null"
    public string param { get; set; }         // filtrar-arg
    public List<string> extras { get; set; } = new();       // empilhados ANTES do param
    public List<string> extrasDepois { get; set; } = new(); // empilhados DEPOIS do param
    public bool retornarSeZero { get; set; }  // filtrar-arg em metodo void
    public string alvo { get; set; }          // trocar-chamada: nome do metodo chamado
    public int valor { get; set; }            // trocar-chamada: constante
    public int esperado { get; set; } = -1;   // trocar-chamada: quantas trocas devem acontecer
}

class Config
{
    public string managed { get; set; }
    public string assembly { get; set; } = "Assembly-CSharp.dll";
    public string modDll { get; set; }
    public string modClasse { get; set; }
    public string saida { get; set; }
    public List<Gancho> ganchos { get; set; } = new();
}

class Program
{
    static int Main(string[] a)
    {
        if (a.Length < 1) { Console.WriteLine("uso: Patcher <ganchos.json>"); return 2; }
        string cfgPath = Path.GetFullPath(a[0]);
        string baseDir = Path.GetDirectoryName(cfgPath);
        var cfg = JsonSerializer.Deserialize<Config>(File.ReadAllText(cfgPath),
            new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
        string R(string p) => Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(baseDir, p));

        string managed = R(cfg.managed);
        string alvo = Path.Combine(managed, cfg.assembly);
        string orig = Path.ChangeExtension(alvo, ".orig.dll");
        if (!File.Exists(orig)) { File.Copy(alvo, orig); Console.WriteLine("backup: " + orig); }
        string saida = cfg.saida != null ? R(cfg.saida) : alvo;

        var res = new DefaultAssemblyResolver();
        res.AddSearchDirectory(managed);
        var rp = new ReaderParameters { AssemblyResolver = res };
        var asm = AssemblyDefinition.ReadAssembly(orig, rp);
        var mod = AssemblyDefinition.ReadAssembly(R(cfg.modDll), rp);
        var m = asm.MainModule;
        var tMod = mod.MainModule.GetType(cfg.modClasse) ?? throw new Exception("classe do mod nao achada: " + cfg.modClasse);

        int erros = 0;
        foreach (var g in cfg.ganchos)
        {
            try { Aplica(m, tMod, g); }
            catch (Exception e) { Console.WriteLine($"ERRO {g.tipo} {g.classe}.{g.metodo}: {e.Message}"); erros++; }
        }
        if (erros > 0) { Console.WriteLine($"ABORTADO: {erros} gancho(s) falharam -- nada foi gravado"); return 1; }
        asm.Write(saida);
        Console.WriteLine("gravado: " + saida);
        return 0;
    }

    static void Aplica(ModuleDefinition m, TypeDefinition tMod, Gancho g)
    {
        var tipo = m.GetType(g.classe) ?? m.Types.SelectMany(TodosTipos).FirstOrDefault(t => t.FullName == g.classe || t.Name == g.classe)
                   ?? throw new Exception("classe nao achada");
        var metodos = tipo.Methods.Where(x => g.prefixo ? x.Name.StartsWith(g.metodo) : x.Name == g.metodo).ToList();
        if (metodos.Count != 1) throw new Exception($"{metodos.Count} metodos com esse nome (use um nome unico ou prefixo)");
        var met = metodos[0];
        var il = met.Body.GetILProcessor();
        MethodReference Mod() => m.ImportReference(tMod.Methods.Single(x => x.Name == g.chamar));

        switch (g.tipo)
        {
            case "no-inicio":
            {
                var p = met.Body.Instructions[0];
                foreach (var ins in Carrega(met, il, g.args)) il.InsertBefore(p, ins);
                il.InsertBefore(p, il.Create(OpCodes.Call, Mod()));
                var rt = tMod.Methods.Single(x => x.Name == g.chamar).ReturnType;
                if (rt.FullName != "System.Void") il.InsertBefore(p, il.Create(OpCodes.Pop));
                break;
            }
            case "interceptar":
            {
                Instruction p = met.Body.Instructions[0];
                if (g.depoisDaChamada != null)
                    p = met.Body.Instructions.Single(i => (i.OpCode == OpCodes.Call || i.OpCode == OpCodes.Callvirt)
                            && i.Operand is MethodReference r && r.Name == g.depoisDaChamada).Next;
                foreach (var ins in Carrega(met, il, g.args)) il.InsertBefore(p, ins);
                il.InsertBefore(p, il.Create(OpCodes.Call, Mod()));
                il.InsertBefore(p, il.Create(OpCodes.Brfalse, p));
                switch (g.retorno ?? "void")
                {
                    case "true": il.InsertBefore(p, il.Create(OpCodes.Ldc_I4_1)); break;
                    case "false": il.InsertBefore(p, il.Create(OpCodes.Ldc_I4_0)); break;
                    case "null": il.InsertBefore(p, il.Create(OpCodes.Ldnull)); break;
                }
                il.InsertBefore(p, il.Create(OpCodes.Ret));
                break;
            }
            case "filtrar-arg":
            {
                var par = met.Parameters.SingleOrDefault(x => x.Name == g.param) ?? throw new Exception("parametro nao achado: " + g.param);
                var p = met.Body.Instructions[0];
                foreach (var ins in Carrega(met, il, g.extras)) il.InsertBefore(p, ins);
                il.InsertBefore(p, il.Create(OpCodes.Ldarg, par));
                foreach (var ins in Carrega(met, il, g.extrasDepois)) il.InsertBefore(p, ins);
                il.InsertBefore(p, il.Create(OpCodes.Call, Mod()));
                il.InsertBefore(p, il.Create(OpCodes.Starg, par));
                if (g.retornarSeZero)
                {
                    if (met.ReturnType.FullName != "System.Void") throw new Exception("retornarSeZero so em metodo void");
                    il.InsertBefore(p, il.Create(OpCodes.Ldarg, par));
                    il.InsertBefore(p, il.Create(OpCodes.Brtrue, p));
                    il.InsertBefore(p, il.Create(OpCodes.Ret));
                }
                break;
            }
            case "trocar-chamada":
            {
                int n = 0;
                foreach (var ins in met.Body.Instructions)
                    if ((ins.OpCode == OpCodes.Call || ins.OpCode == OpCodes.Callvirt) && ins.Operand is MethodReference r && r.Name == g.alvo
                        && r.Parameters.Count == 0 && !r.HasThis)
                    { ins.OpCode = OpCodes.Ldc_I4; ins.Operand = g.valor; n++; }
                if (n == 0 || (g.esperado >= 0 && n != g.esperado)) throw new Exception($"{n} chamada(s) a {g.alvo} (esperado {g.esperado})");
                break;
            }
            default: throw new Exception("tipo de gancho desconhecido");
        }
        met.Body.OptimizeMacros();
        Console.WriteLine($"ok  {g.tipo,-15} {tipo.Name}.{met.Name} -> {g.chamar ?? g.alvo}");
    }

    static IEnumerable<TypeDefinition> TodosTipos(TypeDefinition t) => new[] { t }.Concat(t.NestedTypes.SelectMany(TodosTipos));

    static IEnumerable<Instruction> Carrega(MethodDefinition met, ILProcessor il, List<string> args)
    {
        foreach (var s in args ?? new())
        {
            if (s == "this") yield return il.Create(OpCodes.Ldarg_0);
            else if (s.StartsWith("this."))
            {
                var f = met.DeclaringType.Fields.SingleOrDefault(x => x.Name == s.Substring(5)) ?? throw new Exception("campo nao achado: " + s);
                yield return il.Create(OpCodes.Ldarg_0);
                yield return il.Create(OpCodes.Ldfld, f);
            }
            else if (s.StartsWith("arg:"))
            {
                var par = met.Parameters.SingleOrDefault(x => x.Name == s.Substring(4)) ?? throw new Exception("parametro nao achado: " + s);
                yield return il.Create(OpCodes.Ldarg, par);
            }
            else throw new Exception("arg invalido: " + s);
        }
    }
}


