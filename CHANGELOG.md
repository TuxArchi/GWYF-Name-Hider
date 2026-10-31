# Changelog

Todas las versiones relevantes de GWYF Name Hider.

## 1.1.0 — 2026-10-31

- **Fila nativa en Ajustes** para activar/desactivar el mod, en la pestaña que se
  abre por defecto (General) y antes del botón de restablecer.
- Etiqueta **bilingüe**: `Ocultar nombres de jugadores` / `Hide player names`, según
  el idioma real del juego.
- El estado del ajuste **se guarda** en la configuración del plugin.
- `F10` sincroniza la fila si el menú está abierto.
- Protección para que el guardado de ajustes del juego no pise el estado del mod.
- Log propio con auto-flush (`BepInEx/GwyfNameHider.log`), necesario bajo Proton.

## 1.0.0 — 2026-10-28

- Versión inicial.
- Sustitución de nombres sobre la cabeza y en menús (lobby, dinero, fin de partida,
  gráfica, créditos, menú Escape y lista de miembros).
- Numeración estable con el anfitrión como Usuario 1.
- Atajos `F10` (activar/desactivar) y `F11` (volcar textos y alias).
- Configuración en `BepInEx/config/com.archi.gwyf.namehider.cfg`.
