using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Azure.Storage.Blobs;
using ZuvoPetApiGatewayAWS.Helpers;
using ZuvoPetApiAWS.Repositories;
using ZuvoPetApiGatewayAWS.Services;
using ZuvoPetNugetAWS.Dtos;
using ZuvoPetNugetAWS.Models;

namespace ZuvoPetApiAWS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Adoptante")]
    public class AdoptanteController : ControllerBase
    {
        private IRepositoryZuvoPet repo;
        private HelperUsuarioToken helper;
        public ServiceStorageS3 service;
        public AdoptanteController(IRepositoryZuvoPet repo, HelperUsuarioToken helper, ServiceStorageS3 service)
        {
            this.repo = repo;
            this.helper = helper;
            this.service = service;
        }

        [HttpPost("SubirImagen")]
        public async Task<IActionResult> SubirImagen(IFormFile archivo)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                {
                    return BadRequest(new { mensaje = "No se ha proporcionado ningún archivo" });
                }

                // Validate file type
                string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                {
                    return BadRequest(new { mensaje = "Solo se permiten archivos JPG, JPEG o PNG" });
                }

                // Generate unique filename
                string fileName = $"{Guid.NewGuid()}{extension}";

                // Upload to S3
                using (Stream stream = archivo.OpenReadStream())
                {
                    bool uploaded = await this.service.UploadFileAsync(fileName, stream);
                    if (!uploaded)
                    {
                        return StatusCode(500, new { mensaje = "Error al subir la imagen a S3" });
                    }
                }

                // Construct the S3 URL (you may need to adjust this)
                string bucketName = "bucket-zuvopet";
                string s3BucketUrl = $"https://{bucketName}.s3.amazonaws.com/{fileName}";

                return Ok(new
                {
                    mensaje = "Imagen subida correctamente",
                    fotoUrl = s3BucketUrl,
                    nombreFoto = fileName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al subir la imagen: " + ex.Message });
            }
        }


        [HttpPost("PostFotoPerfil")]
        public async Task<IActionResult> PostFotoPerfil(IFormFile archivo)
        {
            try
            {
                if (archivo == null || archivo.Length == 0)
                {
                    return BadRequest(new { mensaje = "No se ha proporcionado ningún archivo" });
                }

                // Validar tipo de archivo
                string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                {
                    return BadRequest(new { mensaje = "Solo se permiten archivos JPG, JPEG o PNG" });
                }

                // Obtener el ID del usuario actual
                int idUsuario = int.Parse(HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var adoptante = await repo.GetAdoptanteByUsuarioIdAsync(idUsuario);

                if (adoptante == null)
                {
                    return NotFound(new { mensaje = "Adoptante no encontrado" });
                }

                // Nombre actual de la foto
                string oldFotoName = adoptante.Usuario.PerfilUsuario.FotoPerfil;

                // Generar un nuevo nombre único para el archivo
                string newFotoName = $"{Guid.NewGuid()}{extension}";

                // Procesar y subir archivo
                using (Stream stream = archivo.OpenReadStream())
                {
                    // Subir nuevo archivo a S3
                    bool uploaded = await this.service.UploadFileAsync(newFotoName, stream);
                    if (!uploaded)
                    {
                        return StatusCode(500, new { mensaje = "Error al subir la imagen a S3" });
                    }
                }

                // Eliminar archivo anterior de S3 si existe
                if (!string.IsNullOrEmpty(oldFotoName))
                {
                    await this.service.DeleteFileAsync(oldFotoName);
                }

                // Actualizar la referencia en la base de datos
                bool updated = await this.repo.ActualizarFotoPerfilAdoptante(idUsuario, newFotoName);

                if (!updated)
                {
                    return StatusCode(500, new { mensaje = "Error al actualizar la referencia en la base de datos" });
                }

                // Construir la URL de S3
                string bucketName = "bucket-zuvopet"; // Asegúrate de que coincide con tu bucket real
                string s3BucketUrl = $"https://{bucketName}.s3.amazonaws.com/{newFotoName}";

                return Ok(new
                {
                    mensaje = "Foto de perfil actualizada correctamente",
                    fotoUrl = s3BucketUrl,
                    nombreFoto = newFotoName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al actualizar la foto de perfil: " + ex.Message });
            }
        }


        [HttpGet("ObtenerFotoPerfilUrl")]
        public async Task<IActionResult> ObtenerFotoPerfilUrl()
        {
            try
            {
                // Obtienes el nombre de la foto del usuario actual
                int idUsuario = int.Parse(HttpContext.User.FindFirst(ClaimTypes.NameIdentifier).Value);
                var adoptante = await repo.GetAdoptanteByUsuarioIdAsync(idUsuario);

                if (adoptante != null && !string.IsNullOrEmpty(adoptante.Usuario.PerfilUsuario.FotoPerfil))
                {
                    // Construir la URL de S3 usando el nombre del archivo
                    string bucketName = "bucket-zuvopet"; // Asegúrate de que coincide con tu bucket real
                    string s3BucketUrl = $"https://{bucketName}.s3.amazonaws.com/{adoptante.Usuario.PerfilUsuario.FotoPerfil}";

                    // Devolver la URL directa de S3
                    return Ok(s3BucketUrl);
                }

                return Ok(string.Empty);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al obtener la URL de la imagen: " + ex.Message });
            }
        }


        [HttpGet("imagen/{nombreImagen}")]
public async Task<IActionResult> GetImagen(string nombreImagen)
{
    try
    {
        // Download the file from S3
        var fileData = await this.service.DownloadFileAsync(nombreImagen);
        
        if (fileData == null)
        {
            return NotFound($"Imagen {nombreImagen} no encontrada");
        }
        
        // Determine the MIME type based on the file extension
        // If S3 returns a content type, use it; otherwise use our GetContentType method
        string contentType = !string.IsNullOrEmpty(fileData.Value.ContentType) 
            ? fileData.Value.ContentType 
            : GetContentType(nombreImagen);
        
        // Return the file with the appropriate MIME type
        return File(fileData.Value.Stream, contentType);
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

        

        [HttpGet("ObtenerMascotasDestacadas")]
        public async Task<ActionResult<List<MascotaCard>>>
        GetMascotasDestacadas()
        {
            return await this.repo.ObtenerMascotasDestacadasAsync();
        }

        [HttpGet("ObtenerHistoriasExito")]
        public async Task<ActionResult<List<HistoriaExito>>>
        GetHistoriasExito()
        {
            return await this.repo.ObtenerHistoriasExitoAsync();
        }

        [HttpGet("ObtenerLikesHistoria/{idhistoria}")]
        public async Task<ActionResult<List<LikeHistoria>>>
        GetLikesHistoria(int idhistoria)
        {
            return await this.repo.ObtenerLikeHistoriaAsync(idhistoria);
        }

        [HttpGet("ObtenerRefugios")]
        public async Task<ActionResult<List<Refugio>>>
        GetRefugios()
        {
            return await this.repo.ObtenerRefugiosAsync();
        }

        [HttpGet("ObtenerDetallesRefugio/{idrefugio}")]
        public async Task<ActionResult<Refugio>>
        GetDetallesRefugio(int idrefugio)
        {
            return await this.repo.GetDetallesRefugioAsync(idrefugio);
        }

        [HttpGet("ObtenerMascotasRefugio/{idrefugio}")]
        public async Task<ActionResult<List<Mascota>>>
        GetMascotasRefugio(int idrefugio)
        {
            return await this.repo.GetMascotasPorRefugioAsync(idrefugio);
        }

        [HttpGet("ObtenerLikeUsuarioHistoria/{idhistoria}")]
        public async Task<ActionResult<LikeHistoria>>
        GetLikeUsuarioHistoria(int idhistoria)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ObtenerLikeUsuarioHistoriaAsync(idhistoria, idUsuario);
        }

        [HttpDelete("EliminarLikeHistoria/{idhistoria}")]
        public async Task<ActionResult<bool>>
        EliminarLikeHistoria(int idhistoria)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.EliminarLikeHistoriaAsync(idhistoria, idUsuario);
        }

        [HttpPost("CrearLikeHistoria")]
        public async Task<ActionResult<bool>>
        CrearLikeHistoria([FromBody] LikeHistoriaDTO likeDTO)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.CrearLikeHistoriaAsync(likeDTO.IdHistoria, idUsuario, likeDTO.TipoReaccion);
        }

        [HttpPut("ActualizarLikeHistoria")]
        public async Task<ActionResult<bool>>
        ActualizarLikeHistoria([FromBody] LikeHistoriaDTO likeDTO)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarLikeHistoriaAsync(likeDTO.IdHistoria, idUsuario, likeDTO.TipoReaccion);
        }

        [HttpGet("ObtenerContadoresReacciones/{idhistoria}")]
        public async Task<ActionResult<Dictionary<string, int>>>
        GetContadoresReacciones(int idhistoria)
        {
            return await this.repo.ObtenerContadoresReaccionesAsync(idhistoria);
        }

        [HttpGet("ObtenerPerfilAdoptante")]
        public async Task<ActionResult<VistaPerfilAdoptante>>
        GetPerfilAdoptante()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetPerfilAdoptante(idUsuario);
        }

        [HttpGet("ObtenerMascotasFavoritas")]
        public async Task<ActionResult<List<MascotaCard>>>
        GetMascotasFavoritas()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ObtenerMascotasFavoritas(idUsuario);
        }

        [HttpGet("ObtenerMascotasAdoptadas")]
        public async Task<ActionResult<List<MascotaAdoptada>>>
        GetMascotasAdoptadas()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ObtenerMascotasAdoptadas(idUsuario);
        }

        [HttpGet("ObtenerFotoPerfil")]
        public async Task<ActionResult<string>>
        GetFotoPerfil()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetFotoPerfilAsync(idUsuario);
        }

        [HttpGet("ObtenerMascotas")]
        public async Task<ActionResult<List<MascotaCard>>>
        GetMascotas()
        {
            return await this.repo.ObtenerMascotasAsync();
        }

        [HttpGet("ObtenerUltimaAccionFavorito/{idmascota}")]
        public async Task<ActionResult<DateTime?>>
        GetUltimaAccionFavorito(int idmascota)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ObtenerUltimaAccionFavorito(idUsuario, idmascota);
        }

        [HttpGet("ObtenerEsFavorito/{idmascota}")]
        public async Task<ActionResult<bool>>
        GetEsFavorito(int idmascota)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.EsFavorito(idUsuario, idmascota);
        }

        [HttpDelete("EliminarFavorito/{idmascota}")]
        public async Task<ActionResult<bool>>
        EliminarFavorito(int idmascota)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.EliminarFavorito(idUsuario, idmascota);
        }

        [HttpPost("CrearMascotaFavorita/{idmascota}")]
        public async Task<ActionResult<bool>>
        CrearMascotaFavorita(int idmascota)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.InsertMascotaFavorita(idUsuario, idmascota);
        }

        [HttpGet("ObtenerDetallesMascota/{idmascota}")]
        public async Task<ActionResult<Mascota>>
        GetDetallesMascota(int idmascota)
        {
            return await this.repo.GetDetallesMascotaAsync(idmascota);
        }

        [HttpGet("ObtenerExisteSolicitudAdopcion/{idmascota}")]
        public async Task<ActionResult<bool>>
        GetExisteSolicitudAdopcion(int idmascota)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ExisteSolicitudAdopcionAsync(idUsuario, idmascota);
        }

        [HttpPost("CrearSolicitudAdopcion/{idmascota}")]
        public async Task<ActionResult<SolicitudAdopcion>>
        CrearSolicitudAdopcion(int idmascota)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.CrearSolicitudAdopcionAsync(idUsuario, idmascota);
        }

        [HttpGet("ObtenerNombreMascota/{idmascota}")]
        public async Task<ActionResult<string>>
        GetNombreMascota(int idmascota)
        {
            return await this.repo.GetNombreMascotaAsync(idmascota);
        }

        [HttpGet("ObtenerIdRefugioPorMascota/{idmascota}")]
        public async Task<ActionResult<int?>>
        GetIdRefugioPorMascota(int idmascota)
        {
            return await this.repo.IdRefugioPorMascotaAsync(idmascota);
        }

        [HttpPost("CrearNotificacion")]
        public async Task<ActionResult<bool>>
        CrearNotificacion([FromBody] NotificacionCreacionDTO notificacionDTO)
        {
            return await this.repo.CrearNotificacionAsync(notificacionDTO.IdSolicitud, notificacionDTO.IdRefugio, notificacionDTO.NombreMascota);
        }

        [HttpGet("ObtenerNotificacionesUsuario")]
        public async Task<ActionResult<List<Notificacion>>>
        GetNotificacionesUsuario([FromQuery] int pagina = 1, [FromQuery] int tamanopagina = 10)
        {
            if (pagina <= 0)
            {
                return BadRequest("El número de página debe ser mayor que cero");
            }

            if (tamanopagina <= 0)
            {
                return BadRequest("El tamaño de página debe ser mayor que cero");
            }
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetNotificacionesUsuarioAsync(idUsuario, pagina, tamanopagina);
        }

        [HttpGet("ObtenerTotalNotificacionesUsuario")]
        public async Task<ActionResult<int>>
        GetTotalNotificacionesUsuario()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetTotalNotificacionesUsuarioAsync(idUsuario);
        }

        [HttpGet("ObtenerTotalNotificacionesNoLeidas")]
        public async Task<ActionResult<int>>
        GetTotalNotificacionesNoLeidas()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetTotalNotificacionesNoLeidasAsync(idUsuario);
        }

        [HttpGet("ObtenerHayNotificacionesNuevasDesde")]
        public async Task<ActionResult<bool>>
        GetHayNotificacionesNuevasDesde([FromQuery] DateTime desde)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.HayNotificacionesNuevasDesdeAsync(idUsuario, desde);
        }

        [HttpPut("ActualizarMarcarNotificacionComoLeida/{idnotificacion}")]
        public async Task<ActionResult<bool>>
        ActualizarMarcarNotificacionComoLeida(int idnotificacion)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.MarcarNotificacionComoLeidaAsync(idnotificacion, idUsuario);
        }

        [HttpPut("ActualizarMarcarTodasNotificacionesComoLeidas")]
        public async Task<ActionResult<bool>>
        ActualizarMarcarTodasNotificacionesComoLeidas()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.MarcarTodasNotificacionesComoLeidasAsync(idUsuario);
        }

        [HttpDelete("EliminarNotificacion/{idnotificacion}")]
        public async Task<ActionResult<bool>>
        EliminarNotificacion(int idnotificacion)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.EliminarNotificacionAsync(idnotificacion, idUsuario);
        }

        [HttpGet("ObtenerMascotasAdoptadasSinHistoria")]
        public async Task<ActionResult<List<Mascota>>>
        GetMascotasAdoptadasSinHistoria()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetMascotasAdoptadasSinHistoria(idUsuario);
        }

        [HttpGet("ObtenerAdoptanteByUsuarioId")]
        public async Task<ActionResult<Adoptante>>
        GetAdoptanteByUsuarioId()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetAdoptanteByUsuarioId(idUsuario);
        }

        //[HttpPost("CrearHistoriaExito/{historiaexito}")]
        //public async Task<ActionResult<bool>>
        //CrearHistoriaExito(HistoriaExito historiaexito)
        //{
        //    int idUsuario = this.helper.GetAuthenticatedUserId();
        //    return await this.repo.CrearHistoriaExito(historiaexito, idUsuario);
        //}

        [HttpPost("CrearHistoriaExito")]
        public async Task<ActionResult<bool>> 
        CrearHistoriaExito([FromBody] HistoriaExitoCreacionDTO dto)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            HistoriaExito historiaexito = new HistoriaExito
            {
                IdMascota = dto.IdMascota,
                Titulo = dto.Titulo,
                Descripcion = dto.Descripcion,
                Foto = dto.Foto,
                FechaPublicacion = DateTime.Now,
                Estado = "Aprobada"
            };
            return await this.repo.CrearHistoriaExito(historiaexito, idUsuario);
        }

        [HttpGet("ObtenerConversacionesAdoptante")]
        public async Task<ActionResult<List<ConversacionViewModel>>>
        GetConversacionesAdoptante()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetConversacionesAdoptanteAsync(idUsuario);
        }

        [HttpGet("ObtenerMensajesConversacion/{idotrousuario}")]
        public async Task<ActionResult<List<Mensaje>>>
        GetMensajesConversacion(int idotrousuario)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetMensajesConversacionAsync(idUsuario, idotrousuario);
        }

        [HttpPost("CrearMensaje")]
        public async Task<ActionResult<Mensaje>>
        CrearMensaje([FromBody] MensajeCreacionDTO mensajeDTO)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.AgregarMensajeAsync(idUsuario, mensajeDTO.IdReceptor, mensajeDTO.Contenido);
        }

        [HttpPut("ActualizarDescripcionAdoptante")]
        public async Task<ActionResult<bool>>
        ActualizarDescripcionAdoptante([FromBody] string descripcion)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarDescripcionAdoptante(idUsuario, descripcion);
        }

        [HttpPut("ActualizarDetallesAdoptante")]
        public async Task<ActionResult<bool>>
        ActualizarDetallesAdoptante([FromBody] DetallesAdoptanteUpdateDTO detallesDTO)
        {
            VistaPerfilAdoptante modelo = new VistaPerfilAdoptante
            {
                TipoVivienda = detallesDTO.TipoVivienda,
                RecursosDisponibles = detallesDTO.RecursosDisponibles,
                TiempoEnCasa = detallesDTO.TiempoEnCasa,
                TieneJardin = detallesDTO.TieneJardin,
                OtrosAnimales = detallesDTO.OtrosAnimales
            };
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarDetallesAdoptante(idUsuario, modelo);
        }

        [HttpPut("ActualizarPerfilAdoptante")]
        public async Task<ActionResult<bool>>
        ActualizarPerfilAdoptante([FromBody] PerfilAdoptanteDTO datos)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarPerfilAdoptante(idUsuario, datos.Email, datos.Nombre);
        }

        [HttpPut("ActualizarFotoPerfil")]
        public async Task<ActionResult<bool>>
        ActualizarFotoPerfil([FromBody] FotoPerfilDTO datos)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarFotoPerfilAdoptante(idUsuario, datos.NombreArchivo);
        }

        [HttpPut("ActualizarVistasMascota/{idmascota}")]
        public async Task<ActionResult<bool>>
        IncrementarVistasMascota(int idmascota)
        {
            return await this.repo.IncrementarVistasMascota(idmascota);
        }

        [HttpPut("ActualizarMensajesComoLeidos/{idotrousuario}")]
        public async Task<ActionResult<bool>>
        ActualizarMensajesComoLeidos(int idotrousuario)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.MarcarMensajesComoLeidosAsync(idUsuario, idotrousuario);
        }

        [HttpGet("ObtenerAdoptanteByUsuarioIdAsync")]
        public async Task<ActionResult<Adoptante>>
        GetAdoptanteByUsuarioIdAsync()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetAdoptanteByUsuarioIdAsync(idUsuario);
        }

        [HttpGet("ObtenerRefugioChatById/{idrefugio}")]
        public async Task<ActionResult<Refugio>>
        GetRefugioChatById(int idrefugio)
        {
            return await this.repo.GetRefugioChatByIdAsync(idrefugio);
        }

        [HttpGet("ObtenerRefugioChatDosById/{idusuariorefugio}")]
        public async Task<ActionResult<Refugio>>
        GetRefugioChatDosById(int idusuariorefugio)
        {
            return await this.repo.GetRefugioChatDosByIdAsync(idusuariorefugio);
        }
    }
}
