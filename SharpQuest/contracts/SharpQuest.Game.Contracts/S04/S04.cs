// Session 4 - Kinds: inheritance, polymorphism, pattern matching
// One family of types, one loop that doesn't care which member it holds,
// one switch expression that does.
//
// IMPORTANT: this session ADDS to your game. Nothing from session 2 may break:
// its checks run again every time, and they must stay green.

using SharpQuest.Game.Contracts.Grading;

namespace SharpQuest.Game.Contracts
{
    /// <summary>
    /// Tells the S04 tests about your family of kinds. Write one class deriving from this in SharpQuest/Wiring/,
    /// naming YOUR abstract base:
    /// <code>
    /// public sealed class Kinds : KindsWiring&lt;Enemy&gt;
    /// {
    ///     public override IReadOnlyList&lt;Enemy&gt; Samples() =&gt; new Content().LoadContent();
    ///     public override string Describe(Enemy item) =&gt; item.Describe();
    ///     public override string Classify(Enemy item) =&gt; EnemyLabels.Classify(item);
    ///     public override IReadOnlyList&lt;string&gt; DescribeAll(IEnumerable&lt;Enemy&gt; items) =&gt; Roster.DescribeAll(items);
    /// }
    /// </code>
    ///
    /// <para>The shape the tests expect, all in Game.Core:</para>
    /// <list type="bullet">
    /// <item><b>The base</b> (<typeparamref name="TBase"/>): an <c>abstract class</c> declaring at least one
    ///   <c>abstract</c> or <c>virtual</c> member of its own. Any signature: in a game it's usually a rule that
    ///   computes something (<c>int DamageTaken(int amount, DamageType type)</c>, <c>bool CanPass(Gate gate)</c>,
    ///   <c>Room NextMove(Night night, Random dice)</c>), not just a description.</item>
    /// <item><b>Two or more kinds</b>: concrete classes deriving from the base, each <c>override</c>-ing at least one
    ///   of those members with a rule of its own.</item>
    /// </list>
    /// </summary>
    public abstract class KindsWiring<TBase> : IKindsWiring where TBase : class
    {
        /// <summary>
        /// Items of at least two different kinds. Your content is the natural source (the S02 wiring's
        /// <c>LoadContent()</c>); a few made by hand is fine too.
        /// </summary>
        public abstract IReadOnlyList<TBase> Samples();

        /// <summary>
        /// What the item's kind does, as text, so the tests can compare kinds. Call your polymorphic member
        /// through the base type and let the override decide, e.g. <c>item.Describe()</c> or
        /// <c>$"{item.DamageTaken(10, DamageType.Fire)} damage from fire"</c>.
        /// Never empty. Two different kinds must answer differently.
        /// </summary>
        public abstract string Describe(TBase item);

        /// <summary>
        /// A short label saying what the item is, written as a <b>switch expression</b> over type patterns:
        /// <list type="bullet">
        /// <item>items of the same kind get the same label; different kinds get different labels;</item>
        /// <item><c>null</c> is a programming error: <see cref="ArgumentNullException"/> (the <c>null =&gt; throw</c> arm);</item>
        /// <item>keep a <c>_ =&gt;</c> arm for a kind nobody has written yet.</item>
        /// </list>
        /// It answers a different question from <see cref="Describe"/>: Describe is the item speaking for itself (the
        /// override); Classify is the outside world sorting it (the switch).
        /// </summary>
        public abstract string Classify(TBase item);

        /// <summary>
        /// One description per item, in order, each equal to <see cref="Describe"/> of that item.
        /// Your code in Game.Core, written as a single loop over the base type: the loop must not know which kind it
        /// is holding. Nothing in, nothing out.
        /// </summary>
        public abstract IReadOnlyList<string> DescribeAll(IEnumerable<TBase> items);

        string IWiring.BaseName => "KindsWiring<YourBase>";
        Type IKindsWiring.BaseType => typeof(TBase);
        IReadOnlyList<object> IKindsWiring.Samples() => Samples();
        string IKindsWiring.Describe(object item) => Describe((TBase)item);
        string IKindsWiring.Classify(object? item) => Classify((TBase)item!);
        IReadOnlyList<string> IKindsWiring.DescribeAll(IEnumerable<object> items) => DescribeAll(items.Cast<TBase>());
    }
}

namespace SharpQuest.Game.Contracts.Grading
{
    /// <summary>The tests' view of <see cref="KindsWiring{TBase}"/>. You don't implement this: the base class does.</summary>
    public interface IKindsWiring : IWiring
    {
        Type BaseType { get; }
        IReadOnlyList<object> Samples();
        string Describe(object item);
        string Classify(object? item);
        IReadOnlyList<string> DescribeAll(IEnumerable<object> items);
    }
}
