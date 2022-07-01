using System.Collections.Generic;
using System.Linq;
using CombatExtended;
using HarmonyLib;
using Verse;

namespace RimWorldOfMagic.Patch
{
    [StaticConstructorOnStartup]
    public static class RimWorldOfMagicPatches
    {
        static RimWorldOfMagicPatches()
        {
            var h = new Harmony("pirateby.RimWorldOfMagicPatches");
            if (ModLister.GetModWithIdentifier("ceteam.combatextended") != null)
            {
                CE_NoDrop_DestroyOnDrop_Items.Patch(h);
            }
        }
    }

    static class CE_NoDrop_DestroyOnDrop_Items
    {
        public static void Patch(Harmony h)
        {
            var getStorageByThingDef = AccessTools.Method(typeof(Utility_HoldTracker), nameof(Utility_HoldTracker.GetStorageByThingDef));
            var getExcessEquipment = AccessTools.Method(typeof(Utility_HoldTracker), nameof(Utility_HoldTracker.GetExcessEquipment));
            
            if (getStorageByThingDef == null || getExcessEquipment == null)
            {
                Log.Error($"CE_NoDrop_DestroyOnDrop_Items failed");
                return;
            }

            h.Patch(getStorageByThingDef, postfix: new HarmonyMethod(typeof(CE_NoDrop_DestroyOnDrop_Items), nameof(GetStorageByThingDef)));
            h.Patch(getExcessEquipment, postfix: new HarmonyMethod(typeof(CE_NoDrop_DestroyOnDrop_Items), nameof(GetExcessEquipment)));
        }

        static void GetStorageByThingDef(Pawn pawn, ref object __result)
        {
            // Utility_HoldTracker:GetExcessThing => prevent drop destroyOnDrop defs
            var dict = (Dictionary<ThingDef, Integer>)__result;
            var destroyOnDrop = dict.Keys.Where(x => x.destroyOnDrop).ToList();
            foreach (var def in destroyOnDrop)
            {
                dict.Remove(def);
            }
        }

        static void GetExcessEquipment(Pawn pawn, ref ThingWithComps dropEquipment, ref bool __result)
        {
            if (__result && dropEquipment.def.destroyOnDrop)
            {
                __result = false;
                dropEquipment = null;
            }
        }
    }
}
