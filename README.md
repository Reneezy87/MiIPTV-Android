# Mi IPTV para Android (TV y celular)

App de Android que abre tu reproductor (`index.html`) servido desde tu PC. Funciona en:

- **Android TV / Google TV** (Sony, TCL, Hisense, Xiaomi, Chromecast con Google TV): aparece en el menú de apps
- **Fire TV Stick**
- **Celulares y tablets Android** (5.0 o superior)

No funciona en Samsung (Tizen), LG (webOS) ni Roku, porque esas teles no instalan APKs.

## Cómo funciona

La app no trae los canales adentro: se conecta a tu PC, que sirve el reproductor y la lista. Por eso:

1. La PC tiene que estar encendida con el servidor corriendo (ver "Servidor automático" abajo).
2. La tele o el celular tienen que estar en la misma red Wi-Fi que la PC.

## Compilar el APK

### Opción A: Android Studio

1. Instala [Android Studio](https://developer.android.com/studio).
2. *File → Open* y elige la carpeta `MiIPTV-Android`. Espera a que termine "Gradle sync" (la primera vez descarga varias cosas).
3. *Build → Build App Bundle(s) / APK(s) → Build APK(s)*.
4. Al terminar, clic en **locate**: el archivo es `app/build/outputs/apk/debug/app-debug.apk`.

### Opción B: GitHub (sin instalar nada)

1. Sube esta carpeta a un repositorio de GitHub (puede ser privado).
2. Ve a la pestaña **Actions → Compilar APK → Run workflow**.
3. Cuando termine (2–4 min), descarga el APK desde **Artifacts**.

## Instalar

**En la tele (Android TV / Google TV):**

1. En la tele, instala desde Play Store la app **Send Files to TV** (y también en tu celular), o **Downloader** de AFTVnews.
2. En la tele: *Configuración → Sistema → Acerca de* y presiona 7 veces **Compilación del SO** para activar las opciones de desarrollador. Luego en *Configuración → Apps → Seguridad*, permite **fuentes desconocidas** para la app con la que vas a instalar.
3. Manda el APK con Send Files to TV (o con una USB) y ábrelo para instalar.

**En Fire TV:** *Configuración → Mi Fire TV → Opciones para desarrolladores → Apps de origen desconocido*, y usa la app **Downloader**.

**En el celular:** pasa el APK (WhatsApp, Drive, cable), ábrelo y permite instalar apps de origen desconocido cuando lo pida.

**Con cable o por red (adb):**
```
adb connect 192.168.1.80:5555
adb install -r app-debug.apk
```

## Primer uso

Al abrir la app te pide la **dirección del servidor**: escribe `http://IP-DE-TU-PC:8766/` (la IP sale con `ipconfig` en la PC).

## Controles

| Control remoto | Acción |
|---|---|
| Flechas / OK | Moverse y elegir |
| Atrás | Regresar (del video a la lista, del detalle a la cuadrícula…). En la pantalla principal abre el menú: Salir / Recargar / Cambiar servidor |
| Canal + / Canal − | Siguiente / anterior canal (o episodio) |
| Menú / Guía | Mostrar u ocultar la lista |
| Play/Pausa | Pausar o continuar |

## Servidor automático en la PC

Para no tener que abrir PowerShell cada vez, crea una tarea que inicie el servidor al prender la PC
(ejecuta PowerShell **como administrador** y ajusta la ruta):

```powershell
$python = (Get-Command python).Source
$action = New-ScheduledTaskAction -Execute $python `
  -Argument '-m http.server 8766 --bind 0.0.0.0 --directory "E:\IptvCleaner\IptvCleaner\output"'
$trigger = New-ScheduledTaskTrigger -AtStartup
Register-ScheduledTask -TaskName "Mi IPTV servidor" -Action $action -Trigger $trigger -User "SYSTEM" -RunLevel Highest
```

Además, en tu módem reserva una IP fija para la PC (DHCP reservation), para que la dirección no cambie.

## Cambiar el nombre, ícono o paquete

- Nombre: `app/src/main/res/values/strings.xml`
- Ícono: `app/src/main/res/drawable/ic_launcher.xml` y el banner de TV en `banner.xml`
- Paquete: `applicationId` en `app/build.gradle.kts`

## Sitio web (GitHub Pages)

Este repo también publica el reproductor para usarlo fuera de tu red:

- `web/index.html` → el reproductor
- `web/mis-canales.m3u` → canales agregados a mano
- `tools/IptvCleaner/` → genera `mi-lista.m3u` (edita `config.json` para cambiar países o categorías)
- `.github/workflows/publicar.yml` → genera la lista y publica cada lunes y en cada cambio de `web/` o `tools/`

Dirección del sitio: `https://reneezy87.github.io/MiIPTV-Android/`
