using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ZuvoPetApiAWS.Helpers;
using ZuvoPetApiAWS.Repositories;
using ZuvoPetNuget.Models;
using ZuvoPetNuget.Dtos;
using ZuvoPetApiAWS.Services;
using Azure.Storage.Blobs;
using System.Security.Claims;

namespace ZuvoPetApiAWS.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "Refugio")]
    public class RefugioController : ControllerBase
    {
        private IRepositoryZuvoPet repo;
        private HelperUsuarioToken helper;
        private ServiceStorageBlobs storageService;
        public RefugioController(IRepositoryZuvoPet repo, HelperUsuarioToken helper, ServiceStorageBlobs storageService)
        {
            this.repo = repo;
            this.helper = helper;
            this.storageService = storageService;
        }

        [HttpDelete("DeleteFotoMascota")]
        public async Task<IActionResult> DeleteFotoMascota([FromQuery] string nombreFoto)
        {
            try
            {
                if (string.IsNullOrEmpty(nombreFoto))
                {
                    return BadRequest(new { mensaje = "Nombre de archivo no proporcionado" });
                }

                string containerName = "zuvopetimagenes";

                try
                {
                    // Llamar al método sin capturar un resultado booleano
                    await this.storageService.DeleteBlobAsync(containerName, nombreFoto);

                    // Si llegamos aquí, asumimos que la operación fue exitosa
                    return Ok(new
                    {
                        mensaje = "Foto de mascota eliminada correctamente"
                    });
                }
                catch (Exception storageEx)
                {
                    return StatusCode(500, new { mensaje = "Error al eliminar la imagen del almacenamiento: " + storageEx.Message });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al eliminar la foto de mascota: " + ex.Message });
            }
        }

        [HttpPost("PostFotoMascota")]
        public async Task<IActionResult> PostFotoMascota(IFormFile archivo, [FromQuery] int idMascota)
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

                // Verificar que la mascota existe y pertenece al refugio del usuario
                var mascota = await repo.GetMascotaByIdAsync(idMascota);
                if (mascota == null)
                {
                    return NotFound(new { mensaje = "Mascota no encontrada" });
                }

                var refugio = await repo.GetRefugioByUsuarioIdAsync(idUsuario);
                if (refugio == null || mascota.IdRefugio != refugio.Id)
                {
                    return Forbid();
                }

                // Nombre actual de la foto
                string oldFotoName = mascota.Foto;

                // Generar un nuevo nombre único para el archivo
                string newFotoName = $"{Guid.NewGuid()}{extension}";
                string containerName = "zuvopetimagenes";

                // Procesar y subir archivo
                using (Stream stream = archivo.OpenReadStream())
                {
                    // Actualizar el blob, eliminando el anterior
                    await this.storageService.UpdateBlobAsync(containerName, oldFotoName, newFotoName, stream);
                }

                // Actualizar la referencia en la base de datos
                bool updated = await this.repo.ActualizarFotoMascota(idMascota, newFotoName);
                if (!updated)
                {
                    return StatusCode(500, new { mensaje = "Error al actualizar la referencia en la base de datos" });
                }

                // Obtener la URL del nuevo blob
                BlobContainerClient containerClient = this.storageService.client.GetBlobContainerClient(containerName);
                BlobClient blobClient = containerClient.GetBlobClient(newFotoName);

                return Ok(new
                {
                    mensaje = "Foto de mascota actualizada correctamente",
                    fotoUrl = blobClient.Uri.AbsoluteUri,
                    nombreFoto = newFotoName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { mensaje = "Error al actualizar la foto de mascota: " + ex.Message });
            }
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

                // Validar tipo de archivo
                string extension = Path.GetExtension(archivo.FileName).ToLowerInvariant();
                if (extension != ".jpg" && extension != ".jpeg" && extension != ".png")
                {
                    return BadRequest(new { mensaje = "Solo se permiten archivos JPG, JPEG o PNG" });
                }

                // Generar un nuevo nombre único para el archivo
                string blobName = $"{Guid.NewGuid()}{extension}";
                string containerName = "zuvopetimagenes";

                // Procesar y subir archivo
                using (Stream stream = archivo.OpenReadStream())
                {
                    await this.storageService.UploadBlobAsync(containerName, blobName, stream);
                }

                // Obtener la URL del nuevo blob
                BlobContainerClient containerClient = this.storageService.client.GetBlobContainerClient(containerName);
                BlobClient blobClient = containerClient.GetBlobClient(blobName);

                return Ok(new
                {
                    mensaje = "Imagen subida correctamente",
                    fotoUrl = blobClient.Uri.AbsoluteUri,
                    nombreFoto = blobName
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
                var refugio = await repo.GetRefugioByUsuarioIdAsync(idUsuario);

                if (refugio == null)
                {
                    return NotFound(new { mensaje = "Refugio no encontrado" });
                }

                // Nombre actual de la foto
                string oldFotoName = refugio.Usuario.PerfilUsuario.FotoPerfil;

                // Generar un nuevo nombre único para el archivo
                string newFotoName = $"{Guid.NewGuid()}{extension}";
                string containerName = "zuvopetimagenes";

                // Procesar y subir archivo
                using (Stream stream = archivo.OpenReadStream())
                {
                    // Actualizar el blob, eliminando el anterior
                    await this.storageService.UpdateBlobAsync(containerName, oldFotoName, newFotoName, stream);
                }

                // Actualizar la referencia en la base de datos
                await this.repo.ActualizarFotoPerfilAsync(idUsuario, newFotoName);


                // Obtener la URL del nuevo blob
                BlobContainerClient containerClient = this.storageService.client.GetBlobContainerClient(containerName);
                BlobClient blobClient = containerClient.GetBlobClient(newFotoName);

                return Ok(new
                {
                    mensaje = "Foto de perfil actualizada correctamente",
                    fotoUrl = blobClient.Uri.AbsoluteUri,
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
                var refugio = await repo.GetRefugioByUsuarioIdAsync(idUsuario);

                if (refugio != null && !string.IsNullOrEmpty(refugio.Usuario.PerfilUsuario.FotoPerfil))
                {
                    // No devuelves solo el nombre, sino la URL completa
                    string containerName = "zuvopetimagenes";
                    BlobContainerClient containerClient =
                        this.storageService.client.GetBlobContainerClient(containerName);
                    BlobClient blobClient = containerClient.GetBlobClient(refugio.Usuario.PerfilUsuario.FotoPerfil);

                    // Devolver la URL directa del blob
                    return Ok(blobClient.Uri.AbsoluteUri);
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
                // Obtener el cliente del contenedor
                string containerName = "zuvopetimagenes";
                BlobContainerClient containerClient =
                    this.storageService.client.GetBlobContainerClient(containerName);

                // Obtener el cliente del blob
                BlobClient blobClient = containerClient.GetBlobClient(nombreImagen);

                // Verificar si el blob existe
                if (!await blobClient.ExistsAsync())
                {
                    return NotFound($"Imagen {nombreImagen} no encontrada");
                }

                // Descargar el blob
                var response = await blobClient.DownloadAsync();
                Stream stream = response.Value.Content;

                // Determinar el tipo MIME según la extensión del archivo
                string contentType = GetContentType(nombreImagen);

                // Devolver la imagen con el tipo MIME apropiado
                return File(stream, contentType);
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

        [HttpGet("ObtenerRefugioByUsuarioId")]
        public async Task<ActionResult<Refugio>>
        GetRefugioByUsuarioId()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetRefugioByUsuarioIdAsync(idUsuario);
        }

        [HttpGet("ObtenerMascotasByRefugioId/{idrefugio}")]
        public async Task<ActionResult<IEnumerable<Mascota>>>
        GetMascotasByRefugioId(int idrefugio)
        {
            var mascotas = await this.repo.GetMascotasByRefugioIdAsync(idrefugio);
            return Ok(mascotas);
        }

        [HttpGet("ObtenerSolicitudesByEstadoAndRefugio")]
        public async Task<ActionResult<int>>
        GetSolicitudesByEstadoAndRefugio([FromQuery] SolicitudRefugioDTO solicitud)
        {
            return await this.repo.GetSolicitudesByEstadoAndRefugioAsync(solicitud.IdRefugio, solicitud.Estado);
        }

        [HttpGet("ObtenerMascotasRefugio")]
        public async Task<ActionResult<List<MascotaCard>>>
        GetObtenerMascotasRefugio()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ObtenerMascotasRefugioAsync(idUsuario);
        }

        [HttpGet("ObtenerRefugio")]
        public async Task<ActionResult<Refugio>> GetObtenerRefugio()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetRefugio(idUsuario);
        }

        [HttpPost("CrearMascotaRefugio")]
        public async Task<ActionResult<bool>> CrearMascotaRefugio([FromBody] Mascota mascota)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            var resultado = await this.repo.CrearMascotaRefugioAsync(mascota, idUsuario);
            return Ok(resultado);
        }

        [HttpGet("ObtenerMascotaById/{idmascota}")]
        public async Task<ActionResult<Mascota>>
        GetMascotaById(int idmascota)
        {
            return await this.repo.GetMascotaByIdAsync(idmascota);
        }

        [HttpPut("UpdateMascota")]
        public async Task<ActionResult<bool>> 
        UpdateMascota([FromBody] Mascota mascota)
        {
            var resultado = await this.repo.UpdateMascotaAsync(mascota);
            return Ok(resultado);
        }

        [HttpDelete("DeleteMascota/{idmascota}")]
        public async Task<ActionResult<bool>> 
        DeleteMascota(int idmascota)
        {
            return await this.repo.DeleteMascotaAsync(idmascota);
        }

        [HttpGet("ObtenerSolicitudesRefugio")]
        public async Task<ActionResult<List<SolicitudAdopcion>>>
        GetSolicitudesRefugio()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetSolicitudesRefugioAsync(idUsuario);
        }

        [HttpGet("ObtenerSolicitudesById/{idsolicitud}")]
        public async Task<ActionResult<SolicitudAdopcion>>
        GetSolicitudesById(int idsolicitud)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetSolicitudByIdAsync(idUsuario, idsolicitud);
        }

        [HttpPut("ProcesarSolicitudAdopcion")]
        public async Task<ActionResult<bool>>
        ProcesarSolicitudAdopcion([FromBody] SolicitudAdopcionDTO solicitud)
        {
            return await this.repo.ProcesarSolicitudAdopcionAsync(solicitud.IdSolicitud, solicitud.NuevoEstado);
        }

        [HttpGet("ObtenerDetallesMascota/{idmascota}")]
        public async Task<ActionResult<Mascota>>
        GetDetallesMascota(int idmascota)
        {
            return await this.repo.GetDetallesMascotaAsync(idmascota);
        }

        [HttpGet("ObtenerHistoriasExito")]
        public async Task<ActionResult<List<HistoriaExito>>>
        GetHistoriasExito()
        {
            return await this.repo.ObtenerHistoriasExitoAsync();
        }

        [HttpGet("ObtenerLikeHistoria/{idhistoria}")]
        public async Task<ActionResult<List<LikeHistoria>>>
        GetLikeHistoria(int idhistoria)
        {
            return await this.repo.ObtenerLikeHistoriaAsync(idhistoria);
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
        DeleteLikeHistoria(int idhistoria)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.EliminarLikeHistoriaAsync(idhistoria, idUsuario);
        }

        [HttpPost("CrearLikeHistoria")]
        public async Task<ActionResult<bool>>
        CrearLikeHistoria([FromBody] LikeHistoriaDTO like)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.CrearLikeHistoriaAsync(like.IdHistoria, idUsuario, like.TipoReaccion);
        }

        [HttpPut("ActualizarLikeHistoria")]
        public async Task<ActionResult<bool>>
        ActualizarLikeHistoria([FromBody] LikeHistoriaDTO like)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarLikeHistoriaAsync(like.IdHistoria, idUsuario, like.TipoReaccion);
        }

        [HttpGet("ObtenerContadoresReacciones/{idhistoria}")]
        public async Task<ActionResult<Dictionary<string, int>>>
        GetContadoresReacciones(int idhistoria)
        {
            return await this.repo.ObtenerContadoresReaccionesAsync(idhistoria);
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

        [HttpGet("ObtenerNotificacionesNuevasDesde")]
        public async Task<ActionResult<bool>>
        GetNotificacionesNuevasDesde([FromQuery] DateTime desde)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.HayNotificacionesNuevasDesdeAsync(idUsuario, desde);
        }

        [HttpPut("ActualizarNotificacionComoLeida/{idnotificacion}")]
        public async Task<ActionResult<bool>>
        ActualizarNotificacionComoLeida(int idnotificacion)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.MarcarNotificacionComoLeidaAsync(idnotificacion, idUsuario);
        }

        [HttpPut("ActualizarTodasNotificacionesComoLeidas")]
        public async Task<ActionResult<bool>>
        ActualizarTodasNotificacionesComoLeidas()
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

        [HttpGet("ObtenerPerfilRefugio")]
        public async Task<ActionResult<VistaPerfilRefugio>>
        GetPerfilRefugio()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetPerfilRefugio(idUsuario);
        }

        [HttpGet("ObtenerFotoPerfil")]
        public async Task<ActionResult<string>>
        GetFotoPerfil()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetFotoPerfilAsync(idUsuario);
        }

        [HttpGet("ObtenerConversacionesRefugio")]
        public async Task<ActionResult<List<ConversacionViewModel>>>
        GetConversacionesRefugio()
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.GetConversacionesRefugioAsync(idUsuario);
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

        [HttpPut("ActualizarDescripcionRefugio")]
        public async Task<ActionResult<bool>>
        ActualizarDescripcionRefugio([FromBody] string descripcion)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarDescripcionAsync(idUsuario, descripcion);
        }

        [HttpPut("ActualizarDetallesRefugio")]
        public async Task<ActionResult<bool>> 
        ActualizarDetallesRefugio([FromBody] DetallesRefugioDTO datos)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarDetallesRefugioAsync(idUsuario, datos.Contacto, datos.CantidadAnimales, datos.CapacidadMaxima);
        }

        [HttpPut("ActualizarUbicacionRefugio")]
        public async Task<ActionResult<bool>>
        ActualizarUbicacionRefugio([FromBody] UbicacionRefugioDTO datos)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarUbicacionRefugioAsync(idUsuario, datos.Latitud, datos.Longitud);
        }

        [HttpPut("ActualizarPerfilRefugio")]
        public async Task<ActionResult<bool>>
        ActualizarPerfilRefugio([FromBody] PerfilRefugioDTO datos)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarPerfilRefugioAsync(idUsuario, datos.Email, datos.NombreRefugio, datos.ContactoRefugio);
        }

        [HttpPut("ActualizarFotoPerfil")]
        public async Task<ActionResult<string>>
        ActualizarFotoPerfil([FromBody] FotoPerfilDTO datos)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.ActualizarFotoPerfilAsync(idUsuario, datos.NombreArchivo);
        }

        [HttpPut("ActualizarMensajesComoLeidos/{idotrousuario}")]
        public async Task<ActionResult<bool>>
        ActualizarMensajesComoLeidos(int idotrousuario)
        {
            int idUsuario = this.helper.GetAuthenticatedUserId();
            return await this.repo.MarcarMensajesComoLeidosAsync(idUsuario, idotrousuario);
        }

        [HttpGet("ObtenerAdoptanteByUsuarioId/{idusuarioadoptante}")]
        public async Task<ActionResult<Adoptante>>
        GetAdoptanteChatByUsuarioid(int idusuarioadoptante)
        {
            return await this.repo.GetAdoptanteChatByUsuarioId(idusuarioadoptante);
        }
    }
}
