using SkiaSharp;
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

            using var surface = SKSurface.Create(new SKImageInfo(ancho, alto));
            var canvas = surface.Canvas;

            // Limpiar el canvas con color de fondo
            var colorFondo = GenerarColorAleatorio();
            canvas.Clear(new SKColor(colorFondo.R, colorFondo.G, colorFondo.B));

            // Configurar la fuente y el texto
            using var paint = new SKPaint
            {
                Color = SKColors.White,
                TextSize = 50,
                IsAntialias = true,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold),
                TextAlign = SKTextAlign.Center
            };

            // Calcular la posición del texto (centrado)
            var textBounds = new SKRect();
            paint.MeasureText(iniciales, ref textBounds);

            float x = ancho / 2f;
            float y = (alto / 2f) - textBounds.MidY;

            // Dibujar el texto
            canvas.DrawText(iniciales, x, y, paint);

            // Convertir a imagen y obtener bytes
            using var image = surface.Snapshot();
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);

            return data.ToArray();
        }

        public static (byte R, byte G, byte B) GenerarColorAleatorio()
        {
            Random rand = new Random();
            while (true)
            {
                // Generar un color aleatorio
                byte r = (byte)rand.Next(100, 256);
                byte g = (byte)rand.Next(100, 256);
                byte b = (byte)rand.Next(100, 256);

                // Calcular la luminosidad perceptual
                double luminosidad = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

                // Calcular el contraste con el texto blanco (#FFFFFF)
                double contrasteBlanco = Math.Abs(1.0 - luminosidad);

                // Asegurar que el color sea suficientemente brillante pero no excesivamente claro
                if (luminosidad >= 0.5 && luminosidad <= 0.8 && contrasteBlanco >= 0.5)
                    return (r, g, b);
            }
        }

        // Modificar este método para usar Azure Blob Storage
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