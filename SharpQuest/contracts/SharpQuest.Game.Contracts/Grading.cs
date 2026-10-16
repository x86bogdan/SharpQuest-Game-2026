namespace SharpQuest.Game.Contracts.Grading;

// What the course's tests see of your wiring. You never implement these yourself: the base classes
// (ContentWiring<T>, KindsWiring<T>, ...) do it for you, turning your types into plain objects the
// tests can handle without knowing your class names. The per-session interfaces live with their session.

/// <summary>Every wiring base implements this, so the tests can name the session in their messages.</summary>
public interface IWiring
{
    /// <summary>The base class to derive from, as students write it, e.g. "ContentWiring&lt;YourItem&gt;".</summary>
    string BaseName { get; }
}
