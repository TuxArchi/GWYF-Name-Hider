using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GwyfNameHider
{
    /// <summary>
    /// Mantiene la relacion estable SteamID -> numero de alias.
    ///
    /// Reglas:
    ///  - Usuario 1 = anfitrion del lobby.
    ///  - El resto recibe numeros por orden de aparicion en el lobby.
    ///  - Un numero nunca se reutiliza mientras dure el lobby: si alguien se va,
    ///    los demas conservan su numero y los nuevos reciben el siguiente.
    ///  - Al cambiar de lobby (o al salir de uno) se reinicia la numeracion.
    /// </summary>
    internal sealed class AliasRegistry
    {
        private struct Record
        {
            public ulong Id;
            public string Name;
        }

        private readonly PluginConfig _cfg;
        private readonly PluginLog _log;

        private readonly Dictionary<ulong, int> _numberById = new Dictionary<ulong, int>();
        private readonly Dictionary<string, string> _aliasByName = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, string>> _patterns = new List<KeyValuePair<string, string>>();

        private readonly List<Record> _records = new List<Record>();
        private readonly HashSet<ulong> _seen = new HashSet<ulong>();

        private LobbySettings _lobbySettings;
        private float _nextLobbySettingsSearch;
        private ulong _lobbyId;
        private ulong _localId;
        private int _nextNumber = 2;
        private bool _patternsDirty = true;
        private int _version;
        private bool _steamWarningLogged;

        public AliasRegistry(PluginConfig cfg, PluginLog log)
        {
            _cfg = cfg;
            _log = log;
        }

        /// <summary>Se incrementa cuando cambia el conjunto de sustituciones.</summary>
        public int Version => _version;

        public int Count => _numberById.Count;

        public void Refresh()
        {
            try
            {
                RefreshCore();
            }
            catch (Exception e)
            {
                _log.Debug("[Alias] Error al refrescar: " + e);
            }
        }

        private void RefreshCore()
        {
            var lobbySettings = GetLobbySettings();

            ulong lobbyId = 0;
            try
            {
                if (lobbySettings != null && lobbySettings.inALobby && lobbySettings.steamLobbyID.m_SteamID != 0)
                    lobbyId = lobbySettings.steamLobbyID.m_SteamID;
            }
            catch (Exception)
            {
                // Steam todavia no esta listo; se intentara en el siguiente refresco.
            }

            if (lobbyId != 0 && lobbyId != _lobbyId)
            {
                if (_lobbyId != 0)
                {
                    _log.Info("[Alias] Nuevo lobby detectado, reiniciando numeracion.");
                    ResetInternal();
                }
                _lobbyId = lobbyId;
            }

            _records.Clear();
            _seen.Clear();

            if (lobbySettings != null)
            {
                try
                {
                    var players = lobbySettings.players;
                    if (players != null)
                    {
                        for (int i = 0; i < players.Count; i++)
                        {
                            var p = players[i];
                            if (p == null || p.steamId == 0) continue;
                            AddCandidate(p.steamId, p.playerName);
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            try
            {
                var profiles = Object.FindObjectsByType<PlayerProfile>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < profiles.Length; i++)
                {
                    var profile = profiles[i];
                    if (profile == null || profile.steamId == 0) continue;
                    AddCandidate(profile.steamId, profile.playerName);
                }
            }
            catch (Exception)
            {
            }

            ulong localId = GetLocalSteamId();
            _localId = localId;
            if (localId != 0)
                AddCandidate(localId, GetPersonaName(localId, true));

            ulong hostId = GetHostId(lobbySettings);
            if (hostId == 0)
                hostId = localId != 0 ? localId : (_records.Count > 0 ? _records[0].Id : 0);

            if (hostId != 0)
                EnsureHostIsFirst(hostId);

            for (int i = 0; i < _records.Count; i++)
            {
                var rec = _records[i];
                if (!_numberById.ContainsKey(rec.Id))
                    AssignNewNumber(rec.Id);
                RegisterName(rec);
            }
        }

        private void AddCandidate(ulong id, string name)
        {
            if (id == 0) return;
            if (!_seen.Add(id)) return;

            if (LooksLikePlaceholder(name, id))
                name = GetPersonaName(id, id == _localId);

            _records.Add(new Record { Id = id, Name = name });
        }

        /// <summary>El juego usa "Player &lt;steamid&gt;" cuando no hay nombre disponible.</summary>
        private static bool LooksLikePlaceholder(string name, ulong id)
        {
            if (string.IsNullOrEmpty(name)) return true;
            if (!name.StartsWith("Player ", StringComparison.Ordinal)) return false;

            var tail = name.Substring(7);
            if (tail.Length == 0) return false;

            for (int i = 0; i < tail.Length; i++)
            {
                if (!char.IsDigit(tail[i])) return false;
            }

            return true;
        }

        private void EnsureHostIsFirst(ulong hostId)
        {
            int current;
            if (_numberById.TryGetValue(hostId, out current) && current == 1)
                return;

            ulong holderOfOne = 0;
            foreach (var kv in _numberById)
            {
                if (kv.Value == 1) { holderOfOne = kv.Key; break; }
            }

            if (_numberById.TryGetValue(hostId, out current))
            {
                // El anfitrion ya tenia numero (caso raro): se intercambia con quien tenga el 1.
                if (holderOfOne != 0 && holderOfOne != hostId)
                    _numberById[holderOfOne] = current;
                _numberById[hostId] = 1;
                _log.Info($"[Alias] El anfitrion pasa a Usuario 1 (intercambio con {holderOfOne}).");
            }
            else
            {
                if (holderOfOne != 0)
                {
                    int replacement = AllocateNumber();
                    _numberById[holderOfOne] = replacement;
                    _log.Info($"[Alias] El anfitrion pasa a Usuario 1; {holderOfOne} pasa a Usuario {replacement}.");
                }
                _numberById[hostId] = 1;
            }

            _patternsDirty = true;
        }

        private int AllocateNumber()
        {
            int number = _nextNumber;
            _nextNumber++;
            return number;
        }

        private void AssignNewNumber(ulong id)
        {
            int number = AllocateNumber();
            _numberById[id] = number;
            _log.Info($"[Alias] {id} -> {_cfg.FormatAlias(number)}");
        }

        private void RegisterName(Record record)
        {
            if (string.IsNullOrEmpty(record.Name)) return;

            int number;
            if (!_numberById.TryGetValue(record.Id, out number)) return;

            string alias = _cfg.FormatAlias(number);
            string existing;
            if (_aliasByName.TryGetValue(record.Name, out existing))
            {
                if (existing == alias) return;
                _log.Debug($"[Alias] Nombre duplicado '{record.Name}': se usa '{alias}' en lugar de '{existing}'.");
            }

            _aliasByName[record.Name] = alias;
            _patternsDirty = true;
        }

        // ------------------------------------------------------------------
        // Consultas
        // ------------------------------------------------------------------

        public string AliasFor(ulong steamId, string fallbackName = null)
        {
            int number;
            if (steamId != 0 && _numberById.TryGetValue(steamId, out number))
                return _cfg.FormatAlias(number);

            if (!string.IsNullOrEmpty(fallbackName))
                return AliasForName(fallbackName);

            return null;
        }

        public string AliasForName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string alias;
            return _aliasByName.TryGetValue(name, out alias) ? alias : null;
        }

        // ------------------------------------------------------------------
        // Sustitucion de texto
        // ------------------------------------------------------------------

        public string ReplaceText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            if (_patternsDirty) RebuildPatterns();
            if (_patterns.Count == 0) return text;

            StringBuilder sb = null;
            int i = 0;

            while (i < text.Length)
            {
                int match = -1;
                for (int p = 0; p < _patterns.Count; p++)
                {
                    if (MatchesAt(text, i, _patterns[p].Key))
                    {
                        match = p;
                        break;
                    }
                }

                if (match >= 0)
                {
                    if (sb == null)
                    {
                        sb = new StringBuilder(text.Length + 16);
                        sb.Append(text, 0, i);
                    }

                    sb.Append(_patterns[match].Value);
                    i += _patterns[match].Key.Length;
                }
                else
                {
                    if (sb != null) sb.Append(text[i]);
                    i++;
                }
            }

            return sb == null ? text : sb.ToString();
        }

        private void RebuildPatterns()
        {
            _patternsDirty = false;
            _patterns.Clear();

            int minLength = Math.Max(1, _cfg.MinNameLength.Value);

            foreach (var kv in _aliasByName)
            {
                if (string.IsNullOrEmpty(kv.Key)) continue;
                if (string.Equals(kv.Key, kv.Value, StringComparison.Ordinal)) continue;
                if (kv.Key.Trim().Length < minLength) continue;
                _patterns.Add(kv);
            }

            // Los nombres mas largos primero, para que "Juan Perez" gane a "Juan".
            _patterns.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
            _version++;
        }

        private static bool MatchesAt(string text, int index, string name)
        {
            if (index + name.Length > text.Length) return false;
            if (string.CompareOrdinal(text, index, name, 0, name.Length) != 0) return false;

            if (index > 0 && IsWordChar(text[index - 1])) return false;

            int end = index + name.Length;
            if (end < text.Length && IsWordChar(text[end])) return false;

            return true;
        }

        private static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        // ------------------------------------------------------------------
        // Steam / lobby
        // ------------------------------------------------------------------

        private LobbySettings GetLobbySettings()
        {
            if (_lobbySettings != null) return _lobbySettings;
            if (Time.unscaledTime < _nextLobbySettingsSearch) return null;
            _nextLobbySettingsSearch = Time.unscaledTime + 2f;

            try
            {
                var all = Resources.FindObjectsOfTypeAll<LobbySettings>();
                LobbySettings fallback = null;

                for (int i = 0; i < all.Length; i++)
                {
                    var ls = all[i];
                    if (ls == null) continue;
                    if (ls.inALobby) { _lobbySettings = ls; return ls; }
                    if (fallback == null) fallback = ls;
                }

                if (fallback != null)
                {
                    _lobbySettings = fallback;
                    return fallback;
                }

                _lobbySettings = Resources.Load<LobbySettings>("LobbySettings");
                return _lobbySettings;
            }
            catch (Exception e)
            {
                _log.Debug("[Alias] No se pudo localizar LobbySettings: " + e.Message);
                return null;
            }
        }

        private ulong GetHostId(LobbySettings lobbySettings)
        {
            try
            {
                if (lobbySettings == null || !lobbySettings.inALobby) return 0;

                var lobby = lobbySettings.steamLobbyID;
                if (lobby.m_SteamID == 0) return 0;

                var owner = SteamMatchmaking.GetLobbyOwner(lobby);
                return owner.m_SteamID;
            }
            catch (Exception e)
            {
                WarnSteamOnce(e);
                return 0;
            }
        }

        private static ulong GetLocalSteamId()
        {
            try
            {
                return SteamUser.GetSteamID().m_SteamID;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private string GetPersonaName(ulong id, bool local)
        {
            try
            {
                if (local) return SteamFriends.GetPersonaName();

                var name = SteamFriends.GetFriendPersonaName(new CSteamID(id));
                return string.IsNullOrEmpty(name) ? null : name;
            }
            catch (Exception e)
            {
                WarnSteamOnce(e);
                return null;
            }
        }

        private void WarnSteamOnce(Exception e)
        {
            if (_steamWarningLogged) return;
            _steamWarningLogged = true;
            _log.Debug("[Alias] Steam no disponible todavia: " + e.Message);
        }

        // ------------------------------------------------------------------
        // Reset / diagnostico
        // ------------------------------------------------------------------

        private void ResetInternal()
        {
            _numberById.Clear();
            _aliasByName.Clear();
            _patterns.Clear();
            _lobbyId = 0;
            _nextNumber = 2;
            _patternsDirty = true;
            _version++;
        }

        public void Reset()
        {
            ResetInternal();
        }

        public void DumpTo(PluginLog log)
        {
            log.Info("=== GWYF Name Hider: alias asignados ===");

            if (_numberById.Count == 0)
            {
                log.Info("(ninguno todavia)");
                return;
            }

            var ids = new List<ulong>(_numberById.Keys);
            ids.Sort((a, b) => _numberById[a].CompareTo(_numberById[b]));

            for (int i = 0; i < ids.Count; i++)
            {
                ulong id = ids[i];
                string name = "(desconocido)";
                for (int r = 0; r < _records.Count; r++)
                {
                    if (_records[r].Id != id) continue;
                    if (!string.IsNullOrEmpty(_records[r].Name)) name = _records[r].Name;
                    break;
                }
                log.Info($"{_cfg.FormatAlias(_numberById[id])} <- \"{name}\" ({id})");
            }
        }
    }
}
