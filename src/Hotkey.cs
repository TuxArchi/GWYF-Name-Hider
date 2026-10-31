using System;
using System.Reflection;
using UnityEngine;

namespace GwyfNameHider
{
    /// <summary>
    /// Lector de teclas compatible con los dos sistemas de entrada de Unity.
    /// Primero intenta UnityEngine.Input (legacy); si el juego solo usa el
    /// Input System nuevo, se recurre a UnityEngine.InputSystem por reflexion
    /// para no crear una dependencia obligatoria.
    /// </summary>
    internal static class Hotkey
    {
        private static bool _legacyUnavailable;
        private static bool _newInputUnavailable;

        private static Type _keyboardType;
        private static Type _keyEnumType;
        private static PropertyInfo _currentProperty;
        private static PropertyInfo _itemProperty;
        private static PropertyInfo _wasPressedProperty;
        private static bool _newInputReady;

        public static bool IsDown(KeyCode key)
        {
            if (key == KeyCode.None) return false;

            if (!_legacyUnavailable)
            {
                try
                {
                    return Input.GetKeyDown(key);
                }
                catch (Exception)
                {
                    _legacyUnavailable = true;
                }
            }

            return IsDownNewInput(key);
        }

        private static bool IsDownNewInput(KeyCode key)
        {
            if (_newInputUnavailable) return false;

            try
            {
                if (!_newInputReady) InitializeNewInput();
                if (!_newInputReady) return false;

                var keyboard = _currentProperty.GetValue(null, null);
                if (keyboard == null) return false;

                var keyValue = Enum.Parse(_keyEnumType, key.ToString(), true);
                var control = _itemProperty.GetValue(keyboard, new[] { keyValue });
                if (control == null) return false;

                return (bool)_wasPressedProperty.GetValue(control, null);
            }
            catch (Exception)
            {
                _newInputUnavailable = true;
                return false;
            }
        }

        private static void InitializeNewInput()
        {
            _newInputReady = false;

            _keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            _keyEnumType = Type.GetType("UnityEngine.InputSystem.Key, Unity.InputSystem");
            if (_keyboardType == null || _keyEnumType == null) return;

            _currentProperty = _keyboardType.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
            _itemProperty = _keyboardType.GetProperty("Item", new[] { _keyEnumType });
            if (_currentProperty == null || _itemProperty == null) return;

            _wasPressedProperty = _itemProperty.PropertyType.GetProperty("wasPressedThisFrame");
            if (_wasPressedProperty == null) return;

            _newInputReady = true;
        }
    }
}
