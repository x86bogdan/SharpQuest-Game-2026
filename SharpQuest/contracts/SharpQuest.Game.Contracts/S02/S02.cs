// Session 2 - Types & content
// Your game's content as types of your own, loaded from the same JSON file Unity loads.
//
// The nouns are yours: enemies, towers, rooms, abilities, animatronics.
// Everything lives in Game.Core (Assets/Game/Core/). This file only tells the tests where to look.

using SharpQuest.Game.Contracts.Grading;

namespace SharpQuest.Game.Contracts
{
    /// <summary>
    /// Tells the S02 tests what your content is and how your game reads it.
    /// Write one class deriving from this in SharpQuest/Wiring/, naming YOUR item type:
    /// <code>
    /// public sealed class Content : ContentWiring&lt;Enemy&gt;
    /// {
    ///     public override string ContentFile =&gt; "enemies.json";
    ///     public override IReadOnlyList&lt;Enemy&gt; ParseContent(string json) =&gt; EnemyCatalog.Parse(json);
    ///     public override string IdOf(Enemy item) =&gt; item.Id;
    ///     public override string NameOf(Enemy item) =&gt; item.Name;
    /// }
    /// </code>
    /// Each member is one line that calls your code in Game.Core. If it's more than that, the logic belongs in Game.Core.
    /// </summary>
    public abstract class ContentWiring<TItem> : IContentWiring where TItem : class
    {
        /// <summary>
        /// Your content file, inside your content folder (Assets/Game/Content/), e.g. "enemies.json".
        /// The game loads it in Unity as a TextAsset; the tests read the same file.
        /// </summary>
        public abstract string ContentFile { get; }

        /// <summary>
        /// Your loader: the items described by this JSON text. The same method your game calls in Unity.
        /// Text that isn't content (empty, broken JSON, a number, null) gives an empty list and never throws:
        /// a game that crashes on a bad file is worse than one that says the file is empty.
        /// </summary>
        public abstract IReadOnlyList<TItem> ParseContent(string json);

        /// <summary>The item's Id: never empty, unique in your content, never changes.</summary>
        public abstract string IdOf(TItem item);

        /// <summary>What a player would call it. Never empty.</summary>
        public abstract string NameOf(TItem item);

        /// <summary>Everything in <see cref="ContentFile"/>, read through <see cref="ParseContent"/>.</summary>
        public IReadOnlyList<TItem> LoadContent() => ParseContent(GameFiles.ReadContent(ContentFile));

        string IWiring.BaseName => "ContentWiring<YourItem>";
        Type IContentWiring.ItemType => typeof(TItem);
        IReadOnlyList<object> IContentWiring.LoadContent() => LoadContent();
        IReadOnlyList<object> IContentWiring.ParseContent(string json) => ParseContent(json);
        string IContentWiring.IdOf(object item) => IdOf((TItem)item);
        string IContentWiring.NameOf(object item) => NameOf((TItem)item);
    }
}

namespace SharpQuest.Game.Contracts.Grading
{
    /// <summary>The tests' view of <see cref="ContentWiring{TItem}"/>. You don't implement this: the base class does.</summary>
    public interface IContentWiring : IWiring
    {
        Type ItemType { get; }
        string ContentFile { get; }
        IReadOnlyList<object> LoadContent();
        IReadOnlyList<object> ParseContent(string json);
        string IdOf(object item);
        string NameOf(object item);
    }
}
