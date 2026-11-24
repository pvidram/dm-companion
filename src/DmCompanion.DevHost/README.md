# DmCompanion.DevHost

Host de desarrollo en el navegador (Blazor Web App, Interactive Server) para probar las pantallas de `DmCompanion.Screens` sin MAUI ni emulador. Solo para desarrollo: nunca se publica.

## Ejecutar

```bash
dotnet watch --project src/DmCompanion.DevHost
```

Abre http://localhost:5180 desde Windows (WSL2 reenvía el puerto de loopback).

## Vault

- Por defecto trabaja sobre una copia de `samples/vault-demo` en `~/.local/share/dm-companion-devhost/vault-demo`. Se crea la primera vez, solo si la carpeta no existe.
- Para volver al estado inicial: borra esa carpeta y reinicia.
- `DevHost:VaultPath` (appsettings, variable `DevHost__VaultPath` o `--DevHost:VaultPath=...`) usa otra carpeta. Solo se aplica si se pone de forma explícita y nunca se siembra ni se copia nada en ella; ojo: la app puede escribir en ese vault.
- `DevHost:SeedFrom` cambia el origen de la copia (por defecto se busca `samples/vault-demo` subiendo desde el proyecto).
- Las preferencias van en `~/.local/share/dm-companion-devhost/prefs.json`.

## Seguridad

- Escucha **solo en loopback** (puerto 5180). Al arrancar comprueba las direcciones y se detiene si alguna no es loopback.
- **Sin autenticación**: sirve el contenido del vault a cualquiera que llegue al puerto.
- No lo publiques, no lo metas en un contenedor ni expongas el puerto (ni con `netsh portproxy`, túneles o port forwarding).
