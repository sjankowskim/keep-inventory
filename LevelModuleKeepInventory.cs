using ThunderRoad;
using HarmonyLib;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;
using System.Collections;
using System;

namespace KeepInventory
{
    internal class LevelModuleKeepInventory : LevelModulePersistence
    {
        private static LevelModuleKeepInventory _local;
        private static string modTag = "(Keep Inventory)";

        public override IEnumerator OnLoadCoroutine()
        {
            _local = this;
            Debug.Log(modTag + " Loaded successfully!");
            new Harmony("OnUnload").PatchAll();
            EventManager.onCreatureKill += CreatureKillEvent;
            EventManager.onPossess += ReturnHandItems;
            return base.OnLoadCoroutine();
        }

        private static void CreatureKillEvent(Creature creature, Player player, CollisionInstance collisionStruct, EventTime eventTime) 
        {
            if (player != null && eventTime == EventTime.OnStart)
            {
                _local.SaveHands();
                _local.OnUnload();
            }
        }

         [HarmonyPatch(typeof(LevelModule), "OnUnload")]
         class UnloadPatch
        {
            static bool Prefix()
            {
                if (!Player.currentCreature.isKilled)
                {
                    _local.SaveHands();
                    _local.OnUnload();
                }
                return true;
            }
        }

        private void SaveHands()
        {
            var handSaveHandler = new HandSave();

            if (Player.currentCreature.equipment.GetHeldItem(Side.Left) || Player.currentCreature.equipment.GetHeldItem(Side.Right))
            {
                string leftId = null, rightId = null;
                if (Player.currentCreature.equipment.GetHeldItem(Side.Left))
                    leftId = Player.currentCreature.equipment.GetHeldItem(Side.Left).data.id;
                if (Player.currentCreature.equipment.GetHeldItem(Side.Right))
                    rightId = Player.currentCreature.equipment.GetHeldItem(Side.Right).data.id;

                if (leftId != null && Player.currentCreature.equipment.GetHeldItem(Side.Left) == Player.currentCreature.equipment.GetHeldItem(Side.Right)) // if both hands are holding one item
                {
                    if (Player.currentCreature.equipment.GetHeldItem(Side.Left).mainHandler.side == Side.Left)
                        handSaveHandler.leftId = leftId;
                    else
                        handSaveHandler.rightId = rightId;
                }
                else
                {
                    handSaveHandler.leftId = leftId;
                    handSaveHandler.rightId = rightId;
                }
            }

            try
            {
                File.WriteAllText(Application.streamingAssetsPath + "\\Mods\\KeepInventory_U10\\HandLoadout.txt", JsonConvert.SerializeObject(handSaveHandler));
            }
            catch (FileNotFoundException)
            {
                Debug.LogError(modTag + " Unable write to HandLoadout.txt. Contact mod developer!");
            }
        }

        private static void ReturnHandItems(Creature newBody, EventTime eventTime)
        {
            if (eventTime == EventTime.OnEnd)
            {
                try
                {
                    if (Player.local != null)
                    {
                        var handSaveContainer = JsonConvert.DeserializeObject<HandSave>(File.ReadAllText(Application.streamingAssetsPath + "\\Mods\\KeepInventory_U10\\HandLoadout.txt"));
                        if (handSaveContainer.leftId != null)
                        {
                            Catalog.GetData<ItemData>(handSaveContainer.leftId).SpawnAsync(item =>
                            {
                                if (item && !newBody.GetHand(Side.Left).grabbedHandle)
                                    newBody.GetHand(Side.Left).Grab(item.GetMainHandle(Side.Left), true);
                            });
                        }
                        if (handSaveContainer.rightId != null)
                        {
                            Catalog.GetData<ItemData>(handSaveContainer.rightId).SpawnAsync(item =>
                            {
                                if (item && !newBody.GetHand(Side.Right).grabbedHandle)
                                    newBody.GetHand(Side.Right).Grab(item.GetMainHandle(Side.Right), true);
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    if (e is FileNotFoundException)
                        Debug.LogWarning(modTag + " Couldn't read from HandLoadout.txt!");
                    else if (e is NullReferenceException)
                        Debug.LogWarning(modTag + " One or both of your saved items no longer exists! See the error above for the name of the item(s)!");
                }
            }
        }

        public class HandSave
        {
            public string leftId;
            public string rightId;
        }
    }
}