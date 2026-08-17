# Spotify/YouTube Playlist Downloader con C# y yt-dlp

Aplicación de consola en C# que obtiene las canciones de una playlist de **Spotify** o de **YouTube** y descarga el audio de cada canción en formato MP3 utilizando `yt-dlp` y `ffmpeg`.

## Requisitos

* .NET 9 o superior.
* Una aplicación creada en [Spotify for Developers](https://developer.spotify.com) (solo si se usará el modo Spotify).
* Un proyecto en [Google Cloud Console](https://console.cloud.google.com) con la YouTube Data API v3 habilitada (solo si se usará el modo YouTube).
* `yt-dlp.exe`
* `ffmpeg.exe`
* `qjs.exe` (QuickJS-ng) — runtime de JavaScript portable requerido por yt-dlp para resolver los challenges de YouTube.

La estructura esperada del proyecto es:

```text
Proyecto/
├── tools/
│   ├── yt-dlp.exe
│   ├── ffmpeg.exe
│   └── qjs.exe
├── Salida/
├── appsettings.json
└── Proyecto.exe
```

> `appsettings.json` **no se sube al repositorio** (está en `.gitignore`). Contiene las credenciales reales. Usa `appsettings.example.json` como plantilla.

---

## Configuración de credenciales (`appsettings.json`)

Todas las credenciales se leen desde `appsettings.json`, ubicado en la carpeta de salida del proyecto (junto al `.exe`). Crea el archivo con esta estructura:

```json
{
  "Spotify": {
    "ClientId": "tu_client_id_aqui",
    "ClientSecret": "tu_client_secret_aqui"
  },
  "YouTube": {
    "ApiKey": "tu_api_key_aqui"
  }
}
```

Solo necesitas rellenar la sección correspondiente al modo que vayas a usar (Spotify, YouTube, o ambos).

### Por qué no van en el código

Las credenciales hardcodeadas en el código fuente terminan expuestas si el repositorio se sube a GitHub (incluso en repos privados compartidos). Mantenerlas en `appsettings.json` fuera de control de versiones evita ese riesgo. Si en algún momento una credencial real llegó a subirse a git, **debe regenerarse** en el panel correspondiente (Spotify o Google Cloud), ya que quitarla del código no la invalida.

### Cómo se leen en el proyecto

El proyecto usa `Microsoft.Extensions.Configuration` para cargar el archivo:

```csharp
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

string clientId = config["Spotify:ClientId"] ?? "";
string clientSecret = config["Spotify:ClientSecret"] ?? "";
string YtDataApiV3Key = config["YouTube:ApiKey"] ?? "";
```

Paquetes NuGet necesarios:

```bash
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Configuration.Binder
```

Y en el `.csproj`, para que el archivo se copie a la carpeta de salida en cada build:

```xml
<ItemGroup>
  <None Include="appsettings.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

---

## Configuración de Spotify

1. Accede al [Spotify Developer Dashboard](https://developer.spotify.com/dashboard).
2. Crea una aplicación (App name y App description son suficientes).
3. Entra a la app creada → **Settings**.
4. Copia el **Client ID**.
5. Haz clic en **View client secret** y copia el **Client Secret**.
6. Pega ambos valores en `appsettings.json`, bajo `"Spotify"`.

El programa utiliza el flujo **Client Credentials** de OAuth2, pensado para acceso a datos públicos (como playlists públicas) sin necesidad de que un usuario inicie sesión.

---

## Configuración de YouTube (YouTube Data API v3)

Para el modo YouTube se necesita una **API Key** de Google Cloud. A diferencia de Spotify, no requiere OAuth ni "Client Secret" — con una API Key simple es suficiente para leer playlists **públicas**.

### Pasos para obtener la API Key

1. **Crear o seleccionar un proyecto en Google Cloud**
   Entra a [console.cloud.google.com](https://console.cloud.google.com/). En el selector de proyectos (arriba a la izquierda), haz clic en **Nuevo Proyecto**, ponle un nombre (por ejemplo `PlaylistDownloader`) y créalo. Si ya tienes un proyecto, puedes reutilizarlo.

2. **Habilitar la YouTube Data API v3**
   Con el proyecto seleccionado, ve al menú lateral → **APIs y servicios** → **Biblioteca**. Busca `YouTube Data API v3` y haz clic en **Habilitar**.
   También puedes ir directo a la página de la API ya filtrada por tu proyecto:
   `https://console.cloud.google.com/apis/api/youtube.googleapis.com/credentials?project=TU_PROYECTO`
   (reemplazando `TU_PROYECTO` por el ID de tu proyecto). Sin este paso habilitado, cualquier API Key que generes devolverá error `403 - accessNotConfigured` al llamar los endpoints de YouTube.

3. **Crear las credenciales (API Key)**
   Ve a **APIs y servicios** → **Credenciales** → **Crear credenciales** → **Clave de API**. Google genera la key al instante (con el formato `AIzaSy...`). Cópiala.

4. **(Recomendado) Restringir la API Key**
   Justo después de crearla, haz clic en **Restringir clave**. En **Restricciones de API**, selecciona **Restringir clave** y marca únicamente `YouTube Data API v3`. Esto evita que, si la key se filtra, pueda usarse para otras APIs de Google Cloud activas en el mismo proyecto (Maps, Drive, etc). Para una app de escritorio/consola no es necesario configurar restricciones de aplicación (HTTP referrers), ya que esas aplican a peticiones desde navegador.

5. **Pegar la key en `appsettings.json`**

   ```json
   "YouTube": {
     "ApiKey": "AIzaSy..."
   }
   ```

### Cuota

La API tiene una cuota gratuita de **10,000 unidades por día**. El endpoint que usa este proyecto (`playlistItems.list`) cuesta **1 unidad por página** (hasta 50 canciones por página), por lo que una playlist de varios cientos de canciones consume muy poca cuota. No se usa el endpoint `search.list` (que cuesta 100 unidades por llamada), ya que el `videoId` de cada canción se obtiene directamente de `playlistItems.list`, sin necesidad de una búsqueda aparte.

---

## Funcionamiento general

```text
                 ┌─────────────┐
                 │   Modo?     │
                 └──────┬──────┘
             ┌──────────┴──────────┐
             ▼                     ▼
        [Spotify]              [YouTube]
             │                     │
   Client Credentials       (no requiere token,
     → Access Token           solo API Key)
             │                     │
   Obtener tracks de la     Obtener videos de la
   playlist (Nombre +       playlist (Nombre,
   Artista)                 Canal, videoId)
             │                     │
             └──────────┬──────────┘
                         ▼
              Por cada canción:
                         │
        ¿Hay videoId? ──┼── Sí → yt-dlp descarga
             │              directo esa URL
             No
             │
     yt-dlp busca con
     ytsearch1 (Nombre +
     Artista + "audio")
                         │
                         ▼
              Extraer audio (ffmpeg)
                         │
                         ▼
              Convertir a MP3
                         │
                         ▼
              Guardar en ./Salida
```

### Diferencia clave entre modos

* **Modo Spotify**: la API de Spotify solo entrega metadata (nombre y artista), no un enlace a YouTube. Por eso, para cada canción, `yt-dlp` debe **buscar** en YouTube (`ytsearch1:"Nombre Artista audio"`) y descargar el primer resultado. Esto implica que el video descargado es una **aproximación** — no siempre será la grabación exacta.
* **Modo YouTube**: la API de YouTube (`playlistItems.list`) entrega el `videoId` real de cada video de la playlist. Con eso, `yt-dlp` descarga la URL exacta (`https://www.youtube.com/watch?v={videoId}`), sin necesidad de buscar ni adivinar.

---

## 1. Obtener el token de Spotify

El método `ObtenerTokenSpotifyAsync` realiza un `POST` a:

```text
https://accounts.spotify.com/api/token
```

usando autenticación `Basic` con `ClientId:ClientSecret` en Base64, y el body:

```text
grant_type=client_credentials
```

Devuelve el `access_token` que se usa en las siguientes peticiones a la API de Spotify.

## 2. Obtener las canciones de una playlist de Spotify

`ObtenerCancionesDePlaylistAsync` consulta:

```text
https://api.spotify.com/v1/playlists/{playlistId}/tracks
```

en bloques de 100 elementos (`limit=100`), recorriendo todas las páginas con `offset`. Devuelve:

```csharp
List<(string Nombre, string Artista)>
```

## 3. Obtener las canciones de una playlist de YouTube

`ObtenerCancionesDePlaylistYoutubeAsync` consulta:

```text
https://www.googleapis.com/youtube/v3/playlistItems
```

en bloques de 50 elementos (`maxResults=50`), recorriendo todas las páginas con `pageToken`. Ignora videos eliminados o privados. Devuelve:

```csharp
List<(string Nombre, string Artista, string? VideoId)>
```

El `Artista` corresponde al `videoOwnerChannelTitle` (o `channelTitle` si el primero no está disponible) — YouTube no tiene un campo real de "artista" como Spotify, así que se usa el canal que subió el video como aproximación.

## 4. Descargar y convertir el audio

`DescargarCancionDesdeYouTube` recibe el nombre, el artista, la carpeta destino y, opcionalmente, una URL de video directa:

* Si se proporciona `videoUrl` (modo YouTube), `yt-dlp` descarga esa URL exacta.
* Si no (modo Spotify), `yt-dlp` busca con `ytsearch1:"{nombre} {artista} audio"` y descarga el primer resultado.

Parámetros clave del comando:

```text
-f "bestaudio/best"        → mejor audio disponible, con fallback a formato combinado
-x --audio-format mp3      → extrae y convierte el audio a MP3
--extractor-args "youtube:player_client=tv,ios,web,android"
                            → prueba varios "clientes" de YouTube en orden, ya que
                              YouTube ha ido restringiendo la descarga directa
                              (ver sección "Sobre las restricciones de YouTube")
--js-runtimes "quickjs:{ruta a qjs.exe}"
--remote-components ejs:github
                            → runtime de JavaScript requerido por yt-dlp para resolver
                              los challenges de YouTube (ver más abajo)
```

## 5. Nombre de los archivos

Los archivos se guardan como:

```text
Artista - Canción.mp3
```

Eliminando caracteres problemáticos para nombres de archivo:

```csharp
.Replace("\"", "")
.Replace(":", "")
.Replace("?", "")
.Replace("/", "")
.Replace("\\", "");
```

## 6. Herramientas externas (`tools/`)

El programa verifica que las tres herramientas existan antes de intentar descargar, e indica cuál falta:

```csharp
var faltantes = new List<string>();
if (!File.Exists(YtDlpPath)) faltantes.Add("yt-dlp.exe");
if (!File.Exists(QjsPath)) faltantes.Add("qjs.exe");
if (!File.Exists(FfmpegPath)) faltantes.Add("ffmpeg.exe");
```

También agrega la carpeta `tools/` al `PATH` del proceso, para que `yt-dlp` pueda localizar `ffmpeg.exe`:

```csharp
startInfo.Environment["PATH"] = ToolsPath + ";" + Environment.GetEnvironmentVariable("PATH");
```

### Sobre las restricciones de YouTube (SABR / PO Token / JS runtime)

Desde 2025, YouTube ha ido restringiendo progresivamente la descarga directa de video/audio:

* El cliente `web` de YouTube dejó de entregar enlaces de descarga directos para muchos formatos, forzando el uso de su protocolo propietario ("SABR").
* Algunos formatos ahora requieren resolver un desafío de JavaScript ("challenge"), para lo cual `yt-dlp` necesita un runtime externo (`qjs.exe` en este proyecto — también son compatibles Deno, Node.js o Bun).
* Ciertos clientes (`ios`, a veces `tv`) requieren un "PO Token" adicional, o aplican DRM, y son descartados automáticamente por `yt-dlp` si no están disponibles.

Por eso el proyecto configura varios clientes en cadena (`tv,ios,web,android`): `yt-dlp` los prueba en ese orden y usa el primero que logre entregar un formato descargable, normalmente cayendo en el cliente `web` con el formato `18` (video+audio combinado, MP4) cuando los formatos de solo-audio están bloqueados. Esto es válido y funcional — simplemente implica que en algunos videos la calidad de audio final puede ser algo menor a la de un formato de audio puro.

Este es un ajuste que puede requerir mantenimiento en el futuro, ya que YouTube continúa modificando su infraestructura de streaming.

## 7. Manejo de advertencias y errores

La salida de `yt-dlp` (`stderr`) se separa línea por línea:

* Líneas que empiezan con `WARNING:` → se muestran bajo **Advertencias** (no implican que la descarga haya fallado).
* Líneas que empiezan con `ERROR:` (o cualquier otra sin prefijo reconocido) → se muestran bajo **Errores**.

El éxito o fracaso real de la descarga se determina por el código de salida del proceso (`proceso.ExitCode == 0`), no por la presencia de advertencias.

## 8. Resumen final

Al terminar, el programa muestra:

```text
Descargas exitosas: 15
Descargas fallidas: 2

Canciones con error:
- Artista 1 - Canción 1
- Artista 2 - Canción 2
```

---

## Dependencias externas

| Herramienta | Función | Ubicación esperada |
|---|---|---|
| `yt-dlp.exe` | Búsqueda y descarga desde YouTube | `tools/yt-dlp.exe` |
| `ffmpeg.exe` | Extracción y conversión de audio a MP3 | `tools/ffmpeg.exe` |
| `qjs.exe` (QuickJS-ng) | Runtime de JavaScript para resolver challenges de YouTube | `tools/qjs.exe` |

---

## Consideraciones

* **Modo Spotify**: el programa descarga contenido desde YouTube basándose en el nombre y artista obtenidos desde Spotify. El primer resultado de la búsqueda no necesariamente corresponde a la grabación original exacta.
* **Modo YouTube**: al usar el `videoId` real de la playlist, la descarga corresponde exactamente al video original, sin ambigüedad de búsqueda.
* El uso del contenido descargado debe respetar los derechos de autor y los términos de servicio de las plataformas involucradas.
* `appsettings.json` contiene credenciales sensibles y **no debe subirse a un repositorio público ni compartirse**.