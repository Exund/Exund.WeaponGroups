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
        internal static readonly Dictionary<ModuleHammer, List<WeaponGroup>> groups_for_hammer = new Dictionary<ModuleHammer, List<WeaponGroup>>();

        public List<WeaponGroup> groups = new List<WeaponGroup>();

        private SerialData data;

        internal static void RemoveGroupForHammer(ModuleHammer hammer, WeaponGroup group)
        {
            if (ModuleWeaponGroupController.groups_for_hammer.TryGetValue(hammer, out var groups))
            {
                groups.Remove(group);
            }
        }

        private void OnPool()
        {
            block.serializeEvent.Subscribe(this.OnSerialize);
            block.serializeTextEvent.Subscribe(this.OnSerialize);

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
            CleanHammersGroups();
            this.groups.Clear();
            block.tank.control.driveControlEvent.Unsubscribe(GetDriveControl);
            block.tank.DetachEvent.Unsubscribe(OnBlockDetached);
        }

        private void OnRecycle()
        {
            CleanHammersGroups();
        }

        private void OnBlockDetached(TankBlock block, Tank tank)
        {
            foreach (var g in groups)
            {
                for (int i = 0; i < g.weapons.Count; i++)
                {
                    var w = g.weapons[i];
                    if (w.block == block)
                    {
                        if(w.hammer)
                        {
                            RemoveGroupForHammer(w.hammer, g);
                        }

                        w.block.Outline(false);
                        g.weapons.RemoveAt(i);
                        return;
                    }
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

                var weapons = group.positions.Select(p =>
                {
                    var block = blockman.GetBlockAtPosition(p);
                    if (block)
                    {
                        var weapon = new WeaponWrapper(block);
                        if(weapon.hammer)
                        {
                            if (ModuleWeaponGroupController.groups_for_hammer.TryGetValue(weapon.hammer, out var groups))
                            {
                                groups.Add(actual_group);
                            }
                            else
                            {
                                ModuleWeaponGroupController.groups_for_hammer.Add(weapon.hammer, new List<ModuleWeaponGroupController.WeaponGroup>() { actual_group });
                            }
                        }
                        return weapon;
                    }
                    return null;
                }).Where(wrapper => wrapper != null).ToList();

                actual_group.weapons = weapons;

                groups.Add(actual_group);
            }

            data = null;
            block.tank.ResetPhysicsEvent.Unsubscribe(this.OnTankPostSpawn);
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

        private void CleanHammersGroups()
        {
            foreach (var g in groups)
            {
                foreach (var w in g.weapons)
                {
                    if (w.hammer)
                    {
                        RemoveGroupForHammer(w.hammer, g);
                    }
                }
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
        }

        public class WeaponWrapper
        {
            private static readonly MethodInfo drill_ControlInput = AccessTools.Method(typeof(ModuleDrill), "OnControlInput");
            private static readonly MethodInfo hammer_ControlInput = AccessTools.Method(typeof(ModuleHammer), "OnControlInput");

            public ModuleWeapon weapon;
            public ModuleDrill drill;
            public ModuleHammer hammer;

            public TankBlock block;

            public WeaponWrapper(TankBlock block)
            {
                this.block = block;
                this.weapon = block.GetComponent<ModuleWeapon>();
                this.drill = block.GetComponent<ModuleDrill>();
                this.hammer = block.GetComponent<ModuleHammer>();
            }

            public void Fire(bool fire)
            {
                if (weapon)
                {
                    weapon.FireControl = fire;
                }
                if (drill)
                {
                    drill_ControlInput.Invoke(drill, new object[] { 0, fire });
                }
                if (hammer)
                {
                    hammer_ControlInput.Invoke(hammer, new object[] { aim_ID, fire });
                }
            }
        }
    }
}
