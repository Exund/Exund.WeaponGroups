using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

namespace Exund.WeaponGroups
{
    internal class GroupControllerEditor : MonoBehaviour
    {
        internal static Texture2D icon_cancel;
        internal static Texture2D icon_remove;
        internal static Texture2D icon_rename;
        private static readonly float icon_size = 30;

        private static GUIStyle textField;
        private static GUIStyle middledText;
        private static GUIStyle bigText;

        private readonly int ID = GUIUtility.GetControlID(FocusType.Passive);
        private Rect Win = new Rect(0, 0, 500f, 300f);
        private Vector2 scroll;

        private ModuleWeaponGroupController module;
        private ModuleWeaponGroupController.WeaponGroup selectedGroup;
        private bool renaming = false;
        private bool changeKey = false;
        private bool selectingWeapon = false;
        private bool weaponSelected = false;
        private bool groupAdded = false;
        private bool expanded = false;

        private int groupToRemove = -1;
        private List<ModuleWeaponGroupController.WeaponWrapper> displayWeapons;

        private void Start()
        {
            useGUILayout = false;
        }

        private void Update()
        {
            if (module && !module.block.tank)
            {
                Clean();
            }

            if (module && selectedGroup != null && selectingWeapon && Input.GetMouseButtonDown(0))
            {
                selectingWeapon = false;
                try
                {
                    var temp_block = Singleton.Manager<ManPointer>.inst.targetVisible?.block;
                    if (temp_block && temp_block.tank == module.block.tank)
                    {
                        var weapon = new ModuleWeaponGroupController.WeaponWrapper(temp_block);
                        if (selectedGroup.weapons.All(w => w.block != temp_block) && (weapon.weapon || weapon.hammer || weapon.drill))
                        {
                            selectedGroup.weapons.Add(weapon);
                            if(weapon.hammer)
                            {
                                if(ModuleWeaponGroupController.groups_for_hammer.TryGetValue(weapon.hammer, out var groups))
                                {
                                    groups.Add(selectedGroup);
                                }
                                else
                                {
                                    ModuleWeaponGroupController.groups_for_hammer.Add(weapon.hammer, new List<ModuleWeaponGroupController.WeaponGroup>() { selectedGroup });
                                }
                            }

                            weapon.block.Outline(true);
                            weaponSelected = true;
                        }
                    }
                }
                catch { }
            }

            if (Input.GetMouseButtonDown(1))
            {
                CheckForController();
            }
        }

        private void CheckForController()
        {
            var visible = Singleton.Manager<ManPointer>.inst.targetVisible;
            if (visible)
            {
                var block = visible.block;
                if (block)
                {
                    var controller = block.GetComponent<ModuleWeaponGroupController>();
                    if (block.tank && controller)
                    {
                        SelectModule(controller);

                        useGUILayout = true;
                    }
                }
            }
        }

        private void OnGUI()
        {
            if (textField == null)
            {
                textField = new GUIStyle(GUI.skin.textField)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = 20
                };

                middledText = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleLeft
                };

                bigText = new GUIStyle(middledText)
                {
                    fontSize = 20
                };
            }

            if (!useGUILayout)
            {
                return;
            }

            if (Event.current.type == EventType.Layout)
            {
                if (groupToRemove != -1)
                {
                    var g = module.groups[groupToRemove];
                    foreach (var w in g.weapons)
                    {
                        if (w.hammer)
                        {
                            ModuleWeaponGroupController.RemoveGroupForHammer(w.hammer, g);
                        }
                    }
                    module.groups.RemoveAt(groupToRemove);
                    groupToRemove = -1;
                }
                if (selectedGroup != null)
                {
                    displayWeapons = new List<ModuleWeaponGroupController.WeaponWrapper>(selectedGroup.weapons);
                }
            }

