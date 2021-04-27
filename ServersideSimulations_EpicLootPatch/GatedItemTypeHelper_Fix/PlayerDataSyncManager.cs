using System.Collections.Generic;
using System.Linq;
using EpicLoot.GatedItemType;

namespace ServersideSimulations_EpicLootPatch
{
    public static class PlayerDataSyncManager
    {
        private static readonly string Name_RPC_RequestKnown = "ServerSim_RequestKnown";
        private static readonly string Name_RPC_Known = "ServerSim_Known";
        private static readonly string Name_RPC_AddKnownRecipe = "ServerSim_AddKnownRecipe";
        private static readonly string Name_RPC_AddKnownMaterial = "ServerSim_AddKnownMaterial";

        public static Dictionary<long, HashSet<string>> playerKnownRecipes = new Dictionary<long, HashSet<string>>();
        public static Dictionary<long, HashSet<string>> playerKnownMaterial = new Dictionary<long, HashSet<string>>();

        private static bool playerLoaded = false;
        private static bool requestedKnown = false;

        public static void OnPeerConnect(ZNetPeer peer)
        {
            if (!ZNet.instance) return;
            bool isServer = ZNet.instance.IsServer();
            if (isServer)
            {
                peer.m_rpc.Register<ZPackage>(Name_RPC_Known, RPC_Known);
                peer.m_rpc.Register<string>(Name_RPC_AddKnownRecipe, RPC_AddKnownRecipe);
                peer.m_rpc.Register<string>(Name_RPC_AddKnownMaterial, RPC_AddKnownMaterial);
            }
            else
            {
                peer.m_rpc.Register(Name_RPC_RequestKnown, RPC_RequestKnown);
            }
            peer.m_rpc.Invoke(Name_RPC_RequestKnown);
        }

        public static void OnPeerDisconnect(ZNetPeer peer)
        {
            if (playerKnownRecipes.ContainsKey(peer.m_uid))
            {
                playerKnownRecipes.Remove(peer.m_uid);
            }
            if (playerKnownMaterial.ContainsKey(peer.m_uid))
            {
                playerKnownMaterial.Remove(peer.m_uid);
            }
        }

        public static void OnPlayerLoad(Player player)
        {
            playerLoaded = true;
            if (requestedKnown)
            {
                SendKnown(player);
                requestedKnown = false;
            }
        }

        public static void OnPlayerAddKnownItem(string itemName)
        {
            ZNet.instance?.GetServerPeer()?.m_rpc.Invoke(Name_RPC_AddKnownMaterial, itemName);
        }

        public static void OnPlayerAddKnownRecipe(string recipeName)
        {
            ZNet.instance?.GetServerPeer()?.m_rpc.Invoke(Name_RPC_AddKnownRecipe, recipeName);
        }

        public static void RPC_RequestKnown(ZRpc rpc)
        {
            requestedKnown = true;
            Player player = Player.m_localPlayer;
            if (player && playerLoaded)
            {
                SendKnown(player);
            }
        }

        public static void RPC_Known(ZRpc rpc, ZPackage pkg)
        {
            ZNetPeer peer = ZNet.instance.GetPeer(rpc);
            long sender = peer.m_uid;

            int materialCount = LoadKnownFromPkg(sender, pkg, playerKnownMaterial);
            int recipeCount = LoadKnownFromPkg(sender, pkg, playerKnownRecipes);

            Plugin.logger.LogInfo($"Received known from peer {sender}: {materialCount} materials / {recipeCount} recipes");
        }

        public static void RPC_AddKnownRecipe(ZRpc rpc, string recipe)
        {
            ZNetPeer peer = ZNet.instance.GetPeer(rpc);
            long sender = peer.m_uid;
            HashSet<string> knownRecipes;
            if (!playerKnownRecipes.TryGetValue(sender, out knownRecipes))
            {
                Plugin.logger.LogWarning($"RPC_AddKnownRecipe: hashset is null for peer {sender}");
                return;
            }
            knownRecipes.Add(recipe);
            Plugin.logger.LogInfo($"Received add known recipe from peer {sender}: {recipe}");
        }

        public static void RPC_AddKnownMaterial(ZRpc rpc, string material)
        {
            ZNetPeer peer = ZNet.instance.GetPeer(rpc);
            long sender = peer.m_uid;
            HashSet<string> knownMaterial;
            if (!playerKnownMaterial.TryGetValue(sender, out knownMaterial))
            {
                Plugin.logger.LogWarning($"RPC_AddKnownMaterial: hashset is null for peer {sender}");
                return;
            }
            knownMaterial.Add(material);
            Plugin.logger.LogInfo($"Received add known material from peer {sender}: {material}");
        }

        public static void SendKnown(Player player)
        {
            if (player == null)
            {
                Plugin.logger.LogError("PlayerDataSyncManager.ClientSend: m_localPlayer == null");
                return;
            }

            ZPackage pkg = new ZPackage();

            Plugin.logger.LogInfo($"Sending {player.m_knownMaterial.Count} known materials.");
            WriteKnown(player.m_knownMaterial, pkg);

            Plugin.logger.LogInfo($"Sending {player.m_knownRecipes.Count} known recipes.");
            WriteKnown(player.m_knownRecipes, pkg);

            ZNetPeer serverPeer = ZNet.instance.GetServerPeer();
            serverPeer.m_rpc.Invoke(Name_RPC_Known, pkg);
        }

        public static int LoadKnownFromPkg(long sender, ZPackage pkg, Dictionary<long, HashSet<string>> outDict)
        {
            ZPackage pkgKnown = pkg.ReadPackage();
            var tempKnown = new HashSet<string>();
            int numKnown = pkgKnown.ReadInt();
            for (int i = 0; i < numKnown; i++)
            {
                tempKnown.Add(pkgKnown.ReadString());
            }
            if (outDict.ContainsKey(sender))
            {
                outDict.Remove(sender);
            }
            outDict.Add(sender, tempKnown);
            return tempKnown.Count;
        }

        public static void WriteKnown(HashSet<string> known, ZPackage outPkg)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(known.Count);
            foreach (string s in known)
            {
                pkg.Write(s);
            }
            outPkg.Write(pkg);
        }

        public static bool CheckIfItemNeedsGate(GatedItemTypeMode mode, string itemName)
        {
            bool result;
            switch (mode)
            {
                case GatedItemTypeMode.MustKnowRecipe:
                    result = !playerKnownRecipes.Values.Any(recipes => recipes.Contains(itemName));
                    break;
                case GatedItemTypeMode.MustHaveCrafted:
                    result = !playerKnownMaterial.Values.Any(material => material.Contains(itemName));
                    break;
                default:
                    result = false;
                    break;
            }
            Plugin.logger.LogInfo($"Overriding CheckIfItemNeedsGate (mode {mode} / item {itemName} / result {result}");
            return result;
        }
    }
}
