using System;
using System.Collections.Generic;
using EpicLoot.GatedItemType;
using HarmonyLib;
using MonoMod.Cil;
using OC = Mono.Cecil.Cil.OpCodes;

namespace ServersideSimulations_EpicLootPatch.Patches
{
    [Harmony]
    internal class Patches
    {
        [HarmonyPatch(typeof(GatedItemTypeHelper), nameof(GatedItemTypeHelper.GetGatedItemID), new Type[] { typeof(string), typeof(GatedItemTypeMode) })]
        private static class GatedItemTypeHelper_GetGatedItemID_Patch
        {
            private static bool OverrideIsNullLocalPlayer(Player player)
            {
                if (ZNet.instance && ZNet.instance.IsDedicated())
                {
                    return false;
                }
                else
                {
                    return player == null;
                }
            }

            // Remove `Player.m_localPlayer == null` check so we can proceed to `CheckIfItemNeedsGate`
            private static void ILManipulator(ILContext il)
            {
                new ILCursor(il)
                    /*var player = Player.m_localPlayer;
                    if (player == null)
                    {
                        //EpicLoot.LogWarning($"Tried to get gated itemID ({itemID}) with null player! Using itemID");
                        return itemID;
                    }*/
                    .GotoNext(MoveType.After,
                        i => i.MatchLdsfld<Player>("m_localPlayer"),
                        i => i.MatchStloc(0),
                        i => i.MatchLdloc(0),
                        i => i.MatchLdnull(),
                        i => i.MatchCall<UnityEngine.Object>("op_Equality"),
                        i => i.MatchStloc(6),
                        i => i.MatchLdloc(6)
                    )
                    .GotoPrev(MoveType.After,
                        i => i.MatchStloc(6)
                    )
                    .Remove()
                    // player local
                    .Emit(OC.Ldloc, 0)
                    .EmitDelegate<Func<Player, bool>>(OverrideIsNullLocalPlayer);
                ;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GatedItemTypeHelper), "CheckIfItemNeedsGate")]
        private static bool Pre_CheckIfItemNeedsGate(Player player, GatedItemTypeMode mode, string itemName, ref bool __result)
        {
            if (ZNet.instance && ZNet.instance.IsServer())
            {
                __result = PlayerDataSyncManager.CheckIfItemNeedsGate(mode, itemName);
                return false;
            }

            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
        private static void Post_ZNet_OnNewConnection(ZNetPeer peer)
        {
            PlayerDataSyncManager.OnPeerConnect(peer);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        private static void Post_ZNet_Disconnect(ZNetPeer peer)
        {
            PlayerDataSyncManager.OnPeerDisconnect(peer);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static void Post_Player_Load(Player __instance)
        {
            PlayerDataSyncManager.OnPlayerLoad(__instance);
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.AddKnownItem))]
        private static void Pre_Player_AddKnownItem(Player __instance, ItemDrop.ItemData item)
        {
            string itemName = item.m_shared.m_name;
            if (!__instance.m_knownMaterial.Contains(itemName))
            {
                PlayerDataSyncManager.OnPlayerAddKnownItem(itemName);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.AddKnownRecipe))]
        private static void Pre_Player_AddKnownRecipe(Player __instance, Recipe recipe)
        {
            string recipeName = recipe.m_item.m_itemData.m_shared.m_name;
            if (!__instance.m_knownRecipes.Contains(recipeName))
            {
                PlayerDataSyncManager.OnPlayerAddKnownRecipe(recipeName);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Console), nameof(Console.IsConsoleEnabled))]
        private static bool Pre_Console_IsConsoleEnabled(ref bool __result)
        {
            if (ZNet.instance && !ZNet.instance.IsDedicated() && Player.m_localPlayer != null)
            {
                __result = true;
                return false;
            }
            return true;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Console), nameof(Console.InputText))]
        private static bool Pre_Console_InputText(Console __instance)
        {
            if (!Player.m_localPlayer)
            {
                return true;
            }
            string text = __instance.m_input.text;
            if (text == "spoilers")
            {
                var toRemove = new List<string>() {
                    "$item_chest_pcuirass",
                    "$item_legs_pgreaves",
                    "$item_atgeir_blackmetal",
                    "$item_axe_blackmetal",
                    "$item_cape_lox",
                    "$item_knife_blackmetal",
                    "$item_mace_needle",
                    "$item_mace_silver",
                    "$item_shield_blackmetal",
                    "$item_shield_blackmetal_tower",
                    "$item_shield_serpentscale",
                    "$item_sword_blackmetal"
                };
                int removed = Player.m_localPlayer.m_knownMaterial.RemoveWhere(i => toRemove.Contains(i));
                __instance.AddString($"Removed {removed} known items.");
                if (removed > 0)
                {
                    PlayerDataSyncManager.SendKnown(Player.m_localPlayer);
                }
                return false;
            }
            return true;
        }
    }
}
