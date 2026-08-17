using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

internal class Program
{
    public enum Modo
    {
        Spotify = 1,
        Youtube = 2
    }
    private static async Task Main(string[] args)
    {
        // Ingresa tu Client ID y Client Secret del proyecto de Spotify. Para obtenerlos, revisa el MD para mas información.
        // O ingresa la key de la API de YT. Para obtenerla, revisa el MD para mas información.

        Console.OutputEncoding = Encoding.UTF8;
        Console.Clear();

        var config = new ConfigurationBuilder()
                    .SetBasePath(AppContext.BaseDirectory)
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();

        string clientId = config["Spotify:ClientId"] ?? "";
        string clientSecret = config["Spotify:ClientSecret"] ?? "";
        string YtDataApiV3Key = config["YouTube:ApiKey"] ?? "";

        Modo? ModoApp = null;

        while (ModoApp == null)
        {
            Console.WriteLine("Seleccione modo:\n1) Spotify\n2) YT");
            string? entrada = Console.ReadLine();

            if (int.TryParse(entrada, out int opcion) && Enum.IsDefined(typeof(Modo), opcion))
            {
                ModoApp = (Modo)opcion;
            }
            else
            {
                Console.WriteLine("Opción no válida. Intente nuevamente.\n");
            }
        }

        Console.WriteLine($"Modo elegido: {ModoApp}");

        string carpetaDestino = "./Salida";

        if (!Directory.Exists(carpetaDestino))
            Directory.CreateDirectory(carpetaDestino);

        List<(string Nombre, string Artista, string? VideoId)> Canciones = [];

        if (ModoApp == Modo.Spotify)
        {
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                Console.WriteLine("El clientId o clientSecret no pueden ser nulos o vacios, revisa el MD.");
                return;
            }

            var accessToken = await ObtenerTokenSpotifyAsync(clientId, clientSecret);

            Console.WriteLine("Ingresa el id de la playlist");
            string? playlistId = Console.ReadLine() ?? "4RFCmMo7L3zJFEQKCGtv1R";

            if (string.IsNullOrEmpty(playlistId))
            {
                Console.WriteLine("El id de la playlist no puede ser nulo o vacio");
                return;
            }

            if (string.IsNullOrEmpty(accessToken))
            {
                Console.WriteLine("El clientId o clientSecret son incorrectos");
                return;
            }

            Console.WriteLine($"Obteniendo canciones de la playlist {playlistId}...");
            var CancionesSpotify = await ObtenerCancionesDePlaylistAsync(playlistId, accessToken);
            Canciones = [.. CancionesSpotify.Select(c => (c.Nombre, c.Artista, (string?)null))];
        }
        else if (ModoApp == Modo.Youtube)
        {
            if (string.IsNullOrEmpty(YtDataApiV3Key))
            {
                Console.WriteLine("La key de la API de YT no pueden ser nula o vacia, revisa el MD.");
                return;
            }

            Console.WriteLine("Ingresa el id de la playlist");
            string? playlistId = Console.ReadLine();
            playlistId = string.IsNullOrEmpty(playlistId) ? "PLRY77yiPsKP8" : playlistId;

            if (string.IsNullOrEmpty(playlistId))
            {
                Console.WriteLine("El id de la playlist no puede ser nulo o vacio");
                return;
            }

            Console.WriteLine($"Obteniendo canciones de la playlist {playlistId}...");
            Canciones = await ObtenerCancionesDePlaylistYoutubeAsync(playlistId, YtDataApiV3Key);
        }


        int exitosas = 0;
        int fallidas = 0;
        var errores = new List<(string Nombre, string Artista)>();

        foreach (var (Nombre, Artista, VideoId) in Canciones)
        {
            string ArtistaProcesado = Artista.Replace("- Topic", "").ToString();
            string? videoUrl = VideoId != null ? $"https://www.youtube.com/watch?v={VideoId}" : null;

            bool exito = DescargarCancionDesdeYouTube(Nombre, ArtistaProcesado, carpetaDestino, videoUrl);
            if (exito)
            {
                exitosas++;
            }
            else
            {
                fallidas++;
                errores.Add((Nombre, ArtistaProcesado));
            }

            MostrarProgreso(exitosas + fallidas, Canciones.Count);
        }

        Console.WriteLine($"\nDescargas exitosas: {exitosas}");
        Console.WriteLine($"Descargas fallidas: {fallidas}");

