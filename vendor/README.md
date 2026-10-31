# vendor/

`scripts/package.sh` espera aquí el zip oficial de BepInEx 6:

```
vendor/BepInEx_win_x64_6.0.0-be.791.zip
```

Descárgalo de <https://builds.bepinex.dev/projects/bepinex_be> (build **791**,
variante **Unity Mono x64**) y guárdalo con ese nombre. Si el archivo se llama de
otra forma, indícalo con `BEPINEX_ZIP=/ruta/al/zip`.

SHA256 del zip usado para el release 1.1.0:

```
5dce6c73f760933957f477839a6e28e5d359f196a5cef4a5c90f5a7a73fce82c
```

El zip **no** se sube al repositorio (`vendor/*.zip` está en `.gitignore`); el
release ya incluye BepInEx y su licencia.
