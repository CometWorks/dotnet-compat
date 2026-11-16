using System;
using System.Linq;
using System.Threading.Tasks;
using HarmonyLib;
using Shared.Patches.Miscellaneous;
using VRage.Utils;

namespace StringHashCollisionTests;

// Registers two strings with the same MyStringHash hash against the game's VRage.dll.
//
//   vanilla: without the patch the second registration must hang (the bug, SE-1606)
//   patched: with the patch it must throw an ArgumentException naming the first string, and the
//            tables must keep working (on .NET Framework vanilla throws the same way)
//
// Prints PASS or FAIL lines and exits with 0 only if every check passed. Run each mode in its
// own process: the vanilla hang leaves the lock taken for good.
internal static class Program
{
    // Found by brute force: both hash to the same value with MyUtils.GetHash(str, 0)
    private const string Known = "SE1606Collision112400";
    private const string Colliding = "SE1606Collision1029080";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);
    private static int failures;

    private static void Check(bool condition, string what, string detail = "")
    {
        Console.WriteLine(
            $"{(condition ? "PASS" : "FAIL")} {what}{(detail == "" ? "" : " :: " + detail)}"
        );
        if (!condition)
            failures++;
    }

    private static int Main(string[] args)
    {
        var mode = args.FirstOrDefault() ?? "patched";
        if (mode != "vanilla" && mode != "patched")
        {
            Console.WriteLine("Usage: StringHashCollisionTests vanilla|patched");
            return 2;
        }

        Check(
            MyUtils.GetHash(Known, 0) == MyUtils.GetHash(Colliding, 0),
            "the test strings share a hash",
            $"{MyUtils.GetHash(Known, 0)} {MyUtils.GetHash(Colliding, 0)}"
        );

        if (mode == "patched")
        {
            new Harmony("StringHashCollisionTests").PatchCategory(
                typeof(MyStringHashPatch).Assembly,
                "Finish"
            );
        }

        var known = MyStringHash.GetOrCompute(Known);
        var colliding = Task.Run(() => MyStringHash.GetOrCompute(Colliding));
        var returned = Task.WhenAny(colliding, Task.Delay(Timeout)).Result == colliding;

        if (mode == "vanilla")
        {
            Check(
                !returned,
                "vanilla hangs on the colliding string (the bug)",
                returned ? $"{colliding.Status}" : ""
            );
            return Finish();
        }

        Check(
            returned,
            $"registering the colliding string returns within {Timeout.TotalSeconds} s"
        );
        if (!returned)
            return Finish();

        var error = colliding.Exception?.InnerException;
        Check(
            error is ArgumentException
                && error.Message.Contains(Known)
                && error.Message.Contains(Colliding),
            "it throws an ArgumentException naming both strings",
            error?.ToString().Split('\n')[0] ?? $"returned {colliding.Status}"
        );

        var next = Task.Run(() => MyStringHash.GetOrCompute("SE1606AfterTheCollision"));
        Check(
            next.Wait(Timeout) && next.Result.String == "SE1606AfterTheCollision",
            "the next new string still registers"
        );
        Check(
            MyStringHash.TryGet(Known) == known
                && MyStringHash.TryGet(Colliding) == MyStringHash.NullOrEmpty,
            "the tables are unchanged by the collision"
        );
        Check(
            MyStringHash.GetOrCompute(Known) == known,
            "registering a known string again still works"
        );

        return Finish();
    }

    private static int Finish()
    {
        Console.WriteLine(failures == 0 ? "ALL PASSED" : $"{failures} FAILED");
        // A hung vanilla registration keeps a thread pool thread waiting forever
        Environment.Exit(failures == 0 ? 0 : 1);
        return 0;
    }
}