        if (errores.Count > 0)
        {
            Console.WriteLine("\nCanciones con error:");
            foreach (var (Nombre, Artista) in errores)
            {
                Console.WriteLine($"- {Artista} - {Nombre}");
            }
        }
    }

    static void MostrarProgreso(int actual, int total)
    {
        int ancho = 30;
        double porcentaje = total > 0 ? (double)actual / total : 0;
        int completado = (int)(ancho * porcentaje);

        string barra = new string('█', completado) + new string('░', ancho - completado);
        Console.WriteLine($"Progreso: [{barra}] {actual}/{total} ({porcentaje:P0})");
    }

    public static async Task<string?> ObtenerTokenSpotifyAsync(string clientId, string clientSecret)
    {
        using var client = new HttpClient();
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", auth);

        var content = new FormUrlEncodedContent(new[]
        {
        new KeyValuePair<string, string>("grant_type", "client_credentials")
    });

        var response = await client.PostAsync("https://accounts.spotify.com/api/token", content);
        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        if (!response.IsSuccessStatusCode)
        {
            Console.WriteLine($"Error al obtener token de Spotify: {response.RequestMessage}");
            return null;
        }

        return data.GetProperty("access_token").GetString();
    }

    public static async Task<List<(string Nombre, string Artista)>> ObtenerCancionesDePlaylistAsync(string playlistId, string accessToken)
    {
        var canciones = new List<(string, string)>();
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        int offset = 0;
        const int limit = 100;

        while (true)
        {
            var url = $"https://api.spotify.com/v1/playlists/{playlistId}/tracks?limit={limit}&offset={offset}";
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            var data = JsonSerializer.Deserialize<JsonElement>(json);
            var items = data.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                var track = item.GetProperty("track");

                if (track.ValueKind == JsonValueKind.Null) continue;

                var nombre = track.GetProperty("name").GetString() ?? "";
                var artistas = track.GetProperty("artists")[0].GetProperty("name").GetString() ?? "";

                canciones.Add((nombre, artistas));
            }

            int cantidadActual = items.GetArrayLength();
            if (cantidadActual < limit)
                break; // ya no hay más canciones

            offset += limit;
        }

        return canciones;
    }

    public static async Task<List<(string Nombre, string Artista, string? VideoId)>> ObtenerCancionesDePlaylistYoutubeAsync(
    string playlistId, string apiKey)
    {
        var canciones = new List<(string, string, string?)>();
        using var client = new HttpClient();

        string? nextPageToken = null;

        do
        {
            var url = $"https://www.googleapis.com/youtube/v3/playlistItems" +
                       $"?part=snippet" +
                       $"&maxResults=50" +
                       $"&playlistId={playlistId}" +
                       $"&key={apiKey}" +
                       (nextPageToken != null ? $"&pageToken={nextPageToken}" : "");

            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Error al obtener canciones de YouTube: {json}");
                break;
            }

            var data = JsonSerializer.Deserialize<JsonElement>(json);
            var items = data.GetProperty("items");

            foreach (var item in items.EnumerateArray())
            {
                var snippet = item.GetProperty("snippet");

                if (snippet.TryGetProperty("title", out var titleProp) &&
                    (titleProp.GetString() == "Deleted video" || titleProp.GetString() == "Private video"))
                    continue;

                var nombre = snippet.GetProperty("title").GetString() ?? "";

                var artista = snippet.TryGetProperty("videoOwnerChannelTitle", out var canalProp)
                    ? canalProp.GetString() ?? ""
                    : snippet.GetProperty("channelTitle").GetString() ?? "";

                string? videoId = snippet.TryGetProperty("resourceId", out var resourceIdProp) &&
                                   resourceIdProp.TryGetProperty("videoId", out var videoIdProp)
                    ? videoIdProp.GetString()
                    : null;

                canciones.Add((nombre, artista, videoId));
            }

            nextPageToken = data.TryGetProperty("nextPageToken", out var tokenProp)
                ? tokenProp.GetString()
                : null;

        } while (!string.IsNullOrEmpty(nextPageToken));

        return canciones;
    }

    // Metodo viejo para descargar Videos
    // public static bool DescargarVideoDesdeYouTube(string urlYoutube, string carpetaDestino)
    // {
    //     string YtDlpPath = Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe");

    //     if (!File.Exists(YtDlpPath))
    //     {
    //         Console.WriteLine("Falta yt-dlp.exe en la carpeta tools.");
    //         return false;
    //     }

    //     string nombreArchivo = $"%(title)s.%(ext)s";

    //     var startInfo = new ProcessStartInfo
    //     {
    //         StandardOutputEncoding = Encoding.UTF8,
    //         StandardErrorEncoding = Encoding.UTF8,
    //         FileName = YtDlpPath,
    //         Arguments = $"\"{urlYoutube}\" -o \"{carpetaDestino}/{nombreArchivo}\"",
    //         RedirectStandardOutput = true,
    //         RedirectStandardError = true,
    //         UseShellExecute = false,
    //         CreateNoWindow = true
    //     };

    //     var proceso = new Process { StartInfo = startInfo };

    //     try
    //     {
    //         proceso.Start();
    //         string output = proceso.StandardOutput.ReadToEnd();
    //         string error = proceso.StandardError.ReadToEnd();
    //         proceso.WaitForExit();

    //         Console.WriteLine($"Video descargado: {urlYoutube}");
    //         Console.WriteLine(output);
    //         if (!string.IsNullOrWhiteSpace(error))
    //             Console.WriteLine("Errores:\n" + error);

    //         return proceso.ExitCode == 0;
    //     }
    //     catch (Exception ex)
    //     {
    //         Console.WriteLine($"Error al descargar el video: {ex.Message}");
    //         return false;
    //     }
    // }

    public static bool DescargarCancionDesdeYouTube(string nombreCancion, string artista, string carpetaDestino, string? videoUrl = null)
    {
        string ToolsPath = Path.Combine(AppContext.BaseDirectory, "tools");
        string YtDlpPath = Path.Combine(ToolsPath, "yt-dlp.exe");
        string QjsPath = Path.Combine(ToolsPath, "qjs.exe");
        string FfmpegPath = Path.Combine(ToolsPath, "ffmpeg.exe");
        var faltantes = new List<string>();
        if (!File.Exists(YtDlpPath)) faltantes.Add("yt-dlp.exe");
        if (!File.Exists(QjsPath)) faltantes.Add("qjs.exe");
        if (!File.Exists(FfmpegPath)) faltantes.Add("ffmpeg.exe");
        if (faltantes.Count > 0)
        {
            Console.WriteLine($"Falta(n) en la carpeta tools: {string.Join(", ", faltantes)}");
            return false;
        }

        string nombreArchivo = $"{artista} - {nombreCancion}".Replace("\"", "").Replace(":", "").Replace("?", "").Replace("/", "").Replace("\\", "");

        // Si tenemos videoId/URL directa (modo YouTube), la usamos.
        // Si no (modo Spotify), usamos búsqueda de yt-dlp.
        string fuente = !string.IsNullOrEmpty(videoUrl)
            ? $"\"{videoUrl}\""
            : $"ytsearch1:\"{nombreCancion} {artista} audio\"";

        string jsRuntimeArg = File.Exists(QjsPath)
            ? $"--js-runtimes \"quickjs:{QjsPath}\" --remote-components ejs:github"
            : "";

        var startInfo = new ProcessStartInfo
        {
            FileName = YtDlpPath,
            Arguments = $"{fuente} -f \"bestaudio/best\" -x --audio-format mp3 " +
                        $"--extractor-args \"youtube:player_client=tv,ios,web,android\" " +
                        $"{jsRuntimeArg} " +
                        $"-o \"{carpetaDestino}/{nombreArchivo}.%(ext)s\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.Environment["PATH"] = ToolsPath + ";" + Environment.GetEnvironmentVariable("PATH");

        var proceso = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        // Se imprime línea por línea, en tiempo real, a medida que yt-dlp las va generando
        proceso.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
                Console.WriteLine(e.Data);
        };

        proceso.ErrorDataReceived += (sender, e) =>
        {
            if (string.IsNullOrEmpty(e.Data)) return;

            if (e.Data.StartsWith("WARNING:"))
                Console.WriteLine($"[Advertencia] {e.Data}");
            else if (e.Data.StartsWith("ERROR:"))
                Console.WriteLine($"[Error] {e.Data}");
            else
                Console.WriteLine($"[Error] {e.Data}");
        };

        try
        {
            Console.WriteLine($"\nDescargando: {artista} - {nombreCancion}");

            proceso.Start();
            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();
            proceso.WaitForExit();

            bool exito = proceso.ExitCode == 0;

            Console.WriteLine(exito
                ? $"Descargado: {artista} - {nombreCancion}"
                : $"FALLÓ: {artista} - {nombreCancion}");

            return exito;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al descargar {artista} - {nombreCancion}: {ex.Message}");
            return false;
        }
    }
}