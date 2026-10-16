// MANAGED FILE: sq init and sq update refresh it. Don't edit it, don't delete it.
//
// Lets records and init-only properties ({ get; init; }) compile in Unity. Unity compiles C# 9
// against .NET Standard 2.1, which is missing this one marker type; declaring it is the fix
// Unity's own manual gives. It is internal, so nothing outside Game.Core ever sees it.

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
