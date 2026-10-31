using System;
using System.Collections.Generic;
using System.Text;
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GwyfNameHider
{
    /// <summary>
    /// Red de seguridad: recorre los textos visibles de la UI (TextMeshPro y UI.Text)
    /// y sustituye cualquier nombre de jugador conocido por su alias.
    ///
    /// Los parches Harmony cubren los sitios conocidos; esto cubre el resto
    /// (mensajes nuevos, menus, textos que el juego recreea, etc.).
    /// </summary>
    internal sealed class TextScrubber
    {
        private sealed class Tracked
        {
            public TMP_Text Tmp;
            public Text Ugui;
            public string Raw;
            public string Applied;
            public int Version = -1;
            public bool Skip;

            public bool Alive => Tmp != null || Ugui != null;

            public string Get()
            {
                if (Tmp != null) return Tmp.text;
                if (Ugui != null) return Ugui.text;
                return null;
            }

            public void Set(string value)
            {
                if (Tmp != null) Tmp.text = value;
                else if (Ugui != null) Ugui.text = value;
            }
        }

        private readonly PluginConfig _cfg;
        private readonly AliasRegistry _registry;
        private readonly PluginLog _log;

        private readonly List<Tracked> _tracked = new List<Tracked>();
        private readonly Dictionary<int, Tracked> _byId = new Dictionary<int, Tracked>();
        private int _nextRescanFrame;

        public TextScrubber(PluginConfig cfg, AliasRegistry registry, PluginLog log)
        {
            _cfg = cfg;
            _registry = registry;
            _log = log;
        }

        public void RequestRescan()
        {
            _nextRescanFrame = 0;
        }

        public void Tick()
        {
            if (Time.frameCount >= _nextRescanFrame)
            {
                Rescan();
                _nextRescanFrame = Time.frameCount + Math.Max(1, _cfg.RescanIntervalFrames.Value);
            }

            for (int i = 0; i < _tracked.Count; i++)
                ScrubOne(_tracked[i]);
        }

        // ------------------------------------------------------------------
        // Descubrimiento de textos
        // ------------------------------------------------------------------

        public void Rescan()
        {
            try
            {
                Cleanup();

                var tmpTexts = Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < tmpTexts.Length; i++)
                    Track(tmpTexts[i]);

                var uiTexts = Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                for (int i = 0; i < uiTexts.Length; i++)
                    Track(uiTexts[i]);
            }
            catch (Exception e)
            {
                _log.Debug("[Scrubber] Error al buscar textos: " + e.Message);
            }
        }

        private void Track(TMP_Text text)
        {
            if (text == null) return;
            int id = text.GetInstanceID();
            if (_byId.ContainsKey(id)) return;

            var tracked = new Tracked
            {
                Tmp = text,
                Skip = ShouldSkip(text)
            };
            _byId[id] = tracked;
            _tracked.Add(tracked);
        }

        private void Track(Text text)
        {
            if (text == null) return;
            int id = text.GetInstanceID();
            if (_byId.ContainsKey(id)) return;

            var tracked = new Tracked
            {
                Ugui = text,
                Skip = ShouldSkip(text)
            };
            _byId[id] = tracked;
            _tracked.Add(tracked);
        }

        private void Cleanup()
        {
            if (_tracked.Count == 0) return;

            _byId.Clear();
            for (int i = _tracked.Count - 1; i >= 0; i--)
            {
                var tracked = _tracked[i];
                if (!tracked.Alive)
                {
                    _tracked.RemoveAt(i);
                    continue;
                }

                Component comp = tracked.Tmp != null ? (Component)tracked.Tmp : tracked.Ugui;
                _byId[comp.GetInstanceID()] = tracked;
            }
        }

        private bool ShouldSkip(Component component)
        {
            try
            {
                if (component == null) return true;
                if (component.GetComponentInParent<TMP_InputField>() != null) return true;
                if (component.GetComponentInParent<InputField>() != null) return true;
                if (_cfg.SkipDebugUI.Value && IsDebugHierarchy(component.transform)) return true;
            }
            catch (Exception)
            {
                return true;
            }

            return false;
        }

        private static bool IsDebugHierarchy(Transform transform)
        {
            int depth = 0;
            while (transform != null && depth++ < 12)
            {
                var name = transform.name;
                if (!string.IsNullOrEmpty(name))
                {
                    var lower = name.ToLowerInvariant();
                    if (lower.Contains("console") || lower.Contains("debug") || lower.Contains("srdebugger"))
                        return true;
                }
                transform = transform.parent;
            }

            return false;
        }

        // ------------------------------------------------------------------
        // Sustitucion
        // ------------------------------------------------------------------

        private void ScrubOne(Tracked tracked)
        {
            if (!tracked.Alive || tracked.Skip) return;

            string current;
            try
            {
                current = tracked.Get();
            }
            catch (Exception)
            {
                return;
            }

            if (string.IsNullOrEmpty(current)) return;

            if (string.Equals(current, tracked.Applied, StringComparison.Ordinal))
            {
                // Nada nuevo del juego; solo hay que rehacer si cambiaron los alias.
                if (tracked.Version == _registry.Version) return;
                if (string.IsNullOrEmpty(tracked.Raw)) return;

                var updated = _registry.ReplaceText(tracked.Raw);
                tracked.Version = _registry.Version;
                if (!string.Equals(updated, current, StringComparison.Ordinal))
                    Apply(tracked, updated);
                return;
            }

            // El juego acaba de escribir un texto nuevo: se toma como original.
            tracked.Raw = current;
            var scrubbed = _registry.ReplaceText(current);
            tracked.Version = _registry.Version;

            if (!string.Equals(scrubbed, current, StringComparison.Ordinal))
                Apply(tracked, scrubbed);
            else
                tracked.Applied = current;
        }

        private static void Apply(Tracked tracked, string value)
        {
            try
            {
                tracked.Set(value);
                tracked.Applied = value;
            }
            catch (Exception)
            {
                // El objeto pudo destruirse entre la comprobacion y la escritura.
            }
        }

        /// <summary>Procesa un subarbol concreto de inmediato (por ejemplo, una entrada de menu recien creada).</summary>
        public void ScrubObject(GameObject root)
        {
            if (root == null) return;

            try
            {
                var tmpTexts = root.GetComponentsInChildren<TMP_Text>(true);
                for (int i = 0; i < tmpTexts.Length; i++)
                {
                    var text = tmpTexts[i];
                    if (text == null || ShouldSkip(text)) continue;
                    ScrubDirect(text.text, v => text.text = v);
                }

                var uiTexts = root.GetComponentsInChildren<Text>(true);
                for (int i = 0; i < uiTexts.Length; i++)
                {
                    var text = uiTexts[i];
                    if (text == null || ShouldSkip(text)) continue;
                    ScrubDirect(text.text, v => text.text = v);
                }
            }
            catch (Exception e)
            {
                _log.Debug("[Scrubber] Error al procesar objeto: " + e.Message);
            }
        }

        private void ScrubDirect(string current, Action<string> setter)
        {
            if (string.IsNullOrEmpty(current)) return;
            var scrubbed = _registry.ReplaceText(current);
            if (!string.Equals(scrubbed, current, StringComparison.Ordinal))
                setter(scrubbed);
        }

        // ------------------------------------------------------------------
        // Restauracion / diagnostico
        // ------------------------------------------------------------------

        public void RestoreAll()
        {
            for (int i = 0; i < _tracked.Count; i++)
            {
                var tracked = _tracked[i];
                if (!tracked.Alive) continue;
                if (string.IsNullOrEmpty(tracked.Raw) || string.IsNullOrEmpty(tracked.Applied)) continue;
                if (string.Equals(tracked.Raw, tracked.Applied, StringComparison.Ordinal)) continue;

                try
                {
                    tracked.Set(tracked.Raw);
                }
                catch (Exception)
                {
                }
            }

            _tracked.Clear();
            _byId.Clear();
            _nextRescanFrame = 0;
        }

        public void Dump()
        {
            _log.Info("=== GWYF Name Hider: textos visibles ===");

            int index = 0;
            for (int i = 0; i < _tracked.Count; i++)
            {
                var tracked = _tracked[i];
                if (!tracked.Alive) continue;

                string value;
                try
                {
                    value = tracked.Get();
                }
                catch (Exception)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(value)) continue;
                index++;

                Component comp = tracked.Tmp != null ? (Component)tracked.Tmp : tracked.Ugui;
                _log.Info($"[{index}] {PathOf(comp.transform)} :: \"{Shorten(value)}\"");

                if (!string.IsNullOrEmpty(tracked.Raw) && !string.Equals(tracked.Raw, value, StringComparison.Ordinal))
                    _log.Info($"      original: \"{Shorten(tracked.Raw)}\"");
            }

            _log.Info($"Total de textos visibles: {index}");
            _registry.DumpTo(_log);
        }

        private static string PathOf(Transform transform)
        {
            var parts = new List<string>();
            int depth = 0;

            while (transform != null && depth++ < 8)
            {
                parts.Add(transform.name);
                transform = transform.parent;
            }

            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        private static string Shorten(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            value = value.Replace("\n", "\\n").Replace("\r", "");

            return value.Length <= 160 ? value : value.Substring(0, 157) + "...";
        }
    }
}
