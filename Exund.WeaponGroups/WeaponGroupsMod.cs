using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace Exund.WeaponGroups
{
    public class WeaponGroupsMod : ModBase
    {
        private const string HarmonyID = "Exund.WeaponGroups";

        private static bool Inited = false;

        private static ModContainer ThisContainer;

        private static readonly Harmony harmony = new Harmony(HarmonyID);

        public static void Load()
        {
            var holder = new GameObject();
            holder.AddComponent<GroupControllerEditor>();
            GameObject.DontDestroyOnLoad(holder);
        }

        public override void EarlyInit()
        {
            if (!Inited)
            {
                Dictionary<string, ModContainer> dictionary = (Dictionary<string, ModContainer>)AccessTools.Field(typeof(ManMods), "m_Mods").GetValue(Singleton.Manager<ManMods>.inst);

                if (dictionary.TryGetValue("Weapon Groups", out ThisContainer))
                {
                    GroupControllerEditor.icon_cancel = ThisContainer.Contents.FindAsset("cancel.png") as Texture2D;
                    GroupControllerEditor.icon_remove = ThisContainer.Contents.FindAsset("remove.png") as Texture2D;
                    GroupControllerEditor.icon_rename = ThisContainer.Contents.FindAsset("rename.png") as Texture2D;
                }
                else
                {
                    Console.WriteLine("FAILED TO FETCH Weapon Groups ModContainer");
                }
                Inited = true;
                Load();
            }
        }

        public override bool HasEarlyInit()
        {
            return true;
        }

        public override void DeInit()
        {
            harmony.UnpatchAll(HarmonyID);
        }

        public override void Init()
        {
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
	}

    public static class Extension
    {
        public static void Outline(this TankBlock block, bool state)
        {
            block.visible.EnableOutlineGlow(state, cakeslice.Outline.OutlineEnableReason.ScriptHighlight);
        }
    }

    internal static class Patches
    {
        [HarmonyPatch(typeof(ModuleMeleeWeapon), "OnControlInput")]
        private static class ModuleMeleeWeapon_ControlInput
        {
            private static void Prefix(ModuleMeleeWeapon __instance, int aim, ref bool fire)
            {
                ModuleWeaponGroupController.KeepFiring(__instance, aim, ref fire);
            }
        }

        [HarmonyPatch(typeof(ModuleScoop), "ControlInput")]
        private static class ModuleScoop_ControlInput
        {
            private static void Prefix(ModuleScoop __instance, int aim, ref bool doLift)
            {
                //Debug.Log("[ModuleMeleeWeapon/OnControlInput/Prefix] " + aim + " " + fire);
                ModuleWeaponGroupController.KeepFiring(__instance, aim, ref doLift);
            }
        }
    }
}
