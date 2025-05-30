using ZuvoPetNugetAWS.Models;

namespace ZuvoPetApiAWS.Repositories
{
    public interface IRepositoryZuvoPet
    {
        Task<List<MascotaCard>> ObtenerMascotasDestacadasAsync();
        Task<bool> UserExistsAsync(string username, string email);
        Task<List<HistoriaExito>> ObtenerHistoriasExitoAsync();
        //Task<List<ComentarioHistoria>> ObtenerComentariosHistoriaAsync(int idHistoria);
        Task<List<LikeHistoria>> ObtenerLikeHistoriaAsync(int idHistoria);
        Task<int?> RegisterUserAsync(string nombreusuario, string email, string contrasenalimpia, string tipousuario);
        Task RegisterAdoptanteAsync(int idusuario, string nombre, string tipoVivienda, bool tieneJardin, bool otrosAnimales, List<string> recursosDisponibles, string tiempoEnCasa);
        Task RegisterRefugioAsync(int idusuario, string nombrerefugio, string contactorefugio, int cantidadanimales, int capacidadmaxima, double latitud, double longitud);
        Task<Usuario> LogInUserAsync(string nombreUsuario, string contrasena);
        Task RegisterPerfilUserAsync(int idusuario, string fotoperfil);
        Task<string> GetFotoPerfilAsync(int usuarioId);
        Task<VistaPerfilAdoptante> GetPerfilAdoptante(int idusuario);
        Task<VistaPerfilRefugio> GetPerfilRefugio(int idusuario);
        Task<List<MascotaCard>> ObtenerMascotasAsync();
        Task<bool> EsFavorito(int idusuario, int idmascota);
        Task<bool> InsertMascotaFavorita(int idusuario, int idmascota);
        Task<bool> EliminarFavorito(int idusuario, int idmascota);
        Task<List<MascotaCard>> ObtenerMascotasFavoritas(int idadoptante);
        Task<DateTime?> ObtenerUltimaAccionFavorito(int idusuario, int idmascota);
        Task GuardarUltimaAccionFavorito(int idusuario, int idmascota, DateTime fecha);
        Task<Mascota> GetDetallesMascotaAsync(int idmascota);


        Task<SolicitudAdopcion> CrearSolicitudAdopcionAsync(int idusuario, int idmascota);
        Task<int?> IdRefugioPorMascotaAsync(int idMascota);
        Task<bool> CrearNotificacionAsync(int idSolicitud, int idRefugio, string nombreMascota);
        Task<string> GetNombreMascotaAsync(int idMascota);

        Task<bool> ExisteSolicitudAdopcionAsync(int idusuario, int idmascota);
        Task<LikeHistoria> ObtenerLikeUsuarioHistoriaAsync(int idHistoria, int idusuario);
        Task<bool> CrearLikeHistoriaAsync(int idHistoria, int idusuario, string tipoReaccion);
        Task<bool> ActualizarLikeHistoriaAsync(int idHistoria, int idusuario, string tipoReaccion);
        Task<bool> EliminarLikeHistoriaAsync(int idHistoria, int idusuario);
        Task<Dictionary<string, int>> ObtenerContadoresReaccionesAsync(int idHistoria);
        Task<List<Refugio>> ObtenerRefugiosAsync();
        Task<Refugio> GetDetallesRefugioAsync(int idrefugio);
        Task<List<Mascota>> GetMascotasPorRefugioAsync(int idrefugio);

        Task<List<MascotaAdoptada>> ObtenerMascotasAdoptadas(int idusuario);

        Task<Refugio> GetRefugioByUsuarioIdAsync(int idUsuario);
        Task<int> GetSolicitudesPendientesCountAsync(int idRefugio);
        Task<IEnumerable<Mascota>> GetMascotasByRefugioIdAsync(int idRefugio);
        Task<int> GetSolicitudesByEstadoAndRefugioAsync(int idRefugio, string estado);

