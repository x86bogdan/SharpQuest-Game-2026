# Content

Your game's data: enemies, towers, items, rooms, animatronics — as JSON files, one list per file.

Unity reads them as `TextAsset`s (drag one onto a field of a MonoBehaviour, then pass `asset.text`
to your loader in Game.Core). The course's tests read the very same files, through your wiring.
So a file here is the single source of truth: never keep a second copy of your content anywhere else.
