using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using CombatExtended;
using HarmonyLib;
using RimWorld;
using TorannMagic;
using TorannMagic.Thoughts;
using UnityEngine;
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
            h.PatchAll();

            // Supersoldier dump
            // LongEventHandler.ExecuteWhenFinished(() =>
            // {
            //     var pistolList = WeaponCategoryList.Named("TM_Category_Pistols").weaponDefNames;
            //     var rifleList = WeaponCategoryList.Named("TM_Category_Rifles").weaponDefNames;
            //     var shotgunList = WeaponCategoryList.Named("TM_Category_Shotguns").weaponDefNames;

            //     List<string> pistols = new(), rifles = new(), shotguns = new(), undefined = new();
            //     var rangedWeapons = DefDatabase<ThingDef>.AllDefs.Where(x => x.IsRangedWeapon && x.weaponTags?.Contains("TurretGun") != true);
            //     foreach (var weapon in rangedWeapons)
            //     {
            //         if (pistolList.Contains(weapon.defName))
            //             pistols.Add($"  <li>{weapon.defName}</li> <!-- {weapon.label} -->");
            //         else if (rifleList.Contains(weapon.defName))
            //             rifles.Add($"  <li>{weapon.defName}</li> <!-- {weapon.label} -->");
            //         else if (shotgunList.Contains(weapon.defName))
            //             shotguns.Add($"  <li>{weapon.defName}</li> <!-- {weapon.label} -->");
            //         else if (!weapon.defName.ToLower().Contains("shotgun") &&
            //                  !((weapon.defName.ToLower().Contains("rifle") || weapon.defName.Contains("LMG")) && !weapon.defName.ToLower().Contains("sniper")) &&
            //                  !weapon.defName.ToLower().Contains("pistol") &&  !weapon.defName.ToLower().Contains("revolver") && !weapon.defName.Contains("SMG"))
            //             undefined.Add($"  <li>{weapon.defName}</li> <!-- {weapon.label} -->");
            //     }

            //     List<string> allWeapons = new();
            //     allWeapons.Add("undefined:");
            //     allWeapons.AddRange(undefined);
            //     allWeapons.Add("\n\npistols:");
            //     allWeapons.AddRange(pistols);
            //     allWeapons.Add("\n\nrifles:");
            //     allWeapons.AddRange(rifles);
            //     allWeapons.Add("\n\nshotguns:");
            //     allWeapons.AddRange(shotguns);
            //     File.WriteAllLines("SuperSoldier_Weapons_Cat.txt", allWeapons);
            // });
        }
    }
	
	#region Inspiration_Null_checks
    [HarmonyPatch]
    static class Inspiration_Null_checks
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Inspiration_MagicUser), "InspirationCanOccur");
            yield return AccessTools.Method(typeof(Inspiration_MightUser), "InspirationCanOccur");
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> InspirationFix(MethodBase __originalMethod, IEnumerable<CodeInstruction> instructions, ILGenerator ilGen)
		{
			var code = instructions.ToList();

			var is___UserMethod = __originalMethod.DeclaringType.Name == "Inspiration_MagicUser" ?
				AccessTools.Method("TorannMagic.CompAbilityUserMagic:get_IsMagicUser") :
				AccessTools.Method("TorannMagic.CompAbilityUserMight:get_IsMightUser");

            int setFlagIdx = code.FindIndex(x => x.Calls(is___UserMethod)) + 1;
			if (setFlagIdx == -1) throw new Exception("Can't find isMagicUserMethod");

			var defCodeLabel = ilGen.DefineLabel();
			var ifCompNullLabel = ilGen.DefineLabel();
			code[setFlagIdx].labels.Add(defCodeLabel);

			foreach (var ci in instructions)
			{
				// Add null check:
				//   bool isMagicUser = comp.IsMagicUser; => bool isMagicUser = comp != null && comp.IsMagicUser;
				if (ci.Calls(is___UserMethod))
				{
					yield return new CodeInstruction(OpCodes.Brfalse_S, ifCompNullLabel);
					yield return new CodeInstruction(OpCodes.Ldloc_2);
					yield return new CodeInstruction(OpCodes.Callvirt, is___UserMethod);
					yield return new CodeInstruction(OpCodes.Br_S, defCodeLabel);
					yield return new CodeInstruction(OpCodes.Ldc_I4_0).WithLabels(ifCompNullLabel);
				}
				else yield return ci;
			}
		}
    }
    #endregion Inspiration_Null_checks
    
    #region CE_NoDrop_DestroyOnDrop_Items
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
    #endregion CE_NoDrop_DestroyOnDrop_Items

    #region Technomancer and SuperSoldier: CE Fixes

    [HarmonyPatch(typeof(TM_Action), nameof(TM_Action.DoAction_TechnoWeaponCopy))]
    static class CE_DoAction_TechnoWeaponCopy
    {
        static bool Prepare() => ModLister.GetModWithIdentifier("ceteam.combatextended") != null;

        static Thing MakeCEThing(ThingDef def, ThingDef stuff, CompAbilityUserMagic comp, Thing thing)
        {
            int verVal = comp.MagicData.MagicPowerSkill_TechnoWeapon.FirstOrDefault((MagicPowerSkill x) => x.label == "TM_TechnoWeapon_ver").level;
            int pwrVal = comp.MagicData.MagicPowerSkill_TechnoWeapon.FirstOrDefault((MagicPowerSkill x) => x.label == "TM_TechnoWeapon_pwr").level;

            def.SetStatBaseValue(CE_StatDefOf.SightsEfficiency, thing.GetStatValue(CE_StatDefOf.SightsEfficiency) * (1 + .01f * pwrVal));
            def.SetStatBaseValue(CE_StatDefOf.ShotSpread, thing.GetStatValue(CE_StatDefOf.ShotSpread) * (1 - .01f * pwrVal));
            def.SetStatBaseValue(CE_StatDefOf.SwayFactor, thing.GetStatValue(CE_StatDefOf.SwayFactor) * (1 - .01f * pwrVal));

            var srcVerb = thing.def.Verbs.FirstOrDefault();
            var newVerb = new VerbPropertiesCE();
            newVerb.verbClass = typeof(Verb_ShootCE);
            newVerb.hasStandardCommand = srcVerb.hasStandardCommand;
            newVerb.soundCast = srcVerb.soundCast;
            newVerb.soundCastTail = srcVerb.soundCastTail;
            newVerb.muzzleFlashScale = srcVerb.muzzleFlashScale;

            CompAmmoUser compAmmoUser = comp.technoWeaponThing.TryGetComp<CompAmmoUser>();
            newVerb.defaultProjectile = compAmmoUser.CurAmmoProjectile; //srcVerb.defaultProjectile;
            newVerb.range = srcVerb.range * (1f + .02f * pwrVal);
            // newVerb.recoilAmount = srcVerb.recoilAmount * (1f - .02f * pwrVal);
            newVerb.warmupTime = srcVerb.warmupTime * (1f - .02f * pwrVal);
            newVerb.burstShotCount = Mathf.RoundToInt(srcVerb.burstShotCount * (1f + .02f * pwrVal));
            newVerb.ticksBetweenBurstShots = Mathf.RoundToInt(srcVerb.ticksBetweenBurstShots * (1f - .02f * pwrVal));
            def.Verbs.RemoveAt(0);
            def.Verbs.Insert(0, newVerb);

            def.tools = new List<Tool>(thing.def.tools); // Fix melee
            return ThingMaker.MakeThing(def, stuff);
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var makeThingMethod = AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing));
            var makeCEThingMethod = AccessTools.Method(typeof(CE_DoAction_TechnoWeaponCopy), nameof(MakeCEThing));

            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction ci = list[i];
                if (ci.opcode == OpCodes.Ldstr && ci.operand == "Verse.Verb_Shoot")
                    yield return new CodeInstruction(OpCodes.Ldstr, "CombatExtended.Verb_ShootCE");
                else if (ci.Calls(makeThingMethod) && list[i - 1].opcode == OpCodes.Ldnull && list[i - 2].opcode == OpCodes.Ldloc_S)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, makeCEThingMethod);
                    Log.Message("[ROM.CE] CE_DoAction_TechnoWeaponCopy");
                }
                else
                    yield return ci;
            }
        }
    }

    [HarmonyPatch(typeof(TM_Action), nameof(TM_Action.DoAction_PistolSpecCopy))]
    static class CE_DoAction_PistolSpecCopy
    {
        static bool Prepare() => ModLister.GetModWithIdentifier("ceteam.combatextended") != null;

        static Thing MakeCEThing(ThingDef def, ThingDef stuff, CompAbilityUserMagic comp, Thing thing)
        {
            int pwrVal = comp.MagicData.MagicPowerSkill_TechnoWeapon.FirstOrDefault((MagicPowerSkill x) => x.label == "TM_PistolSpec_pwr").level;
            /*
            newThingDef.SetStatBaseValue(StatDefOf.RangedWeapon_DamageMultiplier, thing.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) * (1f + (.03f * pwrVal)));
            newThingDef.SetStatBaseValue(StatDefOf.RangedWeapon_Cooldown, thing.GetStatValue(StatDefOf.RangedWeapon_Cooldown) * (1 - .025f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyTouch, thing.GetStatValue(StatDefOf.AccuracyTouch));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyShort, thing.GetStatValue(StatDefOf.AccuracyShort) * (1 + .015f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyMedium, thing.GetStatValue(StatDefOf.AccuracyMedium) * (1 + .005f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyLong, thing.GetStatValue(StatDefOf.AccuracyLong));

            newThingDef.Verbs.FirstOrDefault().defaultProjectile = thing.def.Verbs.FirstOrDefault().defaultProjectile;
            newThingDef.Verbs.FirstOrDefault().range = thing.def.Verbs.FirstOrDefault().range * (1f + .01f * pwrVal);
            newThingDef.Verbs.FirstOrDefault().warmupTime = thing.def.Verbs.FirstOrDefault().warmupTime * (1f - .025f * pwrVal);
            newThingDef.Verbs.FirstOrDefault().burstShotCount = Mathf.RoundToInt(thing.def.Verbs.FirstOrDefault().burstShotCount * (1f + .02f * pwrVal));
            newThingDef.Verbs.FirstOrDefault().ticksBetweenBurstShots = Mathf.RoundToInt(thing.def.Verbs.FirstOrDefault().ticksBetweenBurstShots * (1f - .03f * pwrVal));
             */
            def.SetStatBaseValue(CE_StatDefOf.SightsEfficiency, thing.GetStatValue(CE_StatDefOf.SightsEfficiency) * (1 + .015f * pwrVal));
            def.SetStatBaseValue(CE_StatDefOf.ShotSpread, thing.GetStatValue(CE_StatDefOf.ShotSpread) * (1 - .015f * pwrVal));
            def.SetStatBaseValue(CE_StatDefOf.SwayFactor, thing.GetStatValue(CE_StatDefOf.SwayFactor) * (1 - .015f * pwrVal));

            var srcVerb = thing.def.Verbs.FirstOrDefault();
            var newVerb = new VerbPropertiesCE();
            newVerb.verbClass = typeof(Verb_ShootCE);
            newVerb.hasStandardCommand = srcVerb.hasStandardCommand;
            newVerb.soundCast = srcVerb.soundCast;
            newVerb.soundCastTail = srcVerb.soundCastTail;
            newVerb.muzzleFlashScale = srcVerb.muzzleFlashScale;

            CompAmmoUser compAmmoUser = comp.technoWeaponThing.TryGetComp<CompAmmoUser>();
            newVerb.defaultProjectile = compAmmoUser.CurAmmoProjectile; //srcVerb.defaultProjectile;
            newVerb.range = srcVerb.range * (1f + .01f * pwrVal);
            // newVerb.recoilAmount = srcVerb.recoilAmount * (1f - .02f * pwrVal);
            newVerb.warmupTime = srcVerb.warmupTime * (1f - .025f * pwrVal);
            newVerb.burstShotCount = Mathf.RoundToInt(srcVerb.burstShotCount * (1f + .02f * pwrVal));
            newVerb.ticksBetweenBurstShots = Mathf.RoundToInt(srcVerb.ticksBetweenBurstShots * (1f - .03f * pwrVal));
            def.Verbs.RemoveAt(0);
            def.Verbs.Insert(0, newVerb);

            def.tools = new List<Tool>(thing.def.tools); // Fix melee
            // Log.Warning($"def: {def}");
            return ThingMaker.MakeThing(def, stuff);
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var makeThingMethod = AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing));
            var makeCEThingMethod = AccessTools.Method(typeof(CE_DoAction_PistolSpecCopy), nameof(MakeCEThing));

            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction ci = list[i];
                if (ci.opcode == OpCodes.Ldstr && ci.operand == "Verse.Verb_Shoot")
                    yield return new CodeInstruction(OpCodes.Ldstr, "CombatExtended.Verb_ShootCE");
                else if (ci.Calls(makeThingMethod) && list[i - 1].opcode == OpCodes.Ldnull && list[i - 2].opcode == OpCodes.Ldloc_S)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, makeCEThingMethod);
                    Log.Message("[ROM.CE] CE_DoAction_PistolSpecCopy");
                }
                else
                    yield return ci;
            }
        }
    }

    [HarmonyPatch(typeof(TM_Action), nameof(TM_Action.DoAction_RifleSpecCopy))]
    static class CE_DoAction_RifleSpecCopy
    {
        static bool Prepare() => ModLister.GetModWithIdentifier("ceteam.combatextended") != null;

        static Thing MakeCEThing(ThingDef def, ThingDef stuff, CompAbilityUserMagic comp, Thing thing)
        {
            int pwrVal = comp.MagicData.MagicPowerSkill_TechnoWeapon.FirstOrDefault((MagicPowerSkill x) => x.label == "TM_RifleSpec_pwr").level;
            /*
            newThingDef.SetStatBaseValue(StatDefOf.RangedWeapon_DamageMultiplier, thing.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) * (1f + (.02f * pwrVal)));
            newThingDef.SetStatBaseValue(StatDefOf.RangedWeapon_Cooldown, thing.GetStatValue(StatDefOf.RangedWeapon_Cooldown) * (1 - .01f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyTouch, thing.GetStatValue(StatDefOf.AccuracyTouch));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyShort, thing.GetStatValue(StatDefOf.AccuracyShort) * (1 + .01f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyMedium, thing.GetStatValue(StatDefOf.AccuracyMedium) * (1 + .01f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyLong, thing.GetStatValue(StatDefOf.AccuracyLong) * (1 + .01f * pwrVal));

            newThingDef.Verbs.FirstOrDefault().defaultProjectile = thing.def.Verbs.FirstOrDefault().defaultProjectile;
            newThingDef.Verbs.FirstOrDefault().range = thing.def.Verbs.FirstOrDefault().range * (1f + .02f * pwrVal);
            newThingDef.Verbs.FirstOrDefault().warmupTime = thing.def.Verbs.FirstOrDefault().warmupTime * (1f - .01f * pwrVal);
            newThingDef.Verbs.FirstOrDefault().burstShotCount = Mathf.RoundToInt(thing.def.Verbs.FirstOrDefault().burstShotCount * (1f + .03f * pwrVal));
            newThingDef.Verbs.FirstOrDefault().ticksBetweenBurstShots = Mathf.RoundToInt(thing.def.Verbs.FirstOrDefault().ticksBetweenBurstShots * (1f - .02f * pwrVal));
             */
            def.SetStatBaseValue(CE_StatDefOf.SightsEfficiency, thing.GetStatValue(CE_StatDefOf.SightsEfficiency) * (1 + .01f * pwrVal));
            def.SetStatBaseValue(CE_StatDefOf.ShotSpread, thing.GetStatValue(CE_StatDefOf.ShotSpread) * (1 - .01f * pwrVal));
            def.SetStatBaseValue(CE_StatDefOf.SwayFactor, thing.GetStatValue(CE_StatDefOf.SwayFactor) * (1 - .01f * pwrVal));

            var srcVerb = thing.def.Verbs.FirstOrDefault();
            var newVerb = new VerbPropertiesCE();
            newVerb.verbClass = typeof(Verb_ShootCE);
            newVerb.hasStandardCommand = srcVerb.hasStandardCommand;
            newVerb.soundCast = srcVerb.soundCast;
            newVerb.soundCastTail = srcVerb.soundCastTail;
            newVerb.muzzleFlashScale = srcVerb.muzzleFlashScale;

            CompAmmoUser compAmmoUser = comp.technoWeaponThing.TryGetComp<CompAmmoUser>();
            newVerb.defaultProjectile = compAmmoUser.CurAmmoProjectile; //srcVerb.defaultProjectile;
            newVerb.range = srcVerb.range * (1f + .02f * pwrVal);
            // newVerb.recoilAmount = srcVerb.recoilAmount * (1f - .02f * pwrVal);
            newVerb.warmupTime = srcVerb.warmupTime * (1f - .01f * pwrVal);
            newVerb.burstShotCount = Mathf.RoundToInt(srcVerb.burstShotCount * (1f + .03f * pwrVal));
            newVerb.ticksBetweenBurstShots = Mathf.RoundToInt(srcVerb.ticksBetweenBurstShots * (1f - .02f * pwrVal));
            def.Verbs.RemoveAt(0);
            def.Verbs.Insert(0, newVerb);

            def.tools = new List<Tool>(thing.def.tools); // Fix melee

            return ThingMaker.MakeThing(def, stuff);
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var makeThingMethod = AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing));
            var makeCEThingMethod = AccessTools.Method(typeof(CE_DoAction_RifleSpecCopy), nameof(MakeCEThing));

            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction ci = list[i];
                if (ci.opcode == OpCodes.Ldstr && ci.operand == "Verse.Verb_Shoot")
                    yield return new CodeInstruction(OpCodes.Ldstr, "CombatExtended.Verb_ShootCE");
                else if (ci.Calls(makeThingMethod) && list[i - 1].opcode == OpCodes.Ldnull && list[i - 2].opcode == OpCodes.Ldloc_S)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, makeCEThingMethod);
                    Log.Message("[ROM.CE] CE_DoAction_RifleSpecCopy");
                }
                else
                    yield return ci;
            }
        }
    }

    [HarmonyPatch(typeof(TM_Action), nameof(TM_Action.DoAction_ShotgunSpecCopy))]
    static class CE_DoAction_ShotgunSpecCopy
    {
        static bool Prepare() => ModLister.GetModWithIdentifier("ceteam.combatextended") != null;

        static Thing MakeCEThing(ThingDef def, ThingDef stuff, CompAbilityUserMagic comp, Thing thing)
        {
            int pwrVal = comp.MagicData.MagicPowerSkill_TechnoWeapon.FirstOrDefault((MagicPowerSkill x) => x.label == "TM_ShotgunSpec_pwr").level;
            /*
            newThingDef.SetStatBaseValue(StatDefOf.RangedWeapon_DamageMultiplier, thing.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) * (1f + (.02f * pwrVal)));
            newThingDef.SetStatBaseValue(StatDefOf.RangedWeapon_Cooldown, thing.GetStatValue(StatDefOf.RangedWeapon_Cooldown) * (1 - .03f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyTouch, thing.GetStatValue(StatDefOf.AccuracyTouch) * (1 + .01f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyShort, thing.GetStatValue(StatDefOf.AccuracyShort) * (1 + .015f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyMedium, thing.GetStatValue(StatDefOf.AccuracyMedium) * (1 + .005f * pwrVal));
            newThingDef.SetStatBaseValue(StatDefOf.AccuracyLong, thing.GetStatValue(StatDefOf.AccuracyLong));

            newThingDef.Verbs.FirstOrDefault().defaultProjectile = thing.def.Verbs.FirstOrDefault().defaultProjectile;
            newThingDef.Verbs.FirstOrDefault().range = thing.def.Verbs.FirstOrDefault().range * (1f + .01f * pwrVal);
            newThingDef.Verbs.FirstOrDefault().warmupTime = thing.def.Verbs.FirstOrDefault().warmupTime * (1f - .03f * pwrVal);
            newThingDef.Verbs.FirstOrDefault().burstShotCount = Mathf.RoundToInt(thing.def.Verbs.FirstOrDefault().burstShotCount);
            newThingDef.Verbs.FirstOrDefault().ticksBetweenBurstShots = Mathf.RoundToInt(thing.def.Verbs.FirstOrDefault().ticksBetweenBurstShots * (1f - .02f * pwrVal));
             */
            def.SetStatBaseValue(CE_StatDefOf.SightsEfficiency, thing.GetStatValue(CE_StatDefOf.SightsEfficiency) * (1 + .015f * pwrVal));
            def.SetStatBaseValue(CE_StatDefOf.ShotSpread, thing.GetStatValue(CE_StatDefOf.ShotSpread) * (1 - .015f * pwrVal));
            def.SetStatBaseValue(CE_StatDefOf.SwayFactor, thing.GetStatValue(CE_StatDefOf.SwayFactor) * (1 - .015f * pwrVal));

            var srcVerb = thing.def.Verbs.FirstOrDefault();
            var newVerb = new VerbPropertiesCE();
            newVerb.verbClass = typeof(Verb_ShootCE);
            newVerb.hasStandardCommand = srcVerb.hasStandardCommand;
            newVerb.soundCast = srcVerb.soundCast;
            newVerb.soundCastTail = srcVerb.soundCastTail;
            newVerb.muzzleFlashScale = srcVerb.muzzleFlashScale;

            CompAmmoUser compAmmoUser = comp.technoWeaponThing.TryGetComp<CompAmmoUser>();
            newVerb.defaultProjectile = compAmmoUser.CurAmmoProjectile; //srcVerb.defaultProjectile;
            newVerb.range = srcVerb.range * (1f + .01f * pwrVal);
            // newVerb.recoilAmount = srcVerb.recoilAmount * (1f - .02f * pwrVal);
            newVerb.warmupTime = srcVerb.warmupTime * (1f - .03f * pwrVal);
            newVerb.burstShotCount = Mathf.RoundToInt(srcVerb.burstShotCount);
            newVerb.ticksBetweenBurstShots = Mathf.RoundToInt(srcVerb.ticksBetweenBurstShots * (1f - .02f * pwrVal));
            def.Verbs.RemoveAt(0);
            def.Verbs.Insert(0, newVerb);

            def.tools = new List<Tool>(thing.def.tools); // Fix melee

            return ThingMaker.MakeThing(def, stuff);
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var makeThingMethod = AccessTools.Method(typeof(ThingMaker), nameof(ThingMaker.MakeThing));
            var makeCEThingMethod = AccessTools.Method(typeof(CE_DoAction_ShotgunSpecCopy), nameof(MakeCEThing));

            var list = instructions.ToList();
            for (int i = 0; i < list.Count; i++)
            {
                CodeInstruction ci = list[i];
                if (ci.opcode == OpCodes.Ldstr && ci.operand == "Verse.Verb_Shoot")
                    yield return new CodeInstruction(OpCodes.Ldstr, "CombatExtended.Verb_ShootCE");
                else if (ci.Calls(makeThingMethod) && list[i - 1].opcode == OpCodes.Ldnull && list[i - 2].opcode == OpCodes.Ldloc_S)
                {
                    yield return new CodeInstruction(OpCodes.Ldloc_0);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Call, makeCEThingMethod);
                    Log.Message("[ROM.CE] CE_DoAction_ShotgunSpecCopy");
                }
                else
                    yield return ci;
            }
        }
    }

    #endregion Technomancer and SuperSoldier: CE Fixes

    #region Chronomancer: VSE - WinstonWaves Fixes

    [HarmonyPatch(typeof(TorannMagicMod.IncidentWorker_TryExecute_Prefix_Patch), nameof(TorannMagicMod.IncidentWorker_TryExecute_Prefix_Patch.Prefix))]
    static class Chronomancer_WinstoWaves_Fix
    {
        static bool Prepare() => ModLister.GetModWithIdentifier("vanillastorytellersexpanded.winstonwave") != null;

        static bool IsVseWWRaid(IncidentParms parms)
        {
            if (Find.Storyteller.def.defName == "VSE_WinstonWave")
            {
                Map map = (Map)parms.target;
                MapComponent component = map.components.FirstOrDefault(x => x.GetType().Name == "MapComponent_Winston");
                if (component != null && Traverse.Create(component) is Traverse c &&
                    (c.Field("nextRaidInfo").Field("incidentParms").GetValue<IncidentParms>().pawnGroupMakerSeed == parms.pawnGroupMakerSeed ||
                     c.Field("nextRaidInfo").Field("reinforcementSeed").GetValue<int>() == parms.pawnGroupMakerSeed))
                {
                    Log.Message($"Block Chronomancer prediction ability for WinstonWaves raid!");
                    return true;
                }
            }
            return false;
        }

        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            Label originalCodeLabel = generator.DefineLabel();
            instructions.First().labels.Add(originalCodeLabel);

            CodeInstruction[] wwChecker = {
                // if (IsVseWwRaid(parms)) return true
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldind_Ref),
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Chronomancer_WinstoWaves_Fix), nameof(IsVseWWRaid))),
                new CodeInstruction(OpCodes.Brfalse_S, originalCodeLabel),
                new CodeInstruction(OpCodes.Ldc_I4_1),
                new CodeInstruction(OpCodes.Ret),
            };

            return wwChecker.Union(instructions);
        }
    }

    #endregion Chronomancer VSE - WinstonWaves Fixes
}
