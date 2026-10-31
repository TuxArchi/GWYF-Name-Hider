using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GwyfNameHider
{
    /// <summary>
    /// Parches Harmony en los puntos donde el juego escribe nombres.
    /// Cada parche se aplica de forma independiente: si uno falla (por ejemplo,
    /// porque el juego cambio en una actualizacion), los demas siguen activos y
    /// el scrubber generico cubre lo que se pueda.
    /// </summary>
    internal static class Patches
    {
        internal static AliasRegistry Registry;
        internal static TextScrubber Scrubber;
        internal static PluginConfig Cfg;
        internal static PluginLog Log;

        private static readonly FieldInfo PlayerNameLabelField =
            AccessTools.Field(typeof(PlayerProfile), "playerNameLabel");

        private static readonly FieldInfo ProfitNameTextsField =
            AccessTools.Field(typeof(ProfitLineGraph3D), "playerNameTexts");

        private static readonly FieldInfo ProfitOrderField =
            AccessTools.Field(typeof(ProfitLineGraph3D), "playerOrder");

        public static void Init(AliasRegistry registry, TextScrubber scrubber, PluginConfig cfg, PluginLog log)
        {
            Registry = registry;
            Scrubber = scrubber;
            Cfg = cfg;
            Log = log;
        }

        public static void Apply(Harmony harmony)
        {
            // --- Nombre sobre la cabeza y datos del jugador -------------------
            Patch(harmony, typeof(PlayerProfile), "SetPlayerNameTag", Type.EmptyTypes,
                null, nameof(Post_PlayerProfile_SetPlayerNameTag));

            Patch(harmony, typeof(PlayerProfile), "OnSync", new[] { typeof(bool), typeof(bool) },
                null, nameof(Post_PlayerProfile_OnSync));

            Patch(harmony, typeof(PlayerProfile), "OnStartClient", Type.EmptyTypes,
                null, nameof(Post_RequestScrub));

            // --- Dinero: historial y textos flotantes -------------------------
            Patch(harmony, typeof(MoneyDisplayAndFeedbacks), "GetPlayerDisplayName", new[] { typeof(ulong) },
                null, nameof(Post_GetPlayerDisplayName));

            Patch(harmony, typeof(MoneyDisplayAndFeedbacks), "SpawnMoneyChangeText", new[] { typeof(BalanceChangeData) },
                null, nameof(Post_RequestScrub));

            // --- Fin de partida ------------------------------------------------
            Patch(harmony, typeof(GameOverUI), "GetCurrentPlayerNamesById", Type.EmptyTypes,
                null, nameof(Post_GetCurrentPlayerNamesById));

            Patch(harmony, typeof(GameLostContributionEntry), "Setup", new[] { typeof(string), typeof(BigNumber) },
                nameof(Pre_GameLostContributionEntry_Setup), null);

            // --- Grafica de beneficios ------------------------------------------
            Patch(harmony, typeof(ProfitLineGraph3D), "UpdatePlayerNameText", new[] { typeof(int), typeof(Vector3) },
                null, nameof(Post_UpdatePlayerNameText));

            // --- Creditos --------------------------------------------------------
            Patch(harmony, typeof(CreditsRollManager), "BuildSnapshotsFromScenePlayers", Type.EmptyTypes,
                null, nameof(Post_BuildSnapshotsFromScenePlayers));

            // --- Menu Escape / lista de voz ---------------------------------------
            Patch(harmony, typeof(EscapeMenuVoiceList), "ResolveDisplayNameAndSteamId", new[] { typeof(string) },
                null, nameof(Post_ResolveDisplayNameAndSteamId));

            Patch(harmony, typeof(VoiceVolumeEntry), "UpdateIdentity", new[] { typeof(string), typeof(string) },
                nameof(Pre_VoiceVolumeEntry_UpdateIdentity), null);

            // --- Lista de miembros del lobby --------------------------------------
            Patch(harmony, typeof(LobbyMemberList), "UpdateMemberEntry", new[] { typeof(CSteamID), typeof(GameObject) },
                null, nameof(Post_LobbyMemberList_UpdateMemberEntry));

            Patch(harmony, typeof(LobbyMemberList), "AddMemberEntry", new[] { typeof(CSteamID) },
                null, nameof(Post_RequestScrub));
        }

        private static void Patch(Harmony harmony, Type type, string methodName, Type[] args,
            string prefix, string postfix)
        {
            try
            {
                MethodInfo original = args == null || args.Length == 0
                    ? AccessTools.Method(type, methodName, Type.EmptyTypes)
                    : AccessTools.Method(type, methodName, args);

                if (original == null)
                {
                    Log.Warning($"[Parches] No se encontro {type.Name}.{methodName}; se omite (el scrubber generico lo cubrira).");
                    return;
                }

                var prefixMethod = prefix == null ? null : new HarmonyMethod(typeof(Patches), prefix);
                var postfixMethod = postfix == null ? null : new HarmonyMethod(typeof(Patches), postfix);

                harmony.Patch(original, prefixMethod, postfixMethod);
                Log.Info($"[Parches] OK: {type.Name}.{methodName}");
            }
            catch (Exception e)
            {
                Log.Error($"[Parches] Fallo al parchear {type.Name}.{methodName}: {e}");
            }
        }

        // ------------------------------------------------------------------
        // Nombre sobre la cabeza
        // ------------------------------------------------------------------

        private static void Post_PlayerProfile_SetPlayerNameTag(PlayerProfile __instance)
        {
            ApplyProfileLabel(__instance);
        }

        private static void Post_PlayerProfile_OnSync(PlayerProfile __instance)
        {
            ApplyProfileLabel(__instance);
        }

        private static void ApplyProfileLabel(PlayerProfile profile)
        {
            try
            {
                if (profile == null || PlayerNameLabelField == null) return;

                var label = PlayerNameLabelField.GetValue(profile) as TextMeshProUGUI;
                if (label == null) return;

                var alias = Registry?.AliasFor(profile.steamId, profile.playerName);
                if (alias != null) label.text = alias;
            }
            catch (Exception e)
            {
                Log.Debug("[Parche] SetPlayerNameTag: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Dinero
        // ------------------------------------------------------------------

        private static void Post_GetPlayerDisplayName(ulong steamId, ref string __result)
        {
            try
            {
                var alias = Registry?.AliasFor(steamId, __result);
                if (alias != null) __result = alias;
            }
            catch (Exception e)
            {
                Log.Debug("[Parche] GetPlayerDisplayName: " + e.Message);
            }
        }

        private static void Post_RequestScrub()
        {
            Scrubber?.RequestRescan();
        }

        // ------------------------------------------------------------------
        // Fin de partida
        // ------------------------------------------------------------------

        private static void Post_GetCurrentPlayerNamesById(Dictionary<ulong, string> __result)
        {
            try
            {
                if (__result == null || Registry == null) return;

                var keys = new List<ulong>(__result.Keys);
                for (int i = 0; i < keys.Count; i++)
                {
                    var id = keys[i];
                    var alias = Registry.AliasFor(id, __result[id]);
                    if (alias != null) __result[id] = alias;
                }
            }
            catch (Exception e)
            {
                Log.Debug("[Parche] GetCurrentPlayerNamesById: " + e.Message);
            }
        }

        private static void Pre_GameLostContributionEntry_Setup(ref string playerName)
        {
            try
            {
                var alias = Registry?.AliasForName(playerName);
                if (alias != null) playerName = alias;
            }
            catch (Exception e)
            {
                Log.Debug("[Parche] GameLostContributionEntry.Setup: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Grafica
        // ------------------------------------------------------------------

        private static void Post_UpdatePlayerNameText(ProfitLineGraph3D __instance, int playerIndex)
        {
            try
            {
                if (__instance == null || Registry == null || ProfitNameTextsField == null) return;

                var texts = ProfitNameTextsField.GetValue(__instance) as List<GameObject>;
                if (texts == null || playerIndex < 0 || playerIndex >= texts.Count) return;

                var textObject = texts[playerIndex];
                if (textObject == null) return;

                ulong steamId = 0;
                string playerName = null;

                var order = ProfitOrderField?.GetValue(__instance) as List<PlayerProfile>;
                if (order != null && playerIndex < order.Count && order[playerIndex] != null)
                {
                    steamId = order[playerIndex].steamId;
                    playerName = order[playerIndex].playerName;
                }

                var alias = Registry.AliasFor(steamId, playerName);
                if (alias == null) return;

                var tmp = textObject.GetComponent<TMP_Text>();
                if (tmp != null) { tmp.text = alias; return; }

                var ugui = textObject.GetComponent<UnityEngine.UI.Text>();
                if (ugui != null) ugui.text = alias;
            }
            catch (Exception e)
            {
                Log.Debug("[Parche] UpdatePlayerNameText: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Creditos
        // ------------------------------------------------------------------

        private static void Post_BuildSnapshotsFromScenePlayers(List<PlayerCreditsSnapshot> __result)
        {
            try
            {
                if (__result == null || Registry == null) return;

                for (int i = 0; i < __result.Count; i++)
                {
                    var snapshot = __result[i];
                    if (snapshot == null) continue;

                    var alias = Registry.AliasFor(snapshot.steamId, snapshot.displayName);
                    if (alias != null) snapshot.displayName = alias;
                }
            }
            catch (Exception e)
            {
                Log.Debug("[Parche] BuildSnapshotsFromScenePlayers: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Menu Escape / voz
        // ------------------------------------------------------------------

        private static void Post_ResolveDisplayNameAndSteamId(ref ValueTuple<string, string, bool> __result)
        {
            try
            {
                if (!__result.Item3 || Registry == null) return;

                ulong steamId = 0;
                ulong.TryParse(__result.Item2, out steamId);

                var alias = Registry.AliasFor(steamId, __result.Item1);
                if (alias != null)
                    __result = new ValueTuple<string, string, bool>(alias, __result.Item2, true);
            }
            catch (Exception e)
            {
                Log.Debug("[Parche] ResolveDisplayNameAndSteamId: " + e.Message);
            }
        }

        private static void Pre_VoiceVolumeEntry_UpdateIdentity(ref string displayName, string steamId)
        {
            try
            {
                if (Registry == null) return;

                ulong id = 0;
                ulong.TryParse(steamId, out id);

                var alias = Registry.AliasFor(id, displayName);
                if (alias != null) displayName = alias;
            }
            catch (Exception e)
            {
                Log.Debug("[Parche] VoiceVolumeEntry.UpdateIdentity: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Lista de miembros del lobby
        // ------------------------------------------------------------------

        private static void Post_LobbyMemberList_UpdateMemberEntry(CSteamID id, GameObject entry)
        {
            try
            {
                if (entry == null) return;

                var alias = Registry?.AliasFor(id.m_SteamID);

                var tmp = entry.GetComponentInChildren<TMP_Text>();
                if (tmp != null)
                {
                    if (alias != null) tmp.text = alias;
                }
                else
                {
                    var ugui = entry.GetComponentInChildren<UnityEngine.UI.Text>();
                    if (ugui != null && alias != null) ugui.text = alias;
                }

                Scrubber?.ScrubObject(entry);
            }
            catch (Exception e)
            {
                Log.Debug("[Parche] UpdateMemberEntry: " + e.Message);
            }
        }
    }
}
