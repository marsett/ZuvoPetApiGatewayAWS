using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Fonts;
using ZuvoPetApiGatewayAWS.Services;

namespace ZuvoPetApiGatewayAWS.Helpers
{
    public class HelperAvatarDinamico
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private static Font? _cachedFont = null;

        // URLs de fuentes gratuitas disponibles online
        private static readonly string[] FONT_URLS = {
           "https://github.com/edx/edx-fonts/blob/master/open-sans/fonts/Bold/OpenSans-Bold.ttf?raw=true",
           "https://github.com/Unity-Technologies/FPSSample/blob/master/Assets/Fonts/Roboto-Bold.ttf?raw=true"
        };


        public static string GetIniciales(string nombre)
        {
            string[] palabras = nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string iniciales = "";
            foreach (var palabra in palabras)
            {
                iniciales += char.ToUpper(palabra[0]);
                if (iniciales.Length == 2) break;
            }
            return iniciales;
        }

        public static async Task<byte[]> GenerarAvatarAsync(string iniciales)
        {
            int ancho = 150, alto = 150;
            using var image = new Image<Rgba32>(ancho, alto);
            var colorFondo = GenerarColorAleatorio();
            var backgroundColor = Color.FromRgb(colorFondo.R, colorFondo.G, colorFondo.B);

            var font = await GetFontAsync(50);

            image.Mutate(ctx =>
            {
                ctx.Fill(backgroundColor);
                var richTextOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Origin = new PointF(ancho / 2f, alto / 2f)
                };
                ctx.DrawText(richTextOptions, iniciales, Color.White);
            });

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        private static async Task<Font> GetFontAsync(float size)
        {
            // Si ya tenemos la fuente en caché, la reutilizamos
            if (_cachedFont != null && Math.Abs(_cachedFont.Size - size) < 0.1f)
            {
                return _cachedFont;
            }

            var fontCollection = new FontCollection();

            try
            {
                // Primero intenta cargar fuente local si existe
                string fontPath = Path.Combine(AppContext.BaseDirectory, "Resources", "Fonts", "MarvelRxsdwfreegular-Dj83.TTF");
                if (File.Exists(fontPath))
                {
                    var family = fontCollection.Add(fontPath);
                    _cachedFont = family.CreateFont(size, FontStyle.Regular);
                    return _cachedFont;
                }

                // Si no hay fuente local, descarga desde URL
                return await LoadFontFromUrlAsync(fontCollection, size);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error cargando fuente: {ex.Message}");
                // Fallback: crear fuente básica
                return await LoadFontFromUrlAsync(fontCollection, size);
            }
        }

        private static async Task<Font> LoadFontFromUrlAsync(FontCollection fontCollection, float size)
        {
            foreach (var fontUrl in FONT_URLS)
            {
                try
                {
                    Console.WriteLine($"Descargando fuente desde: {fontUrl}");
                    using var response = await _httpClient.GetAsync(fontUrl);
                    response.EnsureSuccessStatusCode();

                    await using var sourceStream = await response.Content.ReadAsStreamAsync();
                    using var memoryStream = new MemoryStream();
                    await sourceStream.CopyToAsync(memoryStream);
                    memoryStream.Seek(0, SeekOrigin.Begin);

                    var family = fontCollection.Add(memoryStream);
                    _cachedFont = family.CreateFont(size, FontStyle.Bold); // Forzamos Bold si aplica
                    Console.WriteLine("Fuente cargada correctamente desde URL");
                    return _cachedFont;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error descargando fuente desde {fontUrl}: {ex.Message}");
                }
            }

            throw new InvalidOperationException("No se pudo cargar ninguna fuente desde las URLs disponibles");
        }


        public static (byte R, byte G, byte B) GenerarColorAleatorio()
        {
            Random rand = new Random();
            while (true)
            {
                byte r = (byte)rand.Next(100, 256);
                byte g = (byte)rand.Next(100, 256);
                byte b = (byte)rand.Next(100, 256);
                double luminosidad = (0.299 * r + 0.587 * g + 0.114 * b) / 255;
                double contrasteBlanco = Math.Abs(1.0 - luminosidad);
                if (luminosidad >= 0.5 && luminosidad <= 0.8 && contrasteBlanco >= 0.5)
                    return (r, g, b);
            }
        }

        // Versión sincrónica para compatibilidad (usa la primera fuente URL)
        public static byte[] GenerarAvatar(string iniciales)
        {
            return GenerarAvatarAsync(iniciales).GetAwaiter().GetResult();
        }

        public static async Task<string> CrearYGuardarAvatarEnAzureAsync(string nombreUsuario, ServiceStorageBlobs storageService, string containerName = "zuvopetimagenes")
        {
            string iniciales = GetIniciales(nombreUsuario);
            byte[] imagenAvatar = await GenerarAvatarAsync(iniciales); // Usar versión async
            string nombreAvatar = $"{Guid.NewGuid()}.png";

            using var stream = new MemoryStream(imagenAvatar);
            await storageService.UploadBlobAsync(containerName, nombreAvatar, stream);

            return nombreAvatar;
        }
    }
}