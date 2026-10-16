# Wiring

The course's tests need to reach *your* types without knowing their names. You tell them, with one small
class per session in this folder, derived from that session's base class and naming your own type:

| Session | Derive from | Naming |
|---|---|---|
| 2 | `ContentWiring<TItem>` | your content's item type (`Enemy`, `Tower`, `Room`) |
| 3 | `SearchWiring<TItem>` | the items your search looks through |
| 4 | `KindsWiring<TBase>` | your abstract base |
| 5 | `CollectionWiring<TCollection, TItem>` | your collection, and what it holds |
| 6 | `EventWiring<TPublisher>` and `FunctionWiring<TItem>` | the class with your event; the items your function filters |
| 7 | `QueriesWiring<TItem>` | the items your questions are about |
| 8 | `SaveWiring<TState>` | the type that holds a game in progress |
| 9 | `ViewModelWiring<TViewModel>` | your view model |
| 12 | `ReflectionWiring<TItem>` | your items (their kinds get labelled) |

Sessions 10 and 11 (a web API and a database) are optional on the game track: talk to your instructor if you want them.

Each base class's comments (in `../contracts/SharpQuest.Game.Contracts/SNN/`) say exactly what the tests expect.
One class per base, a public constructor with no parameters, and every member one line that calls Game.Core.

This folder is yours: `sq update` never touches it. It is not part of your game, and Unity never sees it.
