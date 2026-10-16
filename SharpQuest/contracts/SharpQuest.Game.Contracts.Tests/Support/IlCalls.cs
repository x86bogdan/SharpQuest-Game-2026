using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace SharpQuest.Game.Tests.Support;

/// <summary>
/// What a method calls, read from its compiled IL. Nothing is executed. Starting from one method (usually a
/// wiring member, or the method behind a delegate), it follows every call into code YOU wrote, in Game.Core
/// and in SharpQuest/Wiring/: helpers, lambdas, local functions, and the bodies of async and iterator methods
/// (the compiler moves those into a hidden MoveNext). Calls into anything else are recorded, not followed.
///
/// <para>Used by S07 (which LINQ verbs your queries use), S08 (real async file I/O, no blocking) and S09
/// (a view model that never blocks).</para>
/// </summary>
public static class IlCalls
{
    /// <summary>One call: who made it, and what it called.</summary>
    public sealed record Call(MethodBase Caller, MethodBase Callee);

    private static readonly Dictionary<short, OpCode> OpCodesByValue =
        typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => (OpCode)f.GetValue(null)!)
            .GroupBy(o => o.Value).ToDictionary(g => g.Key, g => g.First());

    /// <summary>Is this method's code the student's own: Game.Core or the wiring?</summary>
    public static bool IsStudentCode(MethodBase method)
    {
        var assembly = method.Module.Assembly;
        return assembly == GameCode.Core || assembly == GameCode.WiringAssembly;
    }

    /// <summary>Every call reachable from <paramref name="roots"/> through the student's own code.</summary>
    public static IReadOnlyList<Call> From(params MethodBase[] roots)
    {
        var calls = new List<Call>();
        var seen = new HashSet<MethodBase>();
        var queue = new Queue<MethodBase>();

        void Visit(MethodBase m)
        {
            if (seen.Add(m)) queue.Enqueue(m);
            // An async or iterator method's real body lives in a compiler-made class: follow it too.
            if (m.GetCustomAttribute<StateMachineAttribute>() is { StateMachineType: { } machine }
                && machine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) is { } moveNext
                && seen.Add(moveNext))
                queue.Enqueue(moveNext);
        }

        foreach (var root in roots) Visit(root);
        while (queue.Count > 0)
        {
            var method = queue.Dequeue();
            foreach (var callee in Callees(method))
            {
                calls.Add(new Call(method, callee));
                if (IsStudentCode(callee)) Visit(Definition(callee));
            }
        }
        return calls;
    }

    /// <summary>The full name of a callee's type, without generic arguments: "System.Threading.Tasks.Task`1".</summary>
    public static string OwnerName(MethodBase method)
    {
        var type = method.DeclaringType;
        if (type is null) return "";
        if (type.IsGenericType) type = type.GetGenericTypeDefinition();
        return type.FullName ?? type.Name;
    }

    /// <summary>A call to a Task's .Result, .Wait(), or GetAwaiter().GetResult(): blocking a thread until it finishes.</summary>
    public static string? Blocking(MethodBase callee)
    {
        var owner = OwnerName(callee);
        if (owner is "System.Threading.Tasks.Task" or "System.Threading.Tasks.Task`1"
                  or "System.Threading.Tasks.ValueTask" or "System.Threading.Tasks.ValueTask`1")
            return callee.Name switch
            {
                "get_Result" => ".Result",
                "Wait" or "WaitAll" or "WaitAny" => $".{callee.Name}()",
                _ => null,
            };
        if (owner is "System.Runtime.CompilerServices.TaskAwaiter" or "System.Runtime.CompilerServices.TaskAwaiter`1"
                  or "System.Runtime.CompilerServices.ValueTaskAwaiter" or "System.Runtime.CompilerServices.ValueTaskAwaiter`1"
                  or "System.Runtime.CompilerServices.ConfiguredTaskAwaitable+ConfiguredTaskAwaiter"
                  or "System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter")
            // The compiler's own awaits call GetResult too, but from inside a MoveNext, after checking IsCompleted.
            return callee.Name == "GetResult" ? ".GetAwaiter().GetResult()" : null;
        return null;
    }

    /// <summary>Blocking calls, but not the ones the compiler writes for an await (those live in MoveNext).</summary>
    public static IReadOnlyList<string> BlockingIn(IEnumerable<Call> calls) =>
        calls.Where(c => Blocking(c.Callee) is not null)
             .Where(c => !(c.Caller.Name == "MoveNext" && c.Caller.DeclaringType?.IsDefined(typeof(CompilerGeneratedAttribute)) == true
                           && Blocking(c.Callee) == ".GetAwaiter().GetResult()"))
             .Select(c => $"{Where(c.Caller)} uses {Blocking(c.Callee)}")
             .Distinct()
             .ToList();

    /// <summary>Where a call was written, as a student would recognise it: "SaveGame.LoadAsync", not "&lt;LoadAsync&gt;d__3.MoveNext".</summary>
    public static string Where(MethodBase method)
    {
        var type = method.DeclaringType;
        var name = method.Name;
        if (type is not null && type.Name.StartsWith('<'))
        {
            var close = type.Name.IndexOf('>');
            if (close > 1 && type.Name.Length > 2 && !type.Name.StartsWith("<>")) name = type.Name[1..close];
            while (type.DeclaringType is not null && type.Name.StartsWith('<')) type = type.DeclaringType;
        }
        if (name.StartsWith('<') && name.IndexOf('>') is var end and > 1) name = name[1..end];
        return type is null ? name : $"{type.Name}.{name}";
    }

    private static MethodBase Definition(MethodBase method)
    {
        // A call to a generic method of your own (or a method of your own generic type) is to an instantiation:
        // its IL is the generic definition's.
        try
        {
            if (method is MethodInfo { IsGenericMethod: true, IsGenericMethodDefinition: false } generic)
                return generic.GetGenericMethodDefinition();
            if (method.DeclaringType is { IsGenericType: true, IsGenericTypeDefinition: false } owner)
                return MethodBase.GetMethodFromHandle(method.MethodHandle, owner.GetGenericTypeDefinition().TypeHandle) ?? method;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or NotSupportedException) { }
        return method;
    }

    private static IEnumerable<MethodBase> Callees(MethodBase method)
    {
        byte[]? il;
        try { il = method.GetMethodBody()?.GetILAsByteArray(); }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException or BadImageFormatException) { il = null; }
        if (il is null) yield break;

        var typeArgs = method.DeclaringType is { IsGenericType: true } t ? t.GetGenericArguments() : null;
        var methodArgs = method is MethodInfo { IsGenericMethod: true } && !method.IsConstructor ? method.GetGenericArguments() : null;

        var i = 0;
        while (i < il.Length)
        {
            short value = il[i++];
            if (value == 0xFE && i < il.Length) value = unchecked((short)(0xFE00 | il[i++]));
            if (!OpCodesByValue.TryGetValue(value, out var op)) yield break;

            switch (op.OperandType)
            {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar: i += 1; break;
                case OperandType.InlineVar: i += 2; break;
                case OperandType.InlineI8 or OperandType.InlineR: i += 8; break;
                case OperandType.InlineSwitch:
                    var n = BitConverter.ToInt32(il, i);
                    i += 4 + 4 * n;
                    break;
                case OperandType.InlineMethod or OperandType.InlineTok:
                    var token = BitConverter.ToInt32(il, i);
                    i += 4;
                    MethodBase? callee = null;
                    try { callee = method.Module.ResolveMember(token, typeArgs, methodArgs) as MethodBase; }
                    catch (Exception e) when (e is ArgumentException or BadImageFormatException or TypeLoadException
                                                  or FileNotFoundException or MissingMethodException) { }
                    if (callee is not null) yield return callee;
                    break;
                default: i += 4; break;   // InlineField, InlineType, InlineString, InlineSig, InlineI, InlineBrTarget, ShortInlineR
            }
        }
    }
}
