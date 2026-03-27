# Jellyfin Plugin - SubX

Plugin de Jellyfin para buscar y descargar subtítulos en español desde Subdivx.

## Estado

Este repositorio incluye:

- plugin base para Jellyfin 10.11.x
- proveedor de subtítulos (`ISubtitleProvider`)
- página de configuración
- manifiesto de repositorio (`manifest.json`)
- workflow de GitHub Actions para compilar y adjuntar el ZIP a un release

No pude compilar el binario en este entorno porque no hay SDK de .NET instalado. El workflow de GitHub Actions quedó alineado con .NET 9, que es lo que requieren los paquetes Jellyfin 10.11.6 usados por este proyecto.

## Funcionalidad implementada

- búsqueda de subtítulos para películas y episodios
- modo directo contra Subdivx
- soporte de `Cookie` completa y `User-Agent`
- descarga del archivo de Subdivx y extracción de `.srt`, `.ass`, `.ssa` o `.sub`
- logs básicos opcionales para depuración de búsquedas directas

## Configuración

En Jellyfin > Plugins > SubX:

- `CookieHeader`: pega la cabecera `Cookie` completa del request que funciona en Subdivx
- `UserAgent`: user-agent a reutilizar con las cookies
- `OnlySpanish`: limita resultados a subtítulos en español
- `EnableDebugLogging`: logs más verbosos

## Publicación en GitHub

1. Subí este repo a GitHub.
2. Ajustá `manifest.json` y `manifest.template.json` si cambias usuario, repo o versión.
3. Creá un tag, por ejemplo `v0.1.0.22`.
4. GitHub Actions va a compilar y adjuntar `Jellyfin.Plugin.SubX-v0.1.0.22.zip` al release.
5. Configurá GitHub Pages para servir desde la rama `gh-pages` y carpeta `/ (root)`.
6. El workflow publica automáticamente un `manifest.json` final con checksum correcto en `gh-pages`.
7. En Jellyfin agregá esa URL en **Dashboard > Plugins > Repositories**.

## Notas importantes

- El acceso directo a Subdivx depende de cookies válidas y puede romperse por cambios de Cloudflare o del frontend del sitio.
- El tag `v0.1.0.13` quedó asociado al nombre anterior del plugin; para publicar `SubX` usá un tag nuevo.
- Cuando saques una nueva versión, actualizá `version`, `sourceUrl` y `timestamp` en `manifest.template.json` antes de crear el tag.
- El `checksum` ya no hace falta cargarlo a mano: lo calcula el workflow y publica el manifest final a `gh-pages`.

## Probe externo

Para probar la búsqueda fuera de Jellyfin:

```bash
python3 tools/subdivx_probe.py \
  --query "Made in Abyss" \
  --cookie-header "cf_clearance=...; sdx=..." \
  --user-agent "Mozilla/5.0 (X11; Linux x86_64; rv:148.0) Gecko/20100101 Firefox/148.0"
```
