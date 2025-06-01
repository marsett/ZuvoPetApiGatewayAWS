using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ZuvoPetApiGatewayAWS.Helpers;
using ZuvoPetApiAWS.Repositories;
using ZuvoPetNugetAWS.Models;
using ZuvoPetNugetAWS.Dtos;
using ZuvoPetApiGatewayAWS.Services;
using Amazon.S3;
using Amazon.S3.Model;

namespace ZuvoPetApiAWS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private IRepositoryZuvoPet repo;
        private HelperActionServicesOAuth helper;
        private ServiceStorageS3 service;

        public AuthController(IRepositoryZuvoPet repo, HelperActionServicesOAuth helper, ServiceStorageS3 service)
        {
            this.repo = repo;
            this.helper = helper;
            this.service = service;
        }

        [HttpGet("imagen/{nombreImagen}")]
        public async Task<IActionResult> GetImagen(string nombreImagen)
        {
            try
            {
                // Get S3 client from DI container since ServiceStorageS3 might not expose it
                var s3Client = HttpContext.RequestServices.GetRequiredService<IAmazonS3>();
                string bucketName = "bucket-zuvopet"; // Make sure this matches your bucket

                // Create a request to get the object
                var request = new GetObjectRequest
                {
                    BucketName = bucketName,
                    Key = nombreImagen
                };

                try
                {
                    // Get the object from S3
                    var response = await s3Client.GetObjectAsync(request);

                    // Get the stream containing the object data
                    Stream stream = response.ResponseStream;

                    // Determine the MIME type based on the file extension
                    string contentType = !string.IsNullOrEmpty(response.Headers.ContentType)
                        ? response.Headers.ContentType
                        : GetContentType(nombreImagen);

                    // Return the file with the appropriate MIME type
                    return File(stream, contentType);
                }
                catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    // Object does not exist
                    return NotFound($"Imagen {nombreImagen} no encontrada");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al recuperar la imagen: " + ex.Message });
            }
        }

        private string GetContentType(string fileName)
        {
            // Obtener la extensión del archivo
            string extension = Path.GetExtension(fileName).ToLowerInvariant();

            // Asignar la extensión al tipo MIME correspondiente
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".bmp" => "image/bmp",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream" // Tipo genérico para archivos desconocidos
            };
        }

        [HttpGet("ValidateUser")]
        public async Task<IActionResult>
        ValidateUser(string username, string email)
        {
            bool exists = await this.repo.UserExistsAsync(username, email);
            return Ok(new { exists });
        }

        [HttpGet("ObtenerHistoriasExitoLanding")]
        public async Task<ActionResult<List<HistoriaExitoLandingDTO>>>
        GetHistoriasExitoLanding()
        {
            List<HistoriaExito> historiasExito = await this.repo.ObtenerHistoriasExitoAsync();
            var historiasLimitadas = historiasExito
                .OrderBy(historias => historias.FechaPublicacion)
                .Take(3)
                .Select(h => new HistoriaExitoLandingDTO
                {
                    Titulo = h.Titulo,
                    Descripcion = h.Descripcion,
                    Foto = h.Foto
                })
                .ToList();

            return historiasLimitadas;
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            Usuario usuario = await this.repo.LogInUserAsync(model.NombreUsuario, model.Contrasena);
            if (usuario == null)
            {
                return Unauthorized(new { mensaje = "Credenciales incorrectas" });
            }
            else
            {
                // Obtener la foto de perfil del usuario
                string fotoPerfil = await this.repo.GetFotoPerfilAsync(usuario.Id);

                // Crear credenciales para el token
                SigningCredentials credentials = new SigningCredentials(
                    this.helper.GetKeyToken(),
                    SecurityAlgorithms.HmacSha256);

                // Crear el modelo de usuario para el token
                UsuarioTokenDTO modelUsuario = new UsuarioTokenDTO
                {
                    IdUsuario = usuario.Id,
                    NombreUsuario = usuario.NombreUsuario,
                    Role = usuario.TipoUsuario
                    // Otros campos que necesites
                };

                // Convertir a JSON y cifrar los datos
                string jsonUsuario = JsonConvert.SerializeObject(modelUsuario);
                string jsonCifrado = HelperCriptography.EncryptString(jsonUsuario);

                // Crear claims para el token
                List<Claim> claims = new List<Claim>
                    {
                        new Claim("UserData", jsonCifrado),
                        new Claim(ClaimTypes.Name, usuario.NombreUsuario),
                        new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                        new Claim(ClaimTypes.Role, usuario.TipoUsuario)
                    };

                // Generar el token JWT
                JwtSecurityToken token = new JwtSecurityToken(
                    issuer: this.helper.Issuer,
                    audience: this.helper.Audience,
                    claims: claims,
                    signingCredentials: credentials,
                    expires: DateTime.UtcNow.AddHours(2),
                    notBefore: DateTime.UtcNow);

                // Devolver el token junto con información básica del usuario
                return Ok(new
                {
                    response = new JwtSecurityTokenHandler().WriteToken(token)
                });
            }
        }

        [HttpPost("[action]")]
        public async Task<IActionResult> Registro([FromBody] RegistroDTO modelo)
        {
            // Validar que el tipo de usuario sea válido
            if (modelo.TipoUsuario != "Adoptante" && modelo.TipoUsuario != "Refugio")
            {
                return BadRequest(new { mensaje = "El tipo de usuario debe ser 'Adoptante' o 'Refugio'." });
            }
            // Validar fortaleza de la contraseña
            var passwordRegex = new System.Text.RegularExpressions.Regex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^\da-zA-Z]).{8,}$");
            if (!passwordRegex.IsMatch(modelo.ContrasenaLimpia))
            {
                return BadRequest(new { mensaje = "La contraseña debe tener al menos 8 caracteres, incluir mayúsculas, minúsculas, números y al menos un carácter especial." });
            }

            // Validar formato de correo electrónico
            var emailRegex = new System.Text.RegularExpressions.Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
            if (string.IsNullOrEmpty(modelo.Email) || !emailRegex.IsMatch(modelo.Email))
            {
                return BadRequest(new { mensaje = "Debe proporcionar una dirección de correo electrónico válida." });
            }

            // Verificar si el usuario o email ya existe
            if (await repo.UserExistsAsync(modelo.NombreUsuario, modelo.Email))
            {
                return BadRequest(new { mensaje = "El nombre de usuario o el correo ya están en uso." });
            }

            try
            {
                // Registrar el usuario
                int? userId = await this.repo.RegisterUserAsync(
                    modelo.NombreUsuario,
                    modelo.Email,
                    modelo.ContrasenaLimpia,
                    modelo.TipoUsuario);

                if (userId == null)
                {
                    return BadRequest(new { mensaje = "No se pudo crear el usuario" });
                }

                // Crear perfil con avatar usando AWS S3
                string nombreAvatar = await CrearYGuardarAvatarEnS3Async(modelo.NombreUsuario);
                await this.repo.RegisterPerfilUserAsync(userId.Value, nombreAvatar);

                // Procesar información adicional según el tipo de usuario
                if (modelo.TipoUsuario == "Adoptante" && modelo.DatosAdoptante != null)
                {
                    await this.repo.RegisterAdoptanteAsync(
                        userId.Value,
                        modelo.DatosAdoptante.Nombre,
                        modelo.DatosAdoptante.TipoVivienda,
                        modelo.DatosAdoptante.TieneJardin,
                        modelo.DatosAdoptante.OtrosAnimales,
                        modelo.DatosAdoptante.RecursosDisponibles,
                        modelo.DatosAdoptante.TiempoEnCasa);
                }
                else if (modelo.TipoUsuario == "Refugio" && modelo.DatosRefugio != null)
                {
                    // Procesar coordenadas
                    double latitud = 0, longitud = 0;

                    if (!string.IsNullOrEmpty(modelo.DatosRefugio.LatitudStr))
                    {
                        double.TryParse(modelo.DatosRefugio.LatitudStr,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out latitud);
                    }

                    if (!string.IsNullOrEmpty(modelo.DatosRefugio.LongitudStr))
                    {
                        double.TryParse(modelo.DatosRefugio.LongitudStr,
                            System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out longitud);
                    }

                    await this.repo.RegisterRefugioAsync(
                        userId.Value,
                        modelo.DatosRefugio.NombreRefugio,
                        modelo.DatosRefugio.ContactoRefugio,
                        modelo.DatosRefugio.CantidadAnimales,
                        modelo.DatosRefugio.CapacidadMaxima,
                        latitud,
                        longitud);
                }

                return Ok(new
                {
                    mensaje = "Usuario registrado correctamente",
                    idUsuario = userId.Value,
                    tipoUsuario = modelo.TipoUsuario
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al completar el registro: " + ex.Message });
            }
        }

        // Helper method to create avatar and save to S3
        private async Task<string> CrearYGuardarAvatarEnS3Async(string nombreUsuario)
        {
            string iniciales = HelperAvatarDinamico.GetIniciales(nombreUsuario);
            byte[] imagenAvatar = HelperAvatarDinamico.GenerarAvatar(iniciales);

            string nombreAvatar = $"{Guid.NewGuid()}.png";
            using (MemoryStream stream = new MemoryStream(imagenAvatar))
            {
                bool uploaded = await this.service.UploadFileAsync(nombreAvatar, stream);
                if (!uploaded)
                {
                    throw new Exception("Error al subir el avatar a S3");
                }
            }
            return nombreAvatar;
        }



    }
}
