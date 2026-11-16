using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using HarmonyLib;
using VRage.Utils;

namespace Shared.Patches.Miscellaneous;

// MyStringHash.GetOrCompute adds a new string to its two tables under the exclusive side of a
// non-reentrant FastResourceLock. When the new string's 32-bit hash already belongs to another
// string, m_hashToString.Add throws a duplicate key ArgumentException.
//
// On .NET Framework that exception's message has no key in it, the lock is released and the
// caller gets the exception. On .NET 10 the message includes the key: Dictionary formats it by
// calling MyStringHash.ToString, which takes the shared side of the same lock, already held
// exclusively by this thread, so it waits forever. A programmable block script calling
// MyDefinitionId.TryParse on such a string stopped the game thread of node after node on the
// Magnetar cluster (SE-1606).
//
// The prefix finds the collision before the original takes the lock and throws the exception
// .NET Framework threw, holding nothing, with both strings in the message. It is logged too, so
// the string can be found: the first 20, then every 1000th, as a script may retry every tick.
[HarmonyPatchCategory("Finish")]
[HarmonyPatch(typeof(MyStringHash), nameof(MyStringHash.GetOrCompute))]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "UnusedMember.Local")]
public static class MyStringHashPatch
{
    private const int FullyLoggedCount = 20;
    private static long collisionCount;

    private static void Prefix(string str)
    {
        if (str == null || MyStringHash.TryGet(str, out _))
            return;

        var hash = MyUtils.GetHash(str, 0);

        // TryGet(int) answers NullOrEmpty for an unknown hash. That is also the answer while the
        // static constructor registers "" itself, before NullOrEmpty is set, so only a hash the
        // table knows is a collision.
        var taken = MyStringHash.TryGet(hash);
        if (taken.m_hash != hash || !MyStringHash.IsKnown(taken))
            return;

        // Definitions load on parallel threads: another one may have registered this very string
        // since the first look
        var other = taken.String;
        if (other == str)
            return;

        var message =
            $"MyStringHash collision: \"{str}\" has the hash {hash} of \"{other}\" and cannot be registered";
        var seen = Interlocked.Increment(ref collisionCount);
        if (seen <= FullyLoggedCount || seen % 1000 == 0)
            MyLog.Default?.WriteLineAndConsole($"[DotNetCompat] {message} (#{seen})");

        throw new ArgumentException(message, nameof(str));
    }
}
