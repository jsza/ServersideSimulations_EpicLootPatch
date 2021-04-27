using System.Reflection;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ServersideSimulations_EpicLootPatch
{
    [BepInPlugin("MVP.ServersideSimulations_EpicLootPatch", "ServersideSimulations_EpicLootPatch", "0.0.0")]
    [BepInDependency("randyknapp.mods.epicloot")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource logger;
        public static Plugin instance;

        private void Awake()
        {
            instance = this;
            logger = this.Logger;
            //Assembly assembly = typeof(Plugin).Assembly;
            //Harmony harmony = new Harmony("MVP.Valheim_Serverside_Simulations");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly(), "ServersideSimulations_EpicLootPatch");
        }
    }
}
