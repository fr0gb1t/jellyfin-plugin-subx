# Jellyfin Plugin - Subdivx

Plugin de Jellyfin para buscar y descargar subtítulos en español desde Subdivx.

## Estado

Este repositorio incluye:

- plugin base para Jellyfin 10.11.x
- proveedor de subtítulos (`ISubtitleProvider`)
- página de configuración
- manifiesto de repositorio (`manifest.json`)
- workflow de GitHub Actions para compilar y adjuntar el ZIP a un release

No pude compilar el binario en este entorno porque no hay SDK de .NET instalado. El workflow de GitHub Actions quedó alineado con .NET 9, que es lo que requieren los paquetes Jellyfin 10.11.3 usados por este proyecto.

## Funcionalidad implementada

- búsqueda de subtítulos para películas y episodios
- modo directo contra Subdivx
- modo opcional vía bridge HTTP externo
- soporte de cookies manuales (`cf_clearance` y `sdx`)
- descarga del archivo de Subdivx y extracción de `.srt`, `.ass`, `.ssa` o `.sub`
- logs básicos opcionales para depuración de búsquedas directas

## Configuración

En Jellyfin > Plugins > Subdivx:

- `UseBridge`: usa un bridge HTTP externo en vez de hablar directo con Subdivx
- `BridgeBaseUrl`: URL base del bridge
- `BridgeApiKey`: API key opcional para el bridge
- `CookieHeader`: alternativa para pegar la cabecera completa Cookie
- `CfClearance`: cookie `cf_clearance`
- `SdxCookie`: cookie `sdx`
- `UserAgent`: user-agent a reutilizar con las cookies
- `OnlySpanish`: limita resultados a subtítulos en español
- `EnableDebugLogging`: logs más verbosos

## Publicación en GitHub

1. Subí este repo a GitHub.
2. Ajustá `manifest.json` y `manifest.template.json` si cambias usuario, repo o versión.
3. Creá un tag, por ejemplo `v0.1.0.8`.
4. GitHub Actions va a compilar y adjuntar `Jellyfin.Plugin.Subdivx-v0.1.0.8.zip` al release.
5. Publicá `manifest.json` en GitHub Pages, un gist raw o cualquier URL estática.
6. En Jellyfin agregá esa URL en **Dashboard > Plugins > Repositories**.

## Notas importantes

- El acceso directo a Subdivx depende de cookies válidas y puede romperse por cambios de Cloudflare o del frontend del sitio.
- El modo bridge suele ser más robusto para uso continuo.
- Cuando saques una nueva versión, actualizá `version`, `sourceUrl` y `timestamp` en el manifest antes de crear el tag.
