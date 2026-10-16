# FCPL lab · game track

Your capstone is a game, built in Unity. This repository holds both: your Unity project, and a small
`SharpQuest/` folder that checks your C# the way the labs check everyone else's.

**The one rule that makes it work:** your game's *rules* live in their own folder, `Assets/Game/Core/`,
and never use `UnityEngine`. Your MonoBehaviours draw, play sounds and read input; the rules decide.
Unity recommends this split anyway (it's what assembly definitions are for), and it's what lets the
course's tests run your rules without opening Unity.

## Setting up (once)

1. **Use this template** → create a **private** repository → *Settings → Collaborators* → add your instructor.
2. Clone it (GitHub Desktop is fine).
3. **Unity Hub** → *New project* → Unity **6000.6.6f1** → a 2D or 3D template.
   *Location*: the folder you cloned into. *Project name*: `Game`. Create it, and close Unity once it has opened.
4. In a terminal at the top of the repository: **`sq init`** (Linux/macOS: `sh sq.sh init`).
   It makes the folders below, the three assembly definitions, and adds the JSON package to your project.
5. Open the project in Unity again and let it import.

Already started your game somewhere else? Copy this repository's files into your project's repository
(everything except your own `Game/`), then `sq init <your Unity project's folder>`.

## Where things go

| Folder | What | Unity assembly |
|---|---|---|
| `Game/Assets/Game/Core/` | **Your rules**: enemies, towers, rooms, the maths. Plain C#, no `UnityEngine` | `Game.Core` (No Engine References) |
| `Game/Assets/Game/Unity/` | Your MonoBehaviours: they call Core and show what it decided | `Game.Unity` |
| `Game/Assets/Game/Tests/Core/` | Your tests (NUnit). They run in Unity's Test Runner *and* in `sq test` | `Game.Core.Tests` (EditMode) |
| `Game/Assets/Game/Content/` | Your content as JSON. Unity loads it as a `TextAsset`; the tests read the same file | — |
| `SharpQuest/Wiring/` | One small class per session telling the tests which of your types to look at | not in Unity |
| `SharpQuest/contracts/` | The course's tests. **Managed**: read them, don't edit them | not in Unity |

Everything else in `SharpQuest/` is managed too: `sq update` replaces it.

**Which solution to open:** for your game, the one Unity makes (*Assets → Open C# Project* in Unity). `SharpQuest/SharpQuest.Game.slnx` is only for the wiring and for reading the course's tests.

## Rules for Core (the build tells you when you break one)

- **No `UnityEngine`.** Need the time? Take `float deltaTime` as a parameter. Need randomness? Take a `System.Random` (seeded in tests). Need a position? A `record` of your own.
- **C# 9**, because that's Unity's compiler: `namespace X { … }` with braces, constructors instead of primary constructors, `new[] { … }` instead of `[ … ]`.
- **No implicit usings**: write `using System;` and `using System.Collections.Generic;` yourself.
- **.NET Standard 2.1**: no `DateOnly`, no `System.Text.Json`. JSON is Newtonsoft (`using Newtonsoft.Json;`).
- **Zero nullable warnings**, as in the labs.
- **Properties, not public fields**, for what your content has: `public int Health { get; }`.

## Every session

```
sq update                 # the new session's contract
                          # write your code in Core, then the session's wiring class in SharpQuest/Wiring/
sq test                   # builds Core and your tests outside Unity, runs every check
git commit + push         # the CI run on GitHub is the record your score is read from
```

`sq test 04` runs only session 4's checks. A session shows as *not started* until `SharpQuest/Wiring/` has a class for it.

## The wiring

Each session gives you a base class to derive from, naming **your** type. Session 2, for a tower defence:

```csharp
using Lanes;
using SharpQuest.Game.Contracts;

public sealed class Content : ContentWiring<Enemy>
{
    public override string ContentFile => "enemies.json";
    public override IReadOnlyList<Enemy> ParseContent(string json) => EnemyCatalog.Parse(json);
    public override string IdOf(Enemy item) => item.Id;
    public override string NameOf(Enemy item) => item.Name;
}
```

Every member is one line that calls your code in Core. If it needs more, that logic belongs in Core.
The wiring isn't part of your game and Unity never sees it, so here (and only here) any C# is fine.

## What's graded

The same rubric as everyone: Contract 40% (these tests), Change 30% (a live change to your code), Comprehension 30% (two "why" questions).
Checkpoints are as on the project route. The game itself — the scenes, the art, the feel — is yours, and it's what you show at the end; what's scored is the C# underneath it.
