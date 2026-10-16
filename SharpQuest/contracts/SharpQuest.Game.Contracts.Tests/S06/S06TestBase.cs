using System.Linq.Expressions;
using System.Reflection;
using SharpQuest.Game.Contracts.Grading;
using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S06;

/// <summary>Shared by the S06 event tests: the wiring, the event it points at, and spies of any delegate type.</summary>
public abstract class S06TestBase
{
    private IEventWiring? _wiring;

    protected IEventWiring Wiring => _wiring ??= GameCode.Wiring<IEventWiring>("EventWiring<YourPublisher>");

    protected Type PublisherType => Wiring.PublisherType;

    /// <summary>The event under test: the only public instance event, or the one EventName names.</summary>
    protected EventInfo Event
    {
        get
        {
            var events = PublisherType.GetEvents(BindingFlags.Public | BindingFlags.Instance);
            if (events.Length == 0)
            {
                var statics = PublisherType.GetEvents(BindingFlags.Public | BindingFlags.Static);
                var fields = PublisherType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                    .Where(f => typeof(Delegate).IsAssignableFrom(f.FieldType)).ToList();
                Assert.Fail(statics.Length > 0
                    ? $"{PublisherType.Name}.{statics[0].Name} is declared 'static', so every {PublisherType.Name} in the game would share " +
                      "one subscriber list. Make it an instance member."
                    : fields.Count > 0
                        ? $"{PublisherType.Name}.{fields[0].Name} is a public delegate field, not an event. Add the 'event' keyword: " +
                          "then only the class itself can raise it or clear the list."
                        : $"{PublisherType.Name} declares no public event.");
            }

            var name = Wiring.EventName;
            if (name is null)
            {
                if (events.Length > 1)
                    Assert.Fail($"{PublisherType.Name} declares {events.Length} events ({string.Join(", ", events.Select(e => e.Name))}). " +
                                "Say which one to check: override EventName in your wiring.");
                return events[0];
            }
            return events.FirstOrDefault(e => e.Name == name)
                   ?? Fail<EventInfo>($"EventName is \"{name}\", but {PublisherType.Name} has no public event by that name " +
                                      $"({string.Join(", ", events.Select(e => e.Name))}).");
        }
    }

    protected object NewPublisher()
    {
        var publisher = Wiring.Create();
        Assert.True(publisher is not null, "Create() returned null.");
        return publisher!;
    }

    protected void Trigger(object publisher)
    {
        var ex = Record.Exception(() => Wiring.Trigger(publisher));
        if (ex is not null) Assert.Fail($"Trigger threw {ex.GetType().Name}: {ex.Message}");
    }

    /// <summary>Records every call a subscriber gets, whatever the event's delegate type.</summary>
    protected sealed class Spy
    {
        public List<object?[]> Calls { get; } = [];

        public int Count => Calls.Count;

        public Delegate Handler { get; }

        public Spy(Type delegateType, Action? also = null)
        {
            var invoke = delegateType.GetMethod("Invoke")!;
            if (invoke.ReturnType != typeof(void))
                Assert.Fail($"The event's delegate ({delegateType.Name}) returns {invoke.ReturnType.Name}. An event's handlers return nothing: use Action<...> or EventHandler<...>.");

            var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var args = Expression.NewArrayInit(typeof(object), parameters.Select(p => Expression.Convert(p, typeof(object))));
            var record = Expression.Call(Expression.Constant(this), typeof(Spy).GetMethod(nameof(Record), BindingFlags.NonPublic | BindingFlags.Instance)!, args);
            _also = also;
            Handler = Expression.Lambda(delegateType, record, parameters).Compile();
        }

        private readonly Action? _also;

        private void Record(object?[] args)
        {
            Calls.Add(args);
            _also?.Invoke();
        }
    }

    protected static T Fail<T>(string message)
    {
        Assert.Fail(message);
        return default!;
    }
}
