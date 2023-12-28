﻿using HarmonyLib;
using LiquidLabyrinth.ItemHelpers;
using LiquidLabyrinth.Utilities;
using LiquidLabyrinth.Utilities.MonoBehaviours;
using UnityEngine;

namespace LiquidLabyrinth.Patches
{
    [HarmonyPatch(typeof(GameNetworkManager))]
    class GameNetworkManagerPatch
    {
        [HarmonyPatch("SaveItemsInShip")]
        static void Postfix()
        {
            GrabbableObject[] array = Object.FindObjectsByType<GrabbableObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (array == null || array.Length == 0)
            {
                return;
            }
            foreach(GrabbableObject item in array)
            {
                if (item.itemProperties.saveItemVariable && item.GetType().Equals(typeof(PotionBottle)))
                {
                    LiquidLabyrinthBase.Logger.Log(BepInEx.Logging.LogLevel.All, "FOUND BOTTLE HAHA");
                    //CoroutineHandler.Instance.NewCoroutine(SaveUtils.ProcessQueueAfterDelay<PotionBottle>(item.GetType(), 0.5f));
                }
            }
        }
    }
}