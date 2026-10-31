using System;
using System.IO;
using BepInEx;
using BepInEx.Unity.Mono;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GwyfNameHider
{
    [BepInPlugin(Guid, PluginName, PluginVersion)]
    public sealed class NameHiderPlugin : BaseUnityPlugin
    {
        public const string Guid = "com.archi.gwyf.namehider";
        public const string PluginName = "GWYF Name Hider";
        public const string PluginVersion = "1.1.0";

        private PluginConfig _cfg;
        private PluginLog _log;
        private AliasRegistry _registry;
        private TextScrubber _scrubber;
        private Harmony _harmony;

        private bool _enabled = true;
        private KeyCode _toggleKey = KeyCode.F10;
        private KeyCode _dumpKey = KeyCode.F11;
        private float _nextRegistryRefresh;

        private void Awake()
        {
            _cfg = new PluginConfig(Config);

            var logPath = Path.Combine(Paths.BepInExRootPath, "GwyfNameHider.log");
            _log = new PluginLog(Logger, logPath, _cfg.VerboseLog.Value);
            _log.ResetFile();

            _registry = new AliasRegistry(_cfg, _log);
            _scrubber = new TextScrubber(_cfg, _registry, _log);

            Patches.Init(_registry, _scrubber, _cfg, _log);
            SettingsMenu.Init(this, _cfg, _log);

            _harmony = new Harmony(Guid);
            Patches.Apply(_harmony);
            SettingsMenu.Apply(_harmony);
            SettingsMenu.HookEvents();

            _enabled = _cfg.Enabled.Value;
            _toggleKey = ParseKey(_cfg.ToggleKey.Value);
            _dumpKey = ParseKey(_cfg.DumpKey.Value);

            SceneManager.sceneLoaded += OnSceneLoaded;

            _registry.Refresh();
            _scrubber.RequestRescan();

            _log.Info($"{PluginName} {PluginVersion} cargado.");
            _log.Info($"Estado inicial: {(_enabled ? "activado" : "desactivado")} | " +
                      $"Alternar: {_cfg.ToggleKey.Value} | Volcar textos: {_cfg.DumpKey.Value}");
            _log.Info($"Log detallado en: {logPath}");
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            try
            {
                SettingsMenu.UnhookEvents();
            }
            catch (Exception)
            {
            }

            try
            {
                _scrubber?.RestoreAll();
            }
            catch (Exception)
            {
            }

            try
            {
                _harmony?.UnpatchSelf();
            }
            catch (Exception)
            {
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            try
            {
                _registry?.Refresh();
                _scrubber?.RequestRescan();
            }
            catch (Exception e)
            {
                _log.Debug("Error al cambiar de escena: " + e.Message);
            }
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextRegistryRefresh)
            {
                _nextRegistryRefresh = Time.unscaledTime + 0.5f;
                _registry.Refresh();
            }

            SettingsMenu.Tick();
            HandleHotkeys();

            if (!_enabled) return;

            _scrubber.Tick();
        }

        private void HandleHotkeys()
        {
            if (Hotkey.IsDown(_toggleKey))
                SetEnabled(!_enabled, save: true, refreshMenu: true);

            if (Hotkey.IsDown(_dumpKey))
            {
                _log.Info("Volcado de textos solicitado.");
                _scrubber.Dump();
            }
        }

        public bool IsEnabled => _enabled;

        /// <summary>Llamado desde la fila del menu de ajustes.</summary>
        internal void SetEnabledFromMenu(bool value)
        {
            SetEnabled(value, save: true, refreshMenu: false);
        }

        /// <summary>
        /// Punto unico para activar/desactivar: atajo F10, menu de ajustes y estado guardado.
        /// </summary>
        internal void SetEnabled(bool value, bool save, bool refreshMenu)
        {
            bool changed = _enabled != value;
            _enabled = value;

            if (changed)
            {
                if (value)
                {
                    _scrubber.RequestRescan();
                    _log.Info("Sustitucion de nombres ACTIVADA.");
                }
                else
                {
                    _scrubber.RestoreAll();
                    _log.Info("Sustitucion de nombres DESACTIVADA (nombres originales restaurados).");
                }
            }

            if (save && _cfg.Enabled.Value != value)
            {
                _cfg.Enabled.Value = value;
                Config.Save();
            }

            if (refreshMenu)
                SettingsMenu.RefreshOpenPanel();
        }

        private static KeyCode ParseKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return KeyCode.None;

            KeyCode key;
            return Enum.TryParse(value.Trim(), true, out key) ? key : KeyCode.None;
        }
    }
}
