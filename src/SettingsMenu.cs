using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GwyfNameHider
{
    /// <summary>
    /// Anade una fila nativa al menu de ajustes del juego para activar/desactivar
    /// la sustitucion de nombres sin usar el atajo de teclado.
    ///
    /// El juego representa los ajustes booleanos como un <see cref="ToggleSettingItem"/>,
    /// que la UI dibuja como un desplegable Si/No usando sus propios prefabs,
    /// localizacion y navegacion de mando.
    ///
    /// La fila se inyecta en la pestana que se abre por defecto. No se toca ningun
    /// archivo del juego: la entrada vive solo en memoria.
    /// </summary>
    internal static class SettingsMenu
    {
        internal const string ItemKey = "gwyf_name_hider";

        private const string LabelEs = "Ocultar nombres de jugadores";
        private const string LabelEn = "Hide player names";

        private static NameHiderPlugin _plugin;
        private static PluginConfig _cfg;
        private static PluginLog _log;

        private static FieldInfo _layoutField;
        private static FieldInfo _defaultTabField;
        private static FieldInfo _currentTabField;

        private static string _label;
        private static bool _eventHooked;
        private static bool _missingLayoutLogged;
        private static bool _localeLogged;
        private static float _nextLocaleCheck;
        private static Type _localizationSettingsType;
        private static PropertyInfo _selectedLocaleProperty;

        public static void Init(NameHiderPlugin plugin, PluginConfig cfg, PluginLog log)
        {
            _plugin = plugin;
            _cfg = cfg;
            _log = log;
            _label = ResolveLabel();
        }

        public static void Apply(Harmony harmony)
        {
            Patch(harmony, typeof(SettingsLayoutRuntimeUI), "ShowTab", new[] { typeof(string) },
                nameof(Pre_ShowTab), null);

            Patch(harmony, typeof(SettingsLocalization), "ResolveLabelKey", new[] { typeof(SettingItemBase) },
                null, nameof(Post_ResolveLabelKey));

            // El juego tambien guarda nuestra fila en su settings.json. Evitamos que
            // ese valor guardado pise el estado real del plugin al recargar ajustes.
            Patch(harmony, typeof(SettingsSystem.SettingsPersistence), "ApplySavedValue",
                new[] { typeof(SettingItemBase), typeof(SettingsSystem.SettingsPersistence.SettingSaveEntry) },
                nameof(Pre_ApplySavedValue), null);
        }

        private static bool Pre_ApplySavedValue(SettingItemBase item)
        {
            // false = no ejecutar el metodo original para nuestra entrada.
            return item == null || !string.Equals(item.key, ItemKey, StringComparison.Ordinal);
        }

        private static void Patch(Harmony harmony, Type type, string method, Type[] args,
            string prefix, string postfix)
        {
            try
            {
                var original = AccessTools.Method(type, method, args);
                if (original == null)
                {
                    _log.Warning($"[Menu] No se encontro {type.Name}.{method}; se omite la fila del menu.");
                    return;
                }

                var prefixMethod = prefix == null ? null : new HarmonyMethod(typeof(SettingsMenu), prefix);
                var postfixMethod = postfix == null ? null : new HarmonyMethod(typeof(SettingsMenu), postfix);

                harmony.Patch(original, prefixMethod, postfixMethod);
                _log.Info($"[Menu] OK: {type.Name}.{method}");
            }
            catch (Exception e)
            {
                _log.Error($"[Menu] Fallo al parchear {type.Name}.{method}: {e}");
            }
        }

        // ------------------------------------------------------------------
        // Eventos del juego
        // ------------------------------------------------------------------

        public static void HookEvents()
        {
            if (_eventHooked) return;
            _eventHooked = true;
            SettingItemBase.SettingsChanged += OnSettingChanged;
        }

        public static void UnhookEvents()
        {
            if (!_eventHooked) return;
            _eventHooked = false;
            SettingItemBase.SettingsChanged -= OnSettingChanged;
        }

        private static void OnSettingChanged(SettingItemBase entry)
        {
            if (entry == null || !string.Equals(entry.key, ItemKey, StringComparison.Ordinal))
                return;

            var toggle = entry as ToggleSettingItem;
            if (toggle == null) return;

            _plugin?.SetEnabledFromMenu(toggle.value);
        }

        // ------------------------------------------------------------------
        // Inyeccion de la fila
        // ------------------------------------------------------------------

        private static void Pre_ShowTab(SettingsLayoutRuntimeUI __instance)
        {
            try
            {
                Inject(__instance);
            }
            catch (Exception e)
            {
                _log.Debug("[Menu] Error al inyectar la fila: " + e.Message);
            }
        }

        private static void Inject(SettingsLayoutRuntimeUI ui)
        {
            if (ui == null || _plugin == null || !_cfg.MenuEnabled.Value) return;

            var layout = GetLayout(ui);
            if (layout == null || layout.tabs == null)
            {
                if (!_missingLayoutLogged)
                {
                    _missingLayoutLogged = true;
                    _log.Info("[Menu] El panel de ajustes todavia no tiene SettingsLayout; se reintentara al abrirlo.");
                }
                return;
            }

            var tab = FindTargetTab(ui, layout);
            if (tab == null)
            {
                _log.Warning("[Menu] No se encontro una pestana donde insertar la fila.");
                return;
            }

            if (tab.entries == null)
                tab.entries = new System.Collections.Generic.List<SettingItemBase>();

            var item = FindItem(tab);
            if (item == null)
            {
                item = CreateItem();
                InsertBeforeReset(tab, item);
                _log.Info($"[Menu] Fila \"{item.label}\" anadida en la pestana \"{tab.tabName}\".");
            }

            UpdateLabel(item);
            item.value = _plugin.IsEnabled;

            if (!_localeLogged)
            {
                _localeLogged = true;
                _log.Info($"[Menu] Idioma: locale={GetLocaleCode() ?? "null"}, " +
                          $"sistema={Application.systemLanguage}, etiqueta=\"{_label}\"");
            }
        }

        private static ToggleSettingItem CreateItem()
        {
            var item = ScriptableObject.CreateInstance<ToggleSettingItem>();
            item.name = "GwyfNameHider";
            item.key = ItemKey;
            item.label = _label;
            item.localizationKey = string.Empty;
            item.defaultValue = true;
            item.value = _plugin.IsEnabled;
            item.loadOnSceneStart = false;
            item.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return item;
        }

        private static void InsertBeforeReset(SettingsLayout.Tab tab, ToggleSettingItem item)
        {
            int index = tab.entries.Count;

            // Colocarla justo antes del boton de "restablecer", si lo hay al final.
            if (index > 0 && tab.entries[index - 1] is ResetSettingItem)
                index--;

            tab.entries.Insert(index, item);
        }

        private static ToggleSettingItem FindItem(SettingsLayout.Tab tab)
        {
            for (int i = 0; i < tab.entries.Count; i++)
            {
                var entry = tab.entries[i];
                if (entry != null && string.Equals(entry.key, ItemKey, StringComparison.Ordinal))
                    return entry as ToggleSettingItem;
            }

            return null;
        }

        private static SettingsLayout GetLayout(SettingsLayoutRuntimeUI ui)
        {
            if (_layoutField == null)
                _layoutField = AccessTools.Field(typeof(SettingsLayoutRuntimeUI), "layout");

            return _layoutField?.GetValue(ui) as SettingsLayout;
        }

        private static SettingsLayout.Tab FindTargetTab(SettingsLayoutRuntimeUI ui, SettingsLayout layout)
        {
            var wanted = _cfg.MenuTab.Value;
            if (!string.IsNullOrWhiteSpace(wanted))
            {
                var explicitTab = FindTab(layout, wanted.Trim());
                if (explicitTab != null) return explicitTab;

                _log.Warning($"[Menu] La pestana \"{wanted}\" no existe; se usara la predeterminada.");
            }

            if (_defaultTabField == null)
                _defaultTabField = AccessTools.Field(typeof(SettingsLayoutRuntimeUI), "defaultTabName");

            var defaultName = _defaultTabField?.GetValue(ui) as string;
            var defaultTab = FindTab(layout, defaultName);
            if (defaultTab != null) return defaultTab;

            for (int i = 0; i < layout.tabs.Count; i++)
                if (layout.tabs[i] != null && layout.tabs[i].entries != null)
                    return layout.tabs[i];

            return null;
        }

        private static SettingsLayout.Tab FindTab(SettingsLayout layout, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;

            for (int i = 0; i < layout.tabs.Count; i++)
            {
                var tab = layout.tabs[i];
                if (tab != null && string.Equals(tab.tabName, name, StringComparison.OrdinalIgnoreCase))
                    return tab;
            }

            return null;
        }

        // ------------------------------------------------------------------
        // Sincronizacion con F10
        // ------------------------------------------------------------------

        /// <summary>
        /// Vigila el idioma del juego: si la localizacion termina de inicializarse
        /// despues de crear la fila, actualiza la etiqueta y reconstruye el panel.
        /// </summary>
        public static void Tick()
        {
            if (_cfg == null || _plugin == null || !_cfg.MenuEnabled.Value) return;
            if (!string.IsNullOrWhiteSpace(_cfg.MenuLabel.Value)) return;

            if (Time.unscaledTime < _nextLocaleCheck) return;
            _nextLocaleCheck = Time.unscaledTime + 1f;

            var label = ResolveLabel();
            if (string.Equals(label, _label, StringComparison.Ordinal)) return;

            _label = label;
            _log?.Info($"[Menu] Etiqueta: \"{_label}\" " +
                       $"(locale={GetLocaleCode() ?? "null"}, sistema={Application.systemLanguage})");

            ApplyLabelToExistingItems();
            RefreshOpenPanel();
        }

        private static void ApplyLabelToExistingItems()
        {
            var layouts = Resources.FindObjectsOfTypeAll<SettingsLayout>();

            for (int l = 0; l < layouts.Length; l++)
            {
                var tabs = layouts[l]?.tabs;
                if (tabs == null) continue;

                for (int t = 0; t < tabs.Count; t++)
                {
                    var entries = tabs[t]?.entries;
                    if (entries == null) continue;

                    for (int e = 0; e < entries.Count; e++)
                    {
                        var entry = entries[e];
                        if (entry != null && string.Equals(entry.key, ItemKey, StringComparison.Ordinal) &&
                            !string.Equals(entry.label, _label, StringComparison.Ordinal))
                        {
                            entry.label = _label;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Si el panel de ajustes esta abierto, lo reconstruye para que el
        /// desplegable muestre el estado actual tras pulsar el atajo.
        /// </summary>
        public static void RefreshOpenPanel()
        {
            if (_cfg == null || !_cfg.MenuEnabled.Value) return;

            try
            {
                var panels = Object.FindObjectsByType<SettingsLayoutRuntimeUI>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);

                for (int i = 0; i < panels.Length; i++)
                {
                    var ui = panels[i];
                    if (ui == null || !ui.isActiveAndEnabled) continue;

                    if (_currentTabField == null)
                        _currentTabField = AccessTools.Field(typeof(SettingsLayoutRuntimeUI), "_currentTabName");

                    var current = _currentTabField?.GetValue(ui) as string;
                    if (!string.IsNullOrEmpty(current))
                        ui.ShowTab(current);
                }
            }
            catch (Exception e)
            {
                _log?.Debug("[Menu] No se pudo refrescar el panel: " + e.Message);
            }
        }

        // ------------------------------------------------------------------
        // Texto y localizacion
        // ------------------------------------------------------------------

        private static void Post_ResolveLabelKey(SettingItemBase entry, ref string __result)
        {
            if (entry == null || !string.Equals(entry.key, ItemKey, StringComparison.Ordinal))
                return;

            // Devolver vacio hace que el juego use directamente SettingItemBase.label,
            // sin buscar una clave de localizacion que no existe.
            UpdateLabel(entry);
            __result = string.Empty;
        }

        private static void UpdateLabel(SettingItemBase entry)
        {
            var label = ResolveLabel();

            if (!string.Equals(_label, label, StringComparison.Ordinal))
            {
                _label = label;
                _log?.Info($"[Menu] Etiqueta: \"{_label}\" " +
                           $"(locale={GetLocaleCode() ?? "null"}, sistema={Application.systemLanguage})");
            }

            if (entry != null && !string.Equals(entry.label, _label, StringComparison.Ordinal))
                entry.label = _label;
        }

        private static string ResolveLabel()
        {
            var custom = _cfg?.MenuLabel?.Value;
            if (!string.IsNullOrWhiteSpace(custom))
                return custom.Trim();

            var code = GetLocaleCode();
            bool spanish = !string.IsNullOrEmpty(code)
                ? code.StartsWith("es", StringComparison.OrdinalIgnoreCase)
                : Application.systemLanguage == SystemLanguage.Spanish;

            return spanish ? LabelEs : LabelEn;
        }

        /// <summary>
        /// Codigo del idioma seleccionado en el juego (p. ej. "es"), o null si
        /// Unity Localization todavia no esta inicializado.
        /// </summary>
        private static string GetLocaleCode()
        {
            try
            {
                if (_localizationSettingsType == null)
                {
                    _localizationSettingsType = Type.GetType(
                        "UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization");

                    if (_localizationSettingsType == null)
                    {
                        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                        for (int i = 0; i < assemblies.Length; i++)
                        {
                            var type = assemblies[i].GetType(
                                "UnityEngine.Localization.Settings.LocalizationSettings", false);
                            if (type != null)
                            {
                                _localizationSettingsType = type;
                                break;
                            }
                        }
                    }

                    if (_localizationSettingsType != null)
                    {
                        _selectedLocaleProperty = _localizationSettingsType.GetProperty(
                            "SelectedLocale", BindingFlags.Public | BindingFlags.Static);
                    }
                }

                var locale = _selectedLocaleProperty?.GetValue(null, null);
                if (locale == null) return null;

                var identifier = locale.GetType().GetProperty("Identifier")?
                    .GetValue(locale, null);

                return identifier?.GetType().GetProperty("Code")?
                    .GetValue(identifier, null) as string;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
