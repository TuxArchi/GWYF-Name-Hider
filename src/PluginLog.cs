using System;
using System.IO;
using BepInEx.Logging;

namespace GwyfNameHider
{
    /// <summary>
    /// Logger del plugin. Escribe a la vez en el log de BepInEx y en un archivo
    /// propio con auto-flush (BepInEx/GwyfNameHider.log).
    ///
    /// El archivo propio existe porque, bajo Proton/Wine, el listener de disco de
    /// BepInEx deja de volcar lineas nuevas despues de arrancar Unity. Sin esto,
    /// el volcado de F11 y los avisos de alias se perderian.
    /// </summary>
    internal sealed class PluginLog
    {
        private static readonly object Gate = new object();

        private readonly ManualLogSource _sink;
        private readonly string _path;
        private readonly bool _verbose;

        private bool _fileBroken;

        public PluginLog(ManualLogSource sink, string path, bool verbose)
        {
            _sink = sink;
            _path = path;
            _verbose = verbose;
        }

        public string FilePath => _path;

        public void Info(string message) => Write(LogLevel.Info, message);
        public void Warning(string message) => Write(LogLevel.Warning, message);
        public void Error(string message) => Write(LogLevel.Error, message);

        public void Debug(string message)
        {
            if (_verbose) Write(LogLevel.Debug, message);
        }

        /// <summary>Borra el archivo propio al iniciar, para no mezclar sesiones.</summary>
        public void ResetFile()
        {
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
            }
            catch (Exception)
            {
                // Si no se puede borrar, se seguira agregando al final.
            }
        }

        private void Write(LogLevel level, string message)
        {
            if (message == null) return;

            try
            {
                switch (level)
                {
                    case LogLevel.Warning: _sink?.LogWarning(message); break;
                    case LogLevel.Error: _sink?.LogError(message); break;
                    case LogLevel.Debug: _sink?.LogDebug(message); break;
                    default: _sink?.LogInfo(message); break;
                }
            }
            catch (Exception)
            {
                // Nunca dejamos que un fallo del log rompa el plugin.
            }

            if (_fileBroken) return;

            try
            {
                var line = DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine;
                lock (Gate)
                {
                    File.AppendAllText(_path, line);
                }
            }
            catch (Exception)
            {
                _fileBroken = true;
            }
        }
    }
}
