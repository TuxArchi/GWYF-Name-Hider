# Cómo funciona

Documento técnico para quien quiera mantener o auditar el plugin.

## Resumen

El plugin tiene tres capas:

1. **AliasRegistry**: decide qué número le corresponde a cada SteamID.
2. **Parches Harmony**: sustituyen el texto en los puntos donde el juego escribe
   nombres.
3. **TextScrubber**: red de seguridad que revisa los textos de UI ya creados y
   sustituye cualquier nombre conocido que se haya escapado.

Todo ocurre en el cliente. No se modifican SyncVar, Mirror, Steam ni el websocket.

## Numeración (`AliasRegistry`)

- El anfitrión se obtiene con `SteamMatchmaking.GetLobbyOwner` y siempre recibe el 1.
- El resto se numera por orden de aparición en `LobbySettings.players` y en los
  `PlayerProfile` de la escena.
- Los números no se reutilizan mientras dure el lobby. Al cambiar de lobby
  (`steamLobbyID`) se reinicia la numeración.
- Si el host migra, el nuevo anfitrión pasa a 1 y el anterior recibe un número
  nuevo.
- Los nombres reales se obtienen de `SteamFriends.GetPersonaName` /
  `GetFriendPersonaName` **solo para construir la tabla de sustituciones**. No se
  parchea ninguna API de Steam.
- El nombre local también entra en la tabla, por eso tu propio nombre también se
  sustituye.

## Parches Harmony

| Método | Para qué |
| --- | --- |
| `PlayerProfile.SetPlayerNameTag` | Nombre sobre la cabeza |
| `PlayerProfile.OnSync` | Reaplica la etiqueta cuando el perfil se sincroniza |
| `PlayerProfile.OnStartClient` | Fuerza un rescaneo al aparecer jugadores |
| `MoneyDisplayAndFeedbacks.GetPlayerDisplayName` | Nombre en el historial de dinero |
| `MoneyDisplayAndFeedbacks.SpawnMoneyChangeText` | Textos flotantes de dinero |
| `GameOverUI.GetCurrentPlayerNamesById` | Pantalla de fin de partida |
| `GameLostContributionEntry.Setup` | Entradas de contribución |
| `ProfitLineGraph3D.UpdatePlayerNameText` | Gráfica de beneficios |
| `CreditsRollManager.BuildSnapshotsFromScenePlayers` | Créditos |
| `EscapeMenuVoiceList.ResolveDisplayNameAndSteamId` | Menú Escape / voz |
| `VoiceVolumeEntry.UpdateIdentity` | Lista de volumen por jugador |
| `LobbyMemberList.UpdateMemberEntry` | Lista de miembros del lobby |
| `LobbyMemberList.AddMemberEntry` | Alta de un miembro |
| `SettingsLayoutRuntimeUI.ShowTab` | Inyecta la fila del mod en Ajustes |
| `SettingsLocalization.ResolveLabelKey` | Evita que la localización pise la etiqueta |
| `SettingsPersistence.ApplySavedValue` | No aplicar el valor guardado por el juego a la fila del mod |

Cada parche se aplica por separado. Si el juego cambia y uno deja de existir, se
registra un aviso y el resto sigue funcionando.

## TextScrubber

- Busca `TMP_Text` y `UI.Text` en la escena (incluidos objetos inactivos) cada
  `RescanIntervalFrames`.
- Sustituye nombres completos respetando límites de palabra (no toca subcadenas
  dentro de otras palabras).
- Ignora campos de texto (`TMP_InputField` / `InputField`) y, por defecto, paneles de
  consola/debug.
- Guarda el texto original de cada control para poder restaurarlo al desactivar el
  mod (`F10` o el ajuste del menú).

## Fila en Ajustes

El juego modela los ajustes como `SettingsLayout` → `Tab.entries`
(`SettingItemBase`). Los booleanos son `ToggleSettingItem`, que la UI dibuja como un
desplegable Sí/No localizado.

El plugin:

1. Añade un `ToggleSettingItem` en memoria a la pestaña por defecto antes de que la
   UI construya las filas (prefix de `ShowTab`).
2. Escucha `SettingItemBase.SettingsChanged` y filtra por su clave
   (`gwyf_name_hider`).
3. Sincroniza el valor al abrir el panel y cuando se pulsa `F10`.
4. Impide que `SettingsPersistence` aplique el valor guardado en `settings.json`
   sobre esa fila.

La fila se destruye con la escena; no se escribe nada en los assets del juego.

## Por qué no se parchea `SteamFriends.GetPersonaName`

Sería tentador, pero el websocket del juego construye la URL con
`ownerNickname` a partir del nombre de Steam. Cambiarlo globalmente rompería la
conexión o cambiaría datos que se comparten con el lobby. Por eso las sustituciones
son locales y en la capa de UI.

## Log

BepInEx escribe `LogOutput.log`, pero bajo Proton/Wine su listener de disco puede
dejar de volcar líneas nuevas tras arrancar Unity. El plugin mantiene su propio
archivo con `File.AppendAllText` y auto-flush:

```
BepInEx/GwyfNameHider.log
```

Con `VerboseLog = true` se registran los alias asignados, los textos volcados con
`F11` y los errores de los parches.
