using SharpQuest.Game.Tests.Support;

namespace SharpQuest.Game.Tests.S06;

/// <summary>Session 6: one publisher in Game.Core, several subscribers, and the unsubscribe that actually works.</summary>
public class EventTests : S06TestBase
{
    [Fact]
    public void Wiring_class_can_be_created()
    {
        Assert.NotNull(Wiring);
    }

    [Fact]
    public void The_publisher_is_in_game_core_and_declares_an_event()
    {
        Assert.True(GameCode.IsGameCore(PublisherType), $"{PublisherType.FullName} isn't in Game.Core. The rules raise the event; Unity only listens.");
        Assert.NotNull(Event);
    }

    [Fact]
    public void Triggering_it_tells_the_subscriber_once()
    {
        var publisher = NewPublisher();
        var spy = new Spy(Event.EventHandlerType!);
        Event.AddEventHandler(publisher, spy.Handler);

        Trigger(publisher);

        Assert.True(spy.Count == 1, $"The subscriber was called {spy.Count} times for one Trigger, not once.");
    }

    [Fact]
    public void Two_subscribers_both_hear_it_in_the_order_they_subscribed()
    {
        var publisher = NewPublisher();
        var order = new List<string>();
        Event.AddEventHandler(publisher, new Spy(Event.EventHandlerType!, () => order.Add("first")).Handler);
        Event.AddEventHandler(publisher, new Spy(Event.EventHandlerType!, () => order.Add("second")).Handler);

        Trigger(publisher);

        Assert.Equal("first,second", string.Join(",", order));
    }

    [Fact]
    public void Unsubscribing_stops_that_one_and_leaves_the_other()
    {
        var leaving = new Spy(Event.EventHandlerType!);
        var staying = new Spy(Event.EventHandlerType!);
        var publisher = NewPublisher();
        Event.AddEventHandler(publisher, leaving.Handler);
        Event.AddEventHandler(publisher, staying.Handler);

        Event.RemoveEventHandler(publisher, leaving.Handler);
        Trigger(publisher);

        Assert.True(leaving.Count == 0, $"The unsubscribed handler was still called {leaving.Count} time(s). '-=' has to detach it.");
        Assert.True(staying.Count == 1, $"The other handler was called {staying.Count} times, not once. Unsubscribing one must not detach the rest.");
    }

    [Fact]
    public void Nobody_subscribed_is_not_a_crash()
    {
        var publisher = NewPublisher();

        var ex = Record.Exception(() => Wiring.Trigger(publisher));

        Assert.True(ex is null,
            $"Triggering with nobody subscribed threw {ex?.GetType().Name}. An event with nobody attached is null: raise it with '?.Invoke(...)'. " +
            "In Unity that's every scene where the HUD isn't loaded.");
    }

    [Fact]
    public void Everyone_unsubscribed_is_not_a_crash_either()
    {
        var publisher = NewPublisher();
        var spy = new Spy(Event.EventHandlerType!);
        Event.AddEventHandler(publisher, spy.Handler);
        Event.RemoveEventHandler(publisher, spy.Handler);

        var ex = Record.Exception(() => Wiring.Trigger(publisher));

        Assert.True(ex is null, $"Triggering after the last subscriber left threw {ex?.GetType().Name}. Raise it with '?.Invoke(...)'.");
        Assert.Equal(0, spy.Count);
    }

    [Fact]
    public void The_same_handler_twice_is_called_twice()
    {
        var publisher = NewPublisher();
        var spy = new Spy(Event.EventHandlerType!);
        Event.AddEventHandler(publisher, spy.Handler);
        Event.AddEventHandler(publisher, spy.Handler);

        Trigger(publisher);

        Assert.True(spy.Count == 2, $"A handler subscribed twice was called {spy.Count} time(s), not twice. An event is a list, not a set.");
    }

    [Fact]
    public void Something_the_game_refuses_tells_nobody()
    {
        var publisher = NewPublisher();
        var spy = new Spy(Event.EventHandlerType!);
        Event.AddEventHandler(publisher, spy.Handler);
        Trigger(publisher);

        Record.Exception(() => Wiring.TriggerRejected(publisher));

        Assert.True(spy.Count == 1,
            $"TriggerRejected raised the event (the subscriber was called {spy.Count} times in all). Check first, then change the state, " +
            "then raise: an enemy dies once.");
    }

    [Fact]
    public void Subscribers_of_one_publisher_do_not_hear_another()
    {
        var mine = NewPublisher();
        var theirs = NewPublisher();
        var spy = new Spy(Event.EventHandlerType!);
        Event.AddEventHandler(mine, spy.Handler);

        Trigger(theirs);

        Assert.True(spy.Count == 0,
            "A subscriber of one publisher heard another one. Is the event, or the list behind it, static? Each instance needs its own.");
    }
}