        Task<List<MascotaCard>> ObtenerMascotasRefugioAsync(int idrefugio);


        Task<Refugio> GetRefugio(int idusuario);
        Task<bool> CrearMascotaRefugioAsync(Mascota mascota, int idusuario);


        Task<Mascota> GetMascotaByIdAsync(int id);
        Task<bool> UpdateMascotaAsync(Mascota mascota);
        Task<bool> DeleteMascotaAsync(int id);
        Task<List<SolicitudAdopcion>> GetSolicitudesRefugioAsync(int idusuario);

        Task<bool> ProcesarSolicitudAdopcionAsync(int idSolicitud, string nuevoEstado);
        Task<SolicitudAdopcion> GetSolicitudByIdAsync(int idusuario, int solicitud);


        Task<List<Notificacion>> GetNotificacionesUsuarioAsync(int idUsuario, int pagina, int tamañoPagina);
        Task<int> GetTotalNotificacionesUsuarioAsync(int idUsuario);
        Task<int> GetTotalNotificacionesNoLeidasAsync(int idUsuario);
        Task<bool> MarcarNotificacionComoLeidaAsync(int idNotificacion, int idUsuario);
        Task<bool> MarcarTodasNotificacionesComoLeidasAsync(int idUsuario);
        Task<bool> EliminarNotificacionAsync(int idNotificacion, int idUsuario);
        Task<List<SolicitudAdopcion>> GetSolicitudesAprobadasAdoptante(int idusuario);
        Task<List<Mascota>> GetMascotasAdoptadasSinHistoria(int idusuario);
        Task<bool> CrearHistoriaExito(HistoriaExito h, int idusuario);
        Task<Adoptante> GetAdoptanteByUsuarioId(int idusuario);

        Task<bool> HayNotificacionesNuevasDesdeAsync(int idUsuario, DateTime desde);


        Task<List<Mensaje>> GetMensajesConversacionAsync(int usuarioActualId, int otroUsuarioId);
        Task<List<ConversacionViewModel>> GetConversacionesAdoptanteAsync(int usuarioId);
        Task<List<ConversacionViewModel>> GetConversacionesRefugioAsync(int usuarioId);
        Task<Mensaje> AgregarMensajeAsync(int emisorId, int receptorId, string contenido);



        Task<bool> ActualizarDescripcionAsync(int idUsuario, string descripcion);
        Task<bool> ActualizarDetallesRefugioAsync(int idUsuario, string contacto, int cantidadAnimales, int capacidadMaxima);
        Task<bool> ActualizarUbicacionRefugioAsync(int idUsuario, double latitud, double longitud);
        Task<bool> ActualizarPerfilRefugioAsync(int idUsuario, string email, string nombreRefugio, string contactoRefugio);
        Task<string> ActualizarFotoPerfilAsync(int idUsuario, string nombreArchivo);
        Task<bool> MarcarMensajesComoLeidosAsync(int usuarioActualId, int otroUsuarioId);
        Task<Adoptante> GetAdoptanteChatByUsuarioId(int idusuario);


        Task<bool> ActualizarDescripcionAdoptante(int idUsuario, string descripcion);
        Task<bool> ActualizarDetallesAdoptante(int idUsuario, VistaPerfilAdoptante modelo);
        Task<bool> ActualizarPerfilAdoptante(int idUsuario, string email, string nombre);
        Task<bool> ActualizarFotoPerfilAdoptante(int idUsuario, string fileName);
        Task<bool> IncrementarVistasMascota(int idMascota);
        Task<Adoptante> GetAdoptanteByUsuarioIdAsync(int idUsuario);
        Task<Refugio> GetRefugioChatByIdAsync(int refugioId);
        Task<Refugio> GetRefugioChatDosByIdAsync(int idusuario);


        Task<bool> ActualizarFotoMascota(int idMascota, string nuevaFoto);
    }
}
