# Spotify Playlist Downloader con C# y yt-dlp

Aplicación de consola en C# que obtiene las canciones de una playlist de Spotify mediante la API de Spotify y posteriormente busca y descarga cada canción desde YouTube utilizando `yt-dlp` y `ffmpeg`.

## Requisitos

* .NET 9 o superior.
* Una aplicación creada en [Spotify for Developers](https://developer.spotify.com).
* `Client ID` y `Client Secret` de Spotify.
* `yt-dlp.exe`.
* `ffmpeg.exe`.

La estructura esperada de la aplicación es:

```text
Proyecto/
├── tools/
│   ├── yt-dlp.exe
│   └── ffmpeg.exe
├── Salida/
└── Proyecto.exe
```

## Configuración de Spotify

Para obtener las credenciales:

1. Accede al [Spotify Developer Dashboard](https://developer.spotify.com).
2. Crea una aplicación.
3. Obtén el `Client ID`.
4. Obtén el `Client Secret`.
5. Asigna ambos valores en el código:

```csharp
string ClientId = "";
string ClientSecret = "";
```

El programa utiliza el flujo **Client Credentials** para obtener un token de acceso.

## Funcionamiento

El proceso general es:

```text
Spotify
   │
   ▼
Obtener Access Token
   │
   ▼
Obtener canciones de la Playlist
   │
   ▼
Nombre + Artista
   │
   ▼
Buscar en YouTube mediante yt-dlp
   │
   ▼
Descargar audio
   │
   ▼
Convertir a MP3 mediante ffmpeg
   │
   ▼
Guardar en ./Salida
```

## 1. Obtener el token de Spotify

El método `ObtenerTokenSpotifyAsync` realiza una petición `POST` contra:

```text
https://accounts.spotify.com/api/token
```

Utiliza las credenciales mediante autenticación `Basic` y solicita un token utilizando:

```text
grant_type=client_credentials
```

El método devuelve el `access_token` generado por Spotify.

## 2. Obtener las canciones de la playlist

El método `ObtenerCancionesDePlaylistAsync` consulta:

```text
https://api.spotify.com/v1/playlists/{playlistId}/tracks
```

Las canciones se obtienen en bloques de 100 elementos:

```csharp
const int Limit = 100;
```

Se utiliza `offset` para recorrer todas las páginas de la playlist.

El método devuelve una lista de tuplas:

```csharp
List<(string Nombre, string Artista)>
```

Por ejemplo:

```text
Nombre: Reflexiones
Artista: Esto es Eso
```

## 3. Buscar y descargar la canción

Para cada canción se genera una búsqueda utilizando:

```text
NombreCancion + Artista + audio
```

Por ejemplo:

```text
Reflexiones Esto es Eso audio
```

`yt-dlp` utiliza:

```text
ytsearch1
```

para seleccionar el primer resultado de YouTube.

La ejecución utiliza:

```text
-x --audio-format mp3
```

Esto indica a `yt-dlp` que extraiga el audio y lo convierta a formato MP3.

## 4. Nombre de los archivos

Los archivos se guardan utilizando el siguiente formato:

```text
Artista - Canción.mp3
```

Por ejemplo:

```text
Esto es Eso - Reflexiones.mp3
```

El código elimina algunos caracteres que pueden provocar problemas en nombres de archivos:

```csharp
.Replace("\"", "")
.Replace(":", "")
.Replace("?", "")
.Replace("/", "")
.Replace("\\", "");
```

## 5. Uso de ffmpeg

`yt-dlp` necesita `ffmpeg` para realizar la conversión del audio.

El programa comprueba que ambos ejecutables existan:

```csharp
if (!File.Exists(YtDlpPath) || !File.Exists(Path.Combine(ToolsPath, "ffmpeg.exe")))
{
    Console.WriteLine("Faltan yt-dlp.exe o ffmpeg.exe en la carpeta tools.");
    return false;
}
```

También agrega la carpeta `tools` al `PATH` del proceso:

```csharp
StartInfo.Environment["PATH"] = ToolsPath + ";" + Environment.GetEnvironmentVariable("PATH");
```

De esta forma, `yt-dlp` puede localizar `ffmpeg.exe`.

## 6. Control de errores

El programa mantiene dos contadores:

```csharp
int Exitosas = 0;
int Fallidas = 0;
```

También almacena las canciones que no pudieron descargarse:

```csharp
var Errores = new List<(string Nombre, string Artista)>();
```

Al finalizar muestra un resumen:

```text
Descargas exitosas: 15
Descargas fallidas: 2

Canciones con error:
- Artista 1 - Canción 1
- Artista 2 - Canción 2
```

## Dependencias externas

### yt-dlp

`yt-dlp` es el ejecutable utilizado para realizar las búsquedas y descargas desde YouTube.

Debe colocarse en:

```text
tools/yt-dlp.exe
```

### ffmpeg

`ffmpeg` se utiliza para extraer y convertir el audio a MP3.

Debe colocarse en:

```text
tools/ffmpeg.exe
```

## Consideraciones

El programa descarga contenido desde YouTube basándose en los nombres de las canciones obtenidas desde Spotify. Por lo tanto, el primer resultado de la búsqueda no necesariamente será la grabación exacta correspondiente a la canción original.

Además, el uso de contenido descargado debe realizarse respetando los derechos de autor y los términos de servicio de las plataformas involucradas 👀.