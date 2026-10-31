using BepInEx.Configuration;

namespace GwyfNameHider
{
    /// <summary>
    /// Configuracion del plugin. Se genera automaticamente en
    /// BepInEx/config/com.archi.gwyf.namehider.cfg
    /// </summary>
    internal sealed class PluginConfig
    {
        public readonly ConfigEntry<bool> Enabled;
        public readonly ConfigEntry<string> AliasFormat;
        public readonly ConfigEntry<int> MinNameLength;
        public readonly ConfigEntry<bool> SkipDebugUI;
        public readonly ConfigEntry<bool> VerboseLog;
        public readonly ConfigEntry<int> RescanIntervalFrames;
        public readonly ConfigEntry<string> ToggleKey;
        public readonly ConfigEntry<string> DumpKey;
        public readonly ConfigEntry<bool> MenuEnabled;
        public readonly ConfigEntry<string> MenuLabel;
        public readonly ConfigEntry<string> MenuTab;

        public PluginConfig(ConfigFile cfg)
        {
            Enabled = cfg.Bind(
                "General",
                "Enabled",
                true,
                "Activa o desactiva la sustitucion de nombres. Tambien se puede alternar con la tecla configurada abajo.");

            AliasFormat = cfg.Bind(
                "General",
                "AliasFormat",
                "Usuario {0}",
                "Formato del alias. Debe contener {0}, que se sustituye por el numero del jugador.");

            MinNameLength = cfg.Bind(
                "General",
                "MinNameLength",
                2,
                "Longitud minima del nombre real para intentar sustituirlo. Evita reemplazos peligrosos con nombres de 1 caracter.");

            SkipDebugUI = cfg.Bind(
                "General",
                "SkipDebugUI",
                true,
                "No modificar textos que esten dentro de paneles de consola/debug (por ejemplo la consola de desarrollador o SRDebugger).");

            VerboseLog = cfg.Bind(
                "General",
                "VerboseLog",
                false,
                "Escribe en el log cada jugador detectado y cada alias asignado. Util para diagnosticar.");

            RescanIntervalFrames = cfg.Bind(
                "Rendimiento",
                "RescanIntervalFrames",
                15,
                "Cada cuantos frames se buscan textos nuevos en la escena. Valores bajos detectan antes los textos nuevos, pero consumen mas CPU.");

            ToggleKey = cfg.Bind(
                "Atajos",
                "ToggleKey",
                "F10",
                "Tecla para activar/desactivar el plugin en caliente. Ejemplo: F10. Vacio = desactivado.");

            DumpKey = cfg.Bind(
                "Atajos",
                "DumpKey",
                "F11",
                "Tecla para volcar al log todos los textos visibles y los alias detectados. Ejemplo: F11. Vacio = desactivado.");

            MenuEnabled = cfg.Bind(
                "Menu",
                "Enabled",
                true,
                "Anade una fila en el menu de ajustes del juego para activar/desactivar la sustitucion de nombres.");

            MenuLabel = cfg.Bind(
                "Menu",
                "Label",
                "",
                "Texto de la fila del menu. Vacio = automatico (espanol si el juego esta en espanol; ingles en el resto).");

            MenuTab = cfg.Bind(
                "Menu",
                "Tab",
                "",
                "Pestana del menu de ajustes donde colocar la fila. Vacio = la pestana que se abre por defecto.");
        }

        public string FormatAlias(int number)
        {
            var format = AliasFormat.Value;
            if (string.IsNullOrWhiteSpace(format) || format.IndexOf("{0}", System.StringComparison.Ordinal) < 0)
                format = "Usuario {0}";
            return string.Format(format, number);
        }
    }
}
