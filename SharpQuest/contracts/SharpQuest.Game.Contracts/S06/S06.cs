// Session 6 - Functions as values & events
// Two ideas that look unrelated and are the same idea: a function can be a value you pass around,
// and an event is a list of functions other people gave you, called when something happens.
//
// In a game this is how the rules talk to the screen: Game.Core raises "an enemy died", and the HUD,
// the sound and the loot drop - MonoBehaviours in Assets/Game/Unity/ - each subscribe on their own.
// The rules never know who is listening. Nothing from sessions 2-5 may break.

using SharpQuest.Game.Contracts.Grading;

namespace SharpQuest.Game.Contracts
{
    /// <summary>
    /// Tells the S06 tests about your event. Write one class deriving from this in SharpQuest/Wiring/,
    /// naming the type that declares the event:
    /// <code>
    /// public sealed class Events : EventWiring&lt;Wave&gt;
    /// {
    ///     public override Wave Create() =&gt; new Wave(new Content().LoadContent());
    ///     public override void Trigger(Wave wave) =&gt; wave.Hit("grunt-01", 999, DamageType.Physical);
    ///     public override void TriggerRejected(Wave wave) =&gt; wave.Hit("grunt-01", 999, DamageType.Physical);
    /// }
    /// </code>
    ///
    /// <para>What the tests expect of your publisher, in Game.Core:</para>
    /// <list type="bullet">
    /// <item>it declares a public <c>event</c>: the keyword, not a plain <c>Action</c> field, and not <c>static</c>;</item>
    /// <item><see cref="Trigger"/> raises it exactly once;</item>
    /// <item>something the game refuses (<see cref="TriggerRejected"/>) raises nothing;</item>
    /// <item>with nobody subscribed, nothing crashes: that's what <c>?.Invoke</c> is for.</item>
    /// </list>
    /// </summary>
    public abstract class EventWiring<TPublisher> : IEventWiring where TPublisher : class
    {
        /// <summary>A fresh publisher, ready for the thing to happen: a wave with enemies in it, an empty inventory, a night at 12 AM.</summary>
        public abstract TPublisher Create();

        /// <summary>Make it happen once: kill an enemy, pick up an item, move an animatronic. Your event is raised exactly once.</summary>
        public abstract void Trigger(TPublisher publisher);

        /// <summary>
        /// Called right after <see cref="Trigger"/>, on the same publisher: something your game refuses, so the event must
        /// NOT be raised. Killing the enemy that just died, picking up the item that's already in the bag.
        /// Throwing is allowed; raising the event is not.
        /// </summary>
        public abstract void TriggerRejected(TPublisher publisher);

        /// <summary>The name of the event to check. Needed only when the publisher declares more than one.</summary>
        public virtual string? EventName => null;

        string IWiring.BaseName => "EventWiring<YourPublisher>";
        Type IEventWiring.PublisherType => typeof(TPublisher);
        object IEventWiring.Create() => Create();
        void IEventWiring.Trigger(object publisher) => Trigger((TPublisher)publisher);
        void IEventWiring.TriggerRejected(object publisher) => TriggerRejected((TPublisher)publisher);
    }

    /// <summary>
    /// Tells the S06 tests about your own method that takes a function. Write one class deriving from this:
    /// <code>
    /// public sealed class Functions : FunctionWiring&lt;Enemy&gt;
    /// {
    ///     public override IReadOnlyList&lt;Enemy&gt; Items() =&gt; new Content().LoadContent();
    ///     public override IReadOnlyList&lt;Enemy&gt; Where(IReadOnlyList&lt;Enemy&gt; items, Func&lt;Enemy, bool&gt; keep) =&gt; Targeting.Where(items, keep);
    /// }
    /// </code>
    /// The method itself lives in Game.Core and takes the caller's rule as a parameter: a tower's targeting rule,
    /// a filter for the map, a condition for an achievement. The caller brings the rule; your code runs it.
    /// </summary>
    public abstract class FunctionWiring<TItem> : IFunctionWiring where TItem : class
    {
        /// <summary>At least three items to work on. Your content is fine.</summary>
        public abstract IReadOnlyList<TItem> Items();

        /// <summary>
        /// The items <paramref name="keep"/> says yes to, in the order given. Call the function once per item and don't
        /// second-guess it. A null function is a programming error: <see cref="ArgumentNullException"/>.
        /// </summary>
        public abstract IReadOnlyList<TItem> Where(IReadOnlyList<TItem> items, Func<TItem, bool> keep);

        string IWiring.BaseName => "FunctionWiring<YourItem>";
        Type IFunctionWiring.ItemType => typeof(TItem);
        IReadOnlyList<object> IFunctionWiring.Items() => Items();
        IReadOnlyList<object> IFunctionWiring.Where(IReadOnlyList<object> items, Func<object, bool>? keep) =>
            Where(items.Cast<TItem>().ToList(), keep is null ? null! : item => keep(item));
    }
}

namespace SharpQuest.Game.Contracts.Grading
{
    /// <summary>The tests' view of <see cref="EventWiring{TPublisher}"/>. You don't implement this: the base class does.</summary>
    public interface IEventWiring : IWiring
    {
        Type PublisherType { get; }
        string? EventName { get; }
        object Create();
        void Trigger(object publisher);
        void TriggerRejected(object publisher);
    }

    /// <summary>The tests' view of <see cref="FunctionWiring{TItem}"/>. You don't implement this: the base class does.</summary>
    public interface IFunctionWiring : IWiring
    {
        Type ItemType { get; }
        IReadOnlyList<object> Items();
        IReadOnlyList<object> Where(IReadOnlyList<object> items, Func<object, bool>? keep);
    }
}
