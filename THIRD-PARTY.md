# Third-party components

El zip de distribucion incluye BepInEx 6 y sus dependencias. El codigo de
GwyfNameHider no contiene codigo del juego ni de BepInEx.

| Componente | Version | Licencia | Uso |
| --- | --- | --- | --- |
| [BepInEx](https://github.com/BepInEx/BepInEx) | 6.0.0-be.791 | LGPL-2.1 | Cargador de plugins |
| [Harmony (0Harmony)](https://github.com/BepInEx/HarmonyX) | incluida en BepInEx | MIT | Parches en runtime |
| [Mono.Cecil](https://github.com/jbevain/cecil) | incluida en BepInEx | MIT | Dependencia de BepInEx |
| [MonoMod](https://github.com/MonoMod/MonoMod) | incluida en BepInEx | MIT | Dependencia de BepInEx |
| [Unity Doorstop](https://github.com/NeighTools/UnityDoorstop) | 4.x | LGPL-2.1 | Carga de BepInEx (`winhttp.dll`) |

La licencia completa de BepInEx esta en
[`third_party/LICENSE-BepInEx.txt`](third_party/LICENSE-BepInEx.txt).

No se redistribuye ningun archivo del juego: el plugin referencia los ensamblados
instalados por el propio jugador y solo genera un DLL propio.
