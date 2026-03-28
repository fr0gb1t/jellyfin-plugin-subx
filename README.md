# Jellyfin Plugin - SubX 🎬

Plugin de Jellyfin para buscar y descargar subtítulos en español desde Subdivx.

## ✨ Estado

Este repositorio incluye:

- plugin base para Jellyfin 10.11.x
- proveedor de subtítulos (`ISubtitleProvider`)
- página de configuración
- manifiesto de repositorio (`manifest.json`)
- workflow de GitHub Actions para compilar y adjuntar el ZIP a un release

El workflow de GitHub Actions quedó alineado con .NET 9, que es lo que requieren los paquetes Jellyfin 10.11.6 usados por este proyecto.

## 🧩 Funcionalidad implementada

- búsqueda de subtítulos para películas y episodios
- modo directo contra Subdivx
- soporte de `Cookie` completa y `User-Agent`
- rate limit configurable entre intentos de búsqueda
- descarga del archivo de Subdivx y extracción de `.srt`, `.ass`, `.ssa` o `.sub`
- logs básicos opcionales para depuración de búsquedas directas

## ⚙️ Configuración

En Jellyfin > Plugins > SubX:

- `CookieHeader`: pega la cabecera `Cookie` completa del request que funciona en Subdivx
- `UserAgent`: user-agent a reutilizar con las cookies
- `SearchDelaySeconds`: espera entre intentos de búsqueda alternativos para evitar bloqueos de Cloudflare
- `OnlySpanish`: limita resultados a subtítulos en español
- `EnableDebugLogging`: logs más verbosos

La página de configuración del plugin también incluye una ayuda rápida para obtener `Cookie` y `User-Agent` desde las herramientas de desarrollador del navegador.

## 🚀 Publicación en GitHub

- Las releases se publican mediante GitHub Actions.
- Cada release genera el ZIP instalable del plugin.
- El `manifest.json` del repositorio se publica automáticamente con el checksum correcto para usarlo desde Jellyfin.

## 📝 Notas importantes

- El acceso directo a Subdivx depende de cookies válidas y puede romperse por cambios de Cloudflare o del frontend del sitio.
- El proceso de release y publicación del manifest está automatizado.

## 🧪 Probe externo

Para probar la búsqueda fuera de Jellyfin:

```bash
python3 tools/subdivx_probe.py \
  --query "Made in Abyss" \
  --cookie-header "cf_clearance=...; sdx=..." \
  --user-agent "Mozilla/5.0 (X11; Linux x86_64; rv:148.0) Gecko/20100101 Firefox/148.0"
```