            Win = GUI.Window(ID, Win, DoWindow, "Weapon Group Controller");
        }

        private void DoWindow(int id)
        {
            if (selectedGroup != null && changeKey)
            {
                Event current = Event.current;
                if (current.isKey)
                {
                    selectedGroup.keyCode = current.keyCode;
                    changeKey = false;
                }
            }

            var icon_width = GUILayout.Width(icon_size);
            var icon_height = GUILayout.Height(icon_size);

            GUILayout.BeginVertical();
            scroll = GUILayout.BeginScrollView(scroll);
            {
                //Groups
                for (int i = 0; i < module.groups.Count; i++)
                {
                    var group = module.groups[i];

                    var tempgroup = selectedGroup;
                    var tempExpanded = expanded;

                    GUILayout.BeginHorizontal(GUI.skin.button, GUILayout.Height(25));
                    {
                        GUILayout.BeginVertical();
                        {
                            GUILayout.FlexibleSpace();
                            GUILayout.BeginHorizontal();
                            {
                                //Renaming
                                if (renaming && group == selectedGroup)
                                {
                                    group.name = GUILayout.TextField(group.name, textField, GUILayout.Width(250), icon_height);
                                }
                                else
                                {
                                    GUILayout.Label(group.name, bigText, GUILayout.Width(250));
                                }

                                GUILayout.FlexibleSpace();

                                //Rename button
                                if (GUILayout.Button(renaming && group == selectedGroup ? icon_cancel : icon_rename, icon_width, icon_height))
                                {
                                    renaming = !renaming;
                                    changeKey = false;
                                }

                                //Key button
                                if (GUILayout.Button(changeKey && group == selectedGroup ? "Press a key" : group.keyCode.ToString(), GUILayout.MinWidth(icon_size), icon_height))
                                {
                                    changeKey = true;
                                }

                                //Remove button
                                if (GUILayout.Button(icon_remove, icon_width, icon_height))
                                {
                                    groupToRemove = i;
                                    if (selectedGroup == group)
                                    {
                                        CleanGroup();
                                    }
                                }

                                //Expand button
                                if (GUILayout.Button(selectedGroup == group && expanded ? "▲" : "▼", icon_width, icon_height))
                                {
                                    if (group != selectedGroup)
                                    {
                                        CleanGroup();
                                        expanded = true;

                                        selectedGroup = group;

                                        foreach (var w in selectedGroup.weapons)
                                        {
                                            w.block.Outline(true);
                                        }
                                    }
                                    else
                                    {
                                        expanded = !expanded;
                                    }
                                }
                            }
                            GUILayout.EndHorizontal();
                            GUILayout.FlexibleSpace();
                        }
                        GUILayout.EndVertical();
                    }
                    GUILayout.EndHorizontal();


                    if (Event.current.type == EventType.Repaint)
                    {
                        //Scroll to added group
                        var lastrect = GUILayoutUtility.GetLastRect();
                        if (groupAdded && i == module.groups.Count - 1)
                        {
                            groupAdded = false;
                            scroll.y = lastrect.y + lastrect.height / 2;
                        }
                    }

                    if (tempgroup != null && selectedGroup == tempgroup)
                    {
                        if (group == selectedGroup && tempExpanded)
                        {
                            GUILayout.BeginHorizontal();
                            GUILayout.Space(25f);
                            {
                                GUILayout.BeginVertical();
                                //Group weapons
                                for (int j = 0; j < displayWeapons.Count; j++)
                                {
                                    var weapon = displayWeapons[j];
                                    GUILayout.BeginHorizontal(GUI.skin.button);
                                    {
                                        GUILayout.Label(StringLookup.GetItemName(ObjectTypes.Block, (int)weapon.block.BlockType), middledText, GUILayout.Width(250));

                                        GUILayout.FlexibleSpace();

                                        if (GUILayout.Button(icon_remove, icon_width, icon_height))
                                        {
                                            weapon.block.Outline(false);
                                            group.weapons.RemoveAt(j);
                                            if(weapon.hammer)
                                            {
                                                ModuleWeaponGroupController.RemoveGroupForHammer(weapon.hammer, group);
                                            }
                                        }
                                    }
                                    GUILayout.EndHorizontal();

                                    if (Event.current.type == EventType.Repaint && weaponSelected && j == displayWeapons.Count - 1)
                                    {
                                        weaponSelected = false;
                                        var rect = GUILayoutUtility.GetLastRect();
                                        scroll.y = rect.y + rect.height / 2;
                                    }
                                }
                                if (GUILayout.Button(selectingWeapon ? "Selecting weapon..." : "Add weapon to group"))
                                {
                                    selectingWeapon = true;
                                }
                                GUILayout.EndVertical();
                            }
                            GUILayout.EndHorizontal();
                        }
                    }
                }
            }
            GUILayout.EndScrollView();
            //Add group button
            if (GUILayout.Button("Add group"))
            {
                CleanGroup();
                selectedGroup = new ModuleWeaponGroupController.WeaponGroup();
                module.groups.Add(selectedGroup);
                groupAdded = true;
            }

            if (GUILayout.Button("Close"))
            {
                Clean();
            }
            GUILayout.EndVertical();

            GUI.DragWindow();
        }

        private void SelectModule(ModuleWeaponGroupController controller)
        {
            if (module)
            {
                module.block.Outline(false);
            }

            module = controller;
            module.block.Outline(true);
        }

        private void Clean()
        {
            if (!module)
            {
                return;
            }

            module.block.Outline(false);
            module = null;
            useGUILayout = false;
            CleanGroup();
            selectedGroup = null;

            GUI.UnfocusWindow();
        }

        private void CleanGroup()
        {
            if (selectedGroup != null)
            {
                foreach (var w in selectedGroup.weapons)
                {
                    w.block.Outline(false);
                }
            }

            selectingWeapon = false;
            weaponSelected = false;
            groupAdded = false;
            changeKey = false;
            renaming = false;
            expanded = false;
        }
    }
}
