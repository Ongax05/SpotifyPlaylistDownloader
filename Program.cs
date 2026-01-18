using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

internal class Program
{
    private static async Task Main(string[] args)
    {
        string carpetaDestino = "./Salida";

        if (!Directory.Exists(carpetaDestino))
            Directory.CreateDirectory(carpetaDestino);

        DescargarVideoDesdeYouTube("https://www.youtube.com/watch?v=huW3K7105y8", carpetaDestino);

        // var accessToken = await ObtenerTokenSpotifyAsync();

        // Console.WriteLine("Ingresa el id de la playlist");
        // string? playlistId = "4RFCmMo7L3zJFEQKCGtv1R";

        // if (string.IsNullOrEmpty(playlistId))
        // {
        //     Console.WriteLine("El id de la playlist no puede ser nulo o vacio");
        //     return;
        // }

        // if (string.IsNullOrEmpty(accessToken))
        // {
        //     Console.WriteLine("El clientId o clientSecret son incorrectos");
        //     return;
        // }


        // var canciones = await ObtenerCancionesDePlaylistAsync(playlistId, accessToken);

        // int exitosas = 0;
        // int fallidas = 0;
        // var errores = new List<(string Nombre, string Artista)>();

        // foreach (var (Nombre, Artista) in canciones)
        // {
        //     bool exito = DescargarCancionDesdeYouTube(Nombre, Artista, carpetaDestino);
        //     if (exito)
        //     {
        //         exitosas++;
        //     }
        //     else
        //     {
        //         fallidas++;
        //         errores.Add((Nombre, Artista));
        //     }
        // }

        // Console.WriteLine($"\nDescargas exitosas: {exitosas}");
        // Console.WriteLine($"Descargas fallidas: {fallidas}");

        // if (errores.Count > 0)
        // {
        //     Console.WriteLine("\nCanciones con error:");
        //     foreach (var (Nombre, Artista) in errores)
        //     {
        //         Console.WriteLine($"- {Artista} - {Nombre}");
        //     }
        // }
    }

    public static bool DescargarVideoDesdeYouTube(string urlYoutube, string carpetaDestino)
    {
        string YtDlpPath = Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe");

        if (!File.Exists(YtDlpPath))
        {
            Console.WriteLine("Falta yt-dlp.exe en la carpeta tools.");
            return false;
        }

        string nombreArchivo = $"%(title)s.%(ext)s";

        var startInfo = new ProcessStartInfo
        {
            FileName = YtDlpPath,
            Arguments = $"\"{urlYoutube}\" -o \"{carpetaDestino}/{nombreArchivo}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var proceso = new Process { StartInfo = startInfo };

        try
        {
            proceso.Start();
            string output = proceso.StandardOutput.ReadToEnd();
            string error = proceso.StandardError.ReadToEnd();
            proceso.WaitForExit();

            Console.WriteLine($"Video descargado: {urlYoutube}");
            Console.WriteLine(output);
            if (!string.IsNullOrWhiteSpace(error))
                Console.WriteLine("Errores:\n" + error);

            return proceso.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al descargar el video: {ex.Message}");
            return false;
        }
    }


    public static async Task<string?> ObtenerTokenSpotifyAsync()
    {
#warning Ingresa tu Client ID y Client Secret del proyecto de Spotify. Para obtenerlos, visita https://developer.spotify.com/dashboard y crea una aplicación si aún no la tienes.

        string clientId = "";
        string clientSecret = "";

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

                var nombre = track.GetProperty("name").GetString();
                var artistas = track.GetProperty("artists")[0].GetProperty("name").GetString();

                canciones.Add((nombre, artistas));
            }

            int cantidadActual = items.GetArrayLength();
            if (cantidadActual < limit)
                break; // ya no hay más canciones

            offset += limit;
        }

        return canciones;
    }


    public static bool DescargarCancionDesdeYouTube(string nombreCancion, string artista, string carpetaDestino)
    {
        string YtDlpPath = Path.Combine(AppContext.BaseDirectory, "tools", "yt-dlp.exe");
        string ToolsPath = Path.Combine(AppContext.BaseDirectory, "tools");

        if (!File.Exists(YtDlpPath) || !File.Exists(Path.Combine(ToolsPath, "ffmpeg.exe")))
        {
            Console.WriteLine("Faltan yt-dlp.exe o ffmpeg.exe en la carpeta tools.");
            return false;
        }

        string nombreArchivo = $"{artista} - {nombreCancion}".Replace("\"", "").Replace(":", "").Replace("?", "").Replace("/", "").Replace("\\", "");
        var busqueda = $"{nombreCancion} {artista} audio";

        var startInfo = new ProcessStartInfo
        {
            FileName = YtDlpPath,
            Arguments = $"ytsearch1:\"{busqueda}\" -x --audio-format mp3 -o \"{carpetaDestino}/{nombreArchivo}.%(ext)s\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.Environment["PATH"] = ToolsPath + ";" + Environment.GetEnvironmentVariable("PATH");

        var proceso = new Process { StartInfo = startInfo };

        try
        {
            proceso.Start();
            string output = proceso.StandardOutput.ReadToEnd();
            string error = proceso.StandardError.ReadToEnd();
            proceso.WaitForExit();

            Console.WriteLine($"Descargado: {artista} - {nombreCancion}");
            Console.WriteLine(output);
            if (!string.IsNullOrWhiteSpace(error))
                Console.WriteLine("Errores:\n" + error);

            return proceso.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al descargar {artista} - {nombreCancion}: {ex.Message}");
            return false;
        }
    }
}