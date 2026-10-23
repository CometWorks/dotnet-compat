using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Shared.Tools;

namespace Shared.Patches.Miscellaneous;

// Cecil prepatch, invoked explicitly from Preloader.Patch.
//
// WorldPositionToInnerKey casts position / ClusterSize to int. On .NET Framework
// an out of range cast gave int.MinValue, on .NET 10 it saturates to int.MaxValue.
// The cell loops in AddOrUpdate, FastQuery and RemoveInternal run
// `for (int i = min; i <= max; i++)`, which never ends once max is int.MaxValue,
// so any safe zone query for an entity beyond 2^31 cells of 5000 m (1.07e13 m from
// the origin) hung the thread: entity removal, turret targeting, damage checks.
// Clamping the cell index to +-(2^31 - 2) keeps every loop finite. It is patched in
// the assembly, not with Harmony, because the method is AggressiveInlining and the
// class is generic.
public static class MyStaticObjectsClusterPrepatch
{
    private const double MaxCell = int.MaxValue - 1;

    public static void Prepatch(AssemblyDefinition asmDef)
    {
        if (asmDef.Name.Name != "VRage.Math")
            return;

        var module = asmDef.MainModule;
        var type = module.GetType("VRage.Library.Collections.Concurrent.MyStaticObjectsCluster`1");
        var method = type.Methods.First(m => m.Name == "WorldPositionToInnerKey");
        var clamp = module
            .GetType("VRageMath.MathHelper")
            .Methods.First(m =>
                m.Name == "Clamp"
                && m.Parameters.Count == 3
                && m.Parameters.All(p => p.ParameterType.MetadataType == MetadataType.Double)
            );

        var il = method.Body.Instructions;

        il.RecordOriginalCode(method);
        il.VerifyCodeHash(method, "8dbc097a");

        foreach (var i in il.FindAllIndex(ci => ci.OpCode == OpCodes.Conv_I4).OrderDescending())
        {
            il.Insert(i, Instruction.Create(OpCodes.Ldc_R8, -MaxCell));
            il.Insert(i + 1, Instruction.Create(OpCodes.Ldc_R8, MaxCell));
            il.Insert(i + 2, Instruction.Create(OpCodes.Call, clamp));
        }

        il.RecordPatchedCode(method);
    }
}
