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

        public static byte[] GenerarAvatar(string iniciales)
        {
            int ancho = 150, alto = 150;

            using var image = new Image<Rgba32>(ancho, alto);

            var colorFondo = GenerarColorAleatorio();
            var backgroundColor = SixLabors.ImageSharp.Color.FromRgb(colorFondo.R, colorFondo.G, colorFondo.B);

            image.Mutate(ctx =>
            {
                // Llenar el fondo con el color generado
                ctx.Fill(backgroundColor);

                // Crear fuente
                var font = GetFont(50);

                // Configurar opciones de texto
                var textOptions = new RichTextOptions(font)
                {
                    Origin = new SixLabors.ImageSharp.PointF(ancho / 2f, alto / 2f),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };

                // Dibujar el texto en blanco
                ctx.DrawText(textOptions, iniciales, SixLabors.ImageSharp.Color.White);
            });

            using var ms = new MemoryStream();
            image.SaveAsPng(ms);
            return ms.ToArray();
        }

        private static SixLabors.Fonts.Font GetFont(float size)
        {
            try
            {
                // Intentar usar una fuente del sistema
                var fontCollection = new SixLabors.Fonts.FontCollection();

                // Fuentes comunes en sistemas Linux/AWS
                string[] fontPaths = {
                    "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf",
                    "/usr/share/fonts/TTF/arial.ttf",
                    "/System/Library/Fonts/Helvetica.ttc"
                };

                foreach (var fontPath in fontPaths)
                {
                    if (File.Exists(fontPath))
                    {
                        var fontFamily = fontCollection.Add(fontPath);
                        return fontFamily.CreateFont(size, SixLabors.Fonts.FontStyle.Bold);
                    }
                }

                // Si no encuentra fuentes del sistema, usar la fuente por defecto
                return SystemFonts.CreateFont("Arial", size, SixLabors.Fonts.FontStyle.Bold);
            }
            catch
            {
                // Fallback a fuente por defecto del sistema
                return SystemFonts.CreateFont(SystemFonts.Families.First().Name, size, SixLabors.Fonts.FontStyle.Bold);
            }
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

        public static async Task<string> CrearYGuardarAvatarEnAzureAsync(string nombreUsuario, ServiceStorageBlobs storageService, string containerName = "zuvopetimagenes")
        {
            string iniciales = GetIniciales(nombreUsuario);
            byte[] imagenAvatar = GenerarAvatar(iniciales);
            string nombreAvatar = $"{Guid.NewGuid()}.png";

            using (MemoryStream stream = new MemoryStream(imagenAvatar))
            {
                await storageService.UploadBlobAsync(containerName, nombreAvatar, stream);
            }

            return nombreAvatar;
        }
    }
}