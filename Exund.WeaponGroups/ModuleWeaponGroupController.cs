using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace Exund.WeaponGroups
{
    public class ModuleWeaponGroupController : Module
    {
        internal static readonly int aim_ID = 7777;
        internal static readonly Dictionary<Module, List<WeaponGroup>> manual_groups = new Dictionary<Module, List<WeaponGroup>>();

        public List<WeaponGroup> groups = new List<WeaponGroup>();

        private SerialData data;

        internal static void RemoveGroupForManual(Module module, WeaponGroup group)
        {
            if (!module)
            {
                return;
            }

            if (ModuleWeaponGroupController.manual_groups.TryGetValue(module, out var groups))
            {
                groups.Remove(group);
            }
        }

        internal static void KeepFiring(Module __instance, int aim, ref bool fire)
        {
            if (aim != aim_ID && !fire)
            {
                if (manual_groups.TryGetValue(__instance, out var groups))
                {
                    if (groups.Any(g => g.fireNextFrame))
                    {
                        fire = true;
                    }
                }
            }
        }

        private void OnPool()
        {
            block.serializeEvent.Subscribe(this.OnSerialize);
            block.serializeTextEvent.Subscribe(this.OnSerializeText);

            block.AttachedEvent.Subscribe(OnAttached);
            block.DetachingEvent.Subscribe(OnDetaching);
        }

        private void OnAttached()
        {
            block.tank.control.driveControlEvent.Subscribe(GetDriveControl);
            block.tank.DetachEvent.Subscribe(OnBlockDetached);
        }

        private void OnDetaching()
        {
            CleanManualGroups();
            this.groups.Clear();
            block.tank.control.driveControlEvent.Unsubscribe(GetDriveControl);
            block.tank.DetachEvent.Unsubscribe(OnBlockDetached);
        }

        private void OnRecycle()
        {
            CleanManualGroups();
        }

        private void OnBlockDetached(TankBlock block, Tank tank)
        {
            foreach (var g in groups)
            {
                if (g.Remove(block))
                {
                    return;
                }
            }
        }

        private void OnTankPostSpawn()
        {
            if (data == null)
            {
                return;
            }

            var blockman = block.tank.blockman;
            foreach (var group in data.groups)
            {
                var actual_group = new WeaponGroup()
                {
                    name = group.name,
                    keyCode = (KeyCode)group.keyCode
                };


                foreach (var p in group.positions)
                {
                    var block = blockman.GetBlockAtPosition(p);
                    if (block)
                    {
                        var weapon = new WeaponWrapper(block);
                        actual_group.Add(weapon);
                    }
                }

                groups.Add(actual_group);
            }

            data = null;
            block.tank.ResetPhysicsEvent.Unsubscribe(this.OnTankPostSpawn);
        }

        private void OnSerializeText(bool saving, TankPreset.BlockSpec blockSpec, bool onTech)
        {
            OnSerialize(saving, blockSpec);
        }

        private void OnSerialize(bool saving, TankPreset.BlockSpec blockSpec)
        {
            if (saving)
            {
                var serialDataSave = new SerialData()
                {
                    groups = groups.Select(group => new WeaponGroupSerial()
                    {
                        name = group.name,
                        positions = group.weapons.Select(w => w.block.cachedLocalPosition).ToArray(),
                        keyCode = (int)group.keyCode
                    }).ToArray()
                };

                serialDataSave.Store(blockSpec.saveState);
                return;
            }

            SerialData serialData = Module.SerialData<ModuleWeaponGroupController.SerialData>.Retrieve(blockSpec.saveState);
            if (serialData != null)
            {
                data = serialData;
                block.tank.ResetPhysicsEvent.Subscribe(this.OnTankPostSpawn);
            }
        }

        private void GetDriveControl(TankControl.ControlState state)
        {
            if (!state.Fire)
            {
                foreach (var group in groups)
                {
                    var fire = Input.GetKey(group.keyCode);
                    if (group.fireNextFrame && !fire || fire)
                    {
                        group.Fire(fire);
                    }
                }
            }
        }

        private void CleanManualGroups()
        {
            foreach (var g in groups)
            {
                g.CleanManualGroups();
            }
        }

        private class SerialData : Module.SerialData<ModuleWeaponGroupController.SerialData>
        {
            public WeaponGroupSerial[] groups;
        }

        [Serializable]
        public class WeaponGroupSerial
        {
            public string name;
            public Vector3[] positions;
            public int keyCode;
        }

        public class WeaponGroup
        {
            public string name = "New Group";
            public List<WeaponWrapper> weapons = new List<WeaponWrapper>();
            public KeyCode keyCode = KeyCode.Space;

            public bool fireNextFrame;

            public void Fire(bool fire)
            {
                fireNextFrame = fire;
                foreach (var w in weapons)
                {
                    w.Fire(fire);
                }
            }

            public void Add(WeaponWrapper weapon)
            {
                weapons.Add(weapon);

                foreach (var melee in weapon.melees)
                {
                    AddManual(melee);
                }

                if (weapon.scoop)
                {
                    AddManual(weapon.scoop);
                }
            }

            private void AddManual(Module module)
            {
                if (ModuleWeaponGroupController.manual_groups.TryGetValue(module, out var groups))
                {
                    groups.Add(this);
                }
                else
                {
                    ModuleWeaponGroupController.manual_groups.Add(module, new List<WeaponGroup>() { this });
                }
            }

            public bool Remove(TankBlock block)
            {
                for (int i = 0; i < weapons.Count; i++)
                {
                    var w = weapons[i];
                    if (w.block == block)
                    {
                        foreach (var melee in w.melees)
                        {
                            RemoveGroupForManual(melee, this);
                        }

                        RemoveGroupForManual(w.scoop, this);

                        w.block.Outline(false);
                        weapons.RemoveAt(i);
                        return true;
                    }
                }

                return false;
            }

            public void CleanManualGroups()
            {
                foreach (var w in weapons)
                {
                    foreach (var melee in w.melees)
                    {
                        RemoveGroupForManual(melee, this);
                    }

                    RemoveGroupForManual(w.scoop, this);
                }
            }
        }

        public class WeaponWrapper
        {
            /*
            private static readonly MethodInfo drill_ControlInput = AccessTools.Method(typeof(ModuleDrill), "OnControlInput");
            private static readonly MethodInfo hammer_ControlInput = AccessTools.Method(typeof(ModuleHammer), "OnControlInput");
            */

            private static readonly MethodInfo melee_ControlInput = AccessTools.Method(typeof(ModuleMeleeWeapon), "OnControlInput");
            private static readonly MethodInfo scoop_ControlInput = AccessTools.Method(typeof(ModuleScoop), "ControlInput");


            public ModuleWeapon weapon;
            public ModuleMeleeWeapon[] melees;
            public ModuleScoop scoop;

            public TankBlock block;

            public bool HasWeapons => weapon || melees.Length != 0 || scoop;

            public WeaponWrapper(TankBlock block)
            {
                this.block = block;
                this.weapon = block.GetComponent<ModuleWeapon>();
                this.melees = block.GetComponents<ModuleMeleeWeapon>();
                this.scoop = block.GetComponent<ModuleScoop>();
            }

            public void Fire(bool fire)
            {
                if (weapon)
                {
                    weapon.FireControl = fire;
                }
                if (melees.Length != 0)
                {
                    foreach (var melee in melees)
                    {
                        melee_ControlInput.Invoke(melee, new object[] { aim_ID, fire });
                    }
                }
                if (scoop)
                {
                    scoop_ControlInput.Invoke(scoop, new object[] { aim_ID, fire });
                }
            }
        }
    }
}
