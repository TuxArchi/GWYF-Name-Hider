# Publicar en GitHub

Esta carpeta **es** la raíz del repositorio. Ya tiene un commit inicial y el release
preparado; solo falta crear el repositorio remoto y subirlo.

## 1. Crear el repositorio

Con GitHub CLI autenticado (`gh auth login`):

```bash
cd github
gh repo create gwyf-name-hider --public --source=. --remote=origin --push
```

O crea el repositorio vacío en la web y luego:

```bash
cd github
git remote add origin git@github.com:<usuario>/gwyf-name-hider.git
git push -u origin main
```

## 2. Metadatos sugeridos

**Descripción:**

> Plugin de BepInEx 6 para Gamble With Your Friends: muestra los nombres como Usuario 1, 2… en vez del nombre real de Steam. Solo cliente.

**Topics:**

`bepinex`, `bepinex6`, `unity`, `mod`, `harmony`, `gamble-with-your-friends`, `privacy`

## 3. Publicar el release

El zip ya está en `release/` con su `SHA256SUMS.txt`:

```
release/GambleWithYourFriends-NameHider-BepInEx6.zip
release/SHA256SUMS.txt
```

Crea el tag y el release:

```bash
git tag -a v1.1.0 -m "GWYF Name Hider 1.1.0"
git push origin v1.1.0

gh release create v1.1.0 \
  release/GambleWithYourFriends-NameHider-BepInEx6.zip \
  release/SHA256SUMS.txt \
  --title "GWYF Name Hider 1.1.0" \
  --notes-file CHANGELOG.md
```

El workflow `.github/workflows/release.yml` también crea el release al empujar un
tag `v*`, adjuntando los archivos de `release/`.

## 4. Actualizar el release en el futuro

1. Cambia `PluginVersion` en `src/Plugin.cs` y `<Version>` en
   `src/GwyfNameHider.csproj`.
2. Actualiza `CHANGELOG.md`.
3. Regenera el zip y las sumas:

   ```bash
   BEPINEX_ZIP="/ruta/BepInEx_win_x64_6.0.0-be.791.zip" ./scripts/release.sh
   ```

4. Commit, tag y push:

   ```bash
   git add -A
   git commit -m "GWYF Name Hider 1.1.1"
   git tag -a v1.1.1 -m "GWYF Name Hider 1.1.1"
   git push && git push --tags
   ```

## Contenido que no se sube

- `src/bin/`, `src/obj/`, `dist/`: salidas de compilación (ignoradas).
- `vendor/*.zip`: el zip de BepInEx se descarga; el release ya lo incluye.
- Nada de archivos del juego ni de BepInEx extraídos.
