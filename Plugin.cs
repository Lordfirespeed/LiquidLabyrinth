﻿using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using System.Text;
using UnityEngine;
using LiquidLabyrinth.ItemHelpers;
using LiquidLabyrinth.Patches;
using Unity.Netcode;
using System.Reflection;

namespace LiquidLabyrinth
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("evaisa.lethallib")]
    [BepInProcess("Lethal Company.exe")]
    public class LiquidLabyrinthBase : BaseUnityPlugin
    {

        // TODO: USE SaveLocalPlayerValues METHOD FROM GAMENETWORKMANAGER TO SAVE THE BOTTLE NAMES!
        internal static new ManualLogSource Logger;
        internal static LiquidLabyrinthBase Instance;
        internal static int bottlesAdded = 1;
        private readonly Harmony Harmony = new(PluginInfo.PLUGIN_GUID);


        private void NetcodeWeaver()
        {
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
        }

        private void Awake()
        {
            // Plugin startup logic
            Instance = this;
            Logger = base.Logger;
            NetcodeWeaver();
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!!!!!!!!");
            StringBuilder sb = new();
            sb.AppendLine();
            sb.AppendLine(" ___      ___   _______  __   __  ___   ______     ___      _______  _______  __   __  ______    ___   __    _  _______  __   __ ");
            sb.AppendLine("|   |    |   | |       ||  | |  ||   | |      |   |   |    |   _   ||  _    ||  | |  ||    _ |  |   | |  |  | ||       ||  | |  |");
            sb.AppendLine("|   |    |   | |   _   ||  | |  ||   | |  _    |  |   |    |  |_|  || |_|   ||  |_|  ||   | ||  |   | |   |_| ||_     _||  |_|  |");
            sb.AppendLine("|   |    |   | |  | |  ||  |_|  ||   | | | |   |  |   |    |       ||       ||       ||   |_||_ |   | |       |  |   |  |       |");
            sb.AppendLine("|   |___ |   | |  |_|  ||       ||   | | |_|   |  |   |___ |       ||  _   | |_     _||    __  ||   | |  _    |  |   |  |       |");
            sb.AppendLine("|       ||   | |      | |       ||   | |       |  |       ||   _   || |_|   |  |   |  |   |  | ||   | | | |   |  |   |  |   _   |");
            sb.AppendLine("|_______||___| |____||_||_______||___| |______|   |_______||__| |__||_______|  |___|  |___|  |_||___| |_|  |__|  |___|  |__| |__|");
            Logger.LogWarning(sb.ToString());



            // Bundle loader.

            var bundle = AssetBundle.LoadFromMemory(Properties.Resources.liquidlabyrinth);
            Item item = bundle.LoadAsset<Item>("Assets/Liquid Labyrinth/BottleItem.asset");
            if(item != null)
            {
                if (item.spawnPrefab.GetComponent<NetworkObject>() is NetworkObject obj && obj != null)
                {
                    LethalLib.Modules.NetworkPrefabs.RegisterNetworkPrefab(item.spawnPrefab);
                    Logger.LogWarning("network prefab added");
                }
                // Register the network prefab before registering items.
                LethalLib.Modules.Items.RegisterScrap(item, 100, LethalLib.Modules.Levels.LevelTypes.All);
                LethalLib.Modules.Items.RegisterShopItem(item, -1);
            }
            else
            {
                Logger.LogWarning("AAAAAAAAAAAA");
            }

            Harmony.PatchAll(typeof(StartOfRoundPatch));
            Harmony.PatchAll(typeof(GameNetworkManagerPatch));
        }
    }
}