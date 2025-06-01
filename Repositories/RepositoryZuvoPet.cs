using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.Security.Claims;
using ZuvoPetApiGatewayAWS.Data;
using ZuvoPetApiGatewayAWS.Helpers;
using ZuvoPetApiGatewayAWS.Data;
using ZuvoPetNugetAWS.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
namespace ZuvoPetApiAWS.Repositories
{
    public class RepositoryZuvoPet : IRepositoryZuvoPet
    {
        private readonly ZuvoPetContext context;
        public RepositoryZuvoPet(ZuvoPetContext context)
        {
            this.context = context;
        }
        public async Task<List<MascotaCard>> ObtenerMascotasDestacadasAsync()
        {
            List<MascotaCard> mascotasDestacadas = await this.context.Mascotas
                .Where(m => m.Estado == "Disponible")
                .OrderByDescending(m => m.Vistas)
                .ThenBy(m => m.FechaNacimiento)
                .Select(m => new MascotaCard
                {
                    Id = m.Id,
                    Nombre = m.Nombre,
                    Especie = m.Especie,
                    Tamano = m.Tamano,
                    Sexo = m.Sexo,
                    Foto = m.Foto,
                    Edad = CalcularEdadEnMeses(m.FechaNacimiento) // Llamar al método estático
                })
                .Take(6)
                .ToListAsync();

            return mascotasDestacadas;
        }

        public async Task<bool> UserExistsAsync(string username, string email)
        {
            // Verifica si existe algún usuario con el mismo nombre o email
            return await this.context.Usuarios.AnyAsync(u =>
                u.NombreUsuario.ToLower() == username.ToLower() ||
                u.Email.ToLower() == email.ToLower());
        }

        public async Task<List<MascotaCard>> ObtenerMascotasAsync()
        {
            List<MascotaCard> mascotas = await this.context.Mascotas
                .Where(m => m.Estado == "Disponible")
                .Select(m => new MascotaCard
                {
                    Id = m.Id,
                    Nombre = m.Nombre,
                    Especie = m.Especie,
                    Tamano = m.Tamano,
                    Sexo = m.Sexo,
                    Foto = m.Foto,
                    //Edad = CalcularEdad(m.FechaNacimiento)
                    Edad = CalcularEdadEnMeses(m.FechaNacimiento)
                })
                .ToListAsync();

            return mascotas;
        }

        public async Task<List<MascotaCard>> ObtenerMascotasRefugioAsync(int idusuario)
        {
            var refugio = await this.context.Refugios
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
            List<MascotaCard> mascotas = await this.context.Mascotas
                .Where(m => m.IdRefugio == refugio.Id)
                .Select(m => new MascotaCard
                {
                    Id = m.Id,
                    Nombre = m.Nombre,
                    Especie = m.Especie,
                    Tamano = m.Tamano,
                    Sexo = m.Sexo,
                    Foto = m.Foto,
                    Edad = CalcularEdadEnMeses(m.FechaNacimiento)
                })
                .ToListAsync();

            return mascotas;
        }

        public async Task<List<Refugio>> ObtenerRefugiosAsync()
        {
            List<Refugio> refugios = await this.context.Refugios
                .Include(l => l.Usuario.PerfilUsuario)
                .ToListAsync();

            return refugios;
        }

        private static int CalcularEdadEnMeses(DateTime fechaNacimiento)
        {
            var fechaActual = DateTime.Now;
            var edadEnAnios = fechaActual.Year - fechaNacimiento.Year;

            // Esta condición es correcta
            if (fechaActual.Month < fechaNacimiento.Month ||
                (fechaActual.Month == fechaNacimiento.Month && fechaActual.Day < fechaNacimiento.Day))
            {
                edadEnAnios--;
            }

            // Calcula meses como en el primer algoritmo
            var meses = fechaActual.Month - fechaNacimiento.Month;
            if (fechaActual.Day < fechaNacimiento.Day)
            {
                meses--;
            }
            if (meses < 0)
            {
                meses += 12;
            }

            // Retorna los meses totales
            return edadEnAnios * 12 + meses;
        }

        public async Task<List<HistoriaExito>> ObtenerHistoriasExitoAsync()
        {
            List<HistoriaExito> historiasExito = await this.context.HistoriasExito
                .Where(m => m.Estado == "Aprobada")
                .Include(s => s.Adoptante.Usuario)
                .OrderByDescending(m => m.FechaPublicacion)
                .ToListAsync();

            return historiasExito;
        }

        public async Task<List<SolicitudAdopcion>> GetSolicitudesAprobadasAdoptante(int idusuario)
        {
            var adoptante = await this.context.Adoptantes
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
            List<SolicitudAdopcion> solicitudesAdoptante = await this.context.SolicitudesAdopcion
                .Where(m => m.IdAdoptante == adoptante.Id && m.Estado == "Aprobada")
                .ToListAsync();
            return solicitudesAdoptante;
        }

        public async Task<List<Mascota>> GetMascotasAdoptadasSinHistoria(int idusuario)
        {
            // Obtener el adoptante por su idUsuario
            var adoptante = await this.context.Adoptantes
                .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);

            // Obtener las solicitudes aprobadas para este adoptante
            var solicitudesAprobadas = await this.context.SolicitudesAdopcion
                .Where(s => s.IdAdoptante == adoptante.Id && s.Estado == "Aprobada")
                .ToListAsync();

            // Encontrar las mascotas que ya tienen historia de éxito con este adoptante
            var mascotasConHistoria = await this.context.HistoriasExito
                .Where(h => h.IdAdoptante == adoptante.Id)
                .Select(h => h.IdMascota)
                .ToListAsync();

            // Obtener las mascotas de las solicitudes aprobadas que NO tienen historia de éxito
            var idsMascotasSinHistoria = solicitudesAprobadas
                .Select(s => s.IdMascota)
                .Where(id => !mascotasConHistoria.Contains(id))
                .ToList();

            // Obtener los datos completos de las mascotas
            var mascotasSinHistoria = await this.context.Mascotas
                .Where(m => idsMascotasSinHistoria.Contains(m.Id))
                .ToListAsync();

            return mascotasSinHistoria;
        }

        public async Task<bool> CrearHistoriaExito(HistoriaExito h, int idusuario)
        {
            var adoptante = await this.context.Adoptantes
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);

            HistoriaExito historia = new HistoriaExito()
            {
                Id = await GetMaxIdAsync(this.context.HistoriasExito),
                IdAdoptante = adoptante.Id,
                IdMascota = h.IdMascota,
                Titulo = h.Titulo,
                Descripcion = h.Descripcion,
                Foto = h.Foto,
                FechaPublicacion = DateTime.Now,
                Estado = "Aprobada"
            };

            this.context.HistoriasExito.Add(historia);
            await this.context.SaveChangesAsync();
            return true;
        }

        public async Task<Adoptante> GetAdoptanteByUsuarioId(int idusuario)
        {
            return await this.context.Adoptantes
                .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
        }

        public async Task<List<LikeHistoria>> ObtenerLikeHistoriaAsync(int idHistoria)
        {
            List<LikeHistoria> likesHistoria = await this.context.LikesHistorias
                .Where(m => m.IdHistoria == idHistoria)
                .OrderByDescending(m => m.Fecha)
                .ToListAsync();

            return likesHistoria;
        }
        public async Task<LikeHistoria> ObtenerLikeUsuarioHistoriaAsync(int idHistoria, int idusuario)
        {
            // Obtener el like existente del usuario para esta historia
            var like = await this.context.LikesHistorias
                .Include(l => l.Usuario)
                .FirstOrDefaultAsync(l => l.IdHistoria == idHistoria && l.IdUsuario == idusuario);

            return like;
        }

        public async Task<bool> CrearLikeHistoriaAsync(int idHistoria, int idusuario, string tipoReaccion)
        {
            try
            {
                // Obtener el usuario
                var usuario = await this.context.Usuarios
                    .FirstOrDefaultAsync(u => u.Id == idusuario);

                if (usuario == null)
                {
                    return false;
                }

                // Crear nuevo like
                var newLike = new LikeHistoria
                {
                    Id = await GetMaxIdAsync(this.context.LikesHistorias),
                    IdHistoria = idHistoria,
                    IdUsuario = usuario.Id,
                    TipoReaccion = tipoReaccion,
                    Fecha = DateTime.Now
                };

                await this.context.LikesHistorias.AddAsync(newLike);
                await this.context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ActualizarLikeHistoriaAsync(int idHistoria, int idusuario, string tipoReaccion)
        {
            try
            {
                // Obtener el like existente
                var like = await ObtenerLikeUsuarioHistoriaAsync(idHistoria, idusuario);

                if (like == null)
                {
                    return false;
                }

                // Actualizar el tipo de reacción
                like.TipoReaccion = tipoReaccion;
                like.Fecha = DateTime.Now;

                this.context.LikesHistorias.Update(like);
                await this.context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> EliminarLikeHistoriaAsync(int idHistoria, int idusuario)
        {
            try
            {
                // Obtener el like existente
                var like = await ObtenerLikeUsuarioHistoriaAsync(idHistoria, idusuario);

                if (like == null)
                {
                    return false;
                }

                // Eliminar el like
                this.context.LikesHistorias.Remove(like);
                await this.context.SaveChangesAsync();

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Dictionary<string, int>> ObtenerContadoresReaccionesAsync(int idHistoria)
        {
            // Obtener todos los likes para esta historia
            var likes = await ObtenerLikeHistoriaAsync(idHistoria);

            // Crear un diccionario con los contadores para cada tipo de reacción
            var contadores = new Dictionary<string, int>
        {
            { "meGusta", likes.Count(l => l.TipoReaccion == "MeGusta") },
            { "meEncanta", likes.Count(l => l.TipoReaccion == "MeEncanta") },
            { "inspirador", likes.Count(l => l.TipoReaccion == "Inspirador") },
            { "solidario", likes.Count(l => l.TipoReaccion == "Solidario") },
            { "asombroso", likes.Count(l => l.TipoReaccion == "Asombroso") }
        };

            return contadores;
        }

        public async Task<int> GetMaxIdAsync<T>(DbSet<T> dbSet) where T : class
        {
            var propertyInfo = typeof(T).GetProperty("Id");

            var parameter = Expression.Parameter(typeof(T), "x");
            var property = Expression.Property(parameter, propertyInfo);
            var lambda = Expression.Lambda<Func<T, int>>(property, parameter);

            return await dbSet.MaxAsync(lambda) + 1;
        }

        public async Task<int?> RegisterUserAsync(string nombreusuario, string email, string contrasenalimpia, string tipousuario)
        {
            var existingUser = await this.context.Usuarios
                .FirstOrDefaultAsync(u => u.NombreUsuario == nombreusuario || u.Email == email);

            if (existingUser != null)
            {
                return null;
            }

            Usuario user = new Usuario();
            user.Id = await GetMaxIdAsync(this.context.Usuarios);
            user.NombreUsuario = nombreusuario;
            user.Email = email;
            user.ContrasenaLimpia = contrasenalimpia;
            user.Salt = HelperCriptography.GenerateSalt();
            user.ContrasenaEncriptada = HelperCriptography.EncryptPassword(contrasenalimpia, user.Salt);
            user.TipoUsuario = tipousuario;
            this.context.Usuarios.Add(user);
            await this.context.SaveChangesAsync();
            return user.Id;
        }

        public async Task RegisterPerfilUserAsync(int idusuario, string fotoperfil)
        {
            PerfilUsuario perfil = new PerfilUsuario
            {
                Id = await GetMaxIdAsync(this.context.PerfilUsuario),
                IdUsuario = idusuario,
                FotoPerfil = fotoperfil,
                Descripcion = "¡Bienvenido a tu perfil! Edita la descripción a tu gusto."
            };

            this.context.PerfilUsuario.Add(perfil);
            await this.context.SaveChangesAsync();
        }

        public async Task RegisterAdoptanteAsync(int idusuario, string nombre, string tipoVivienda, bool tieneJardin, bool otrosAnimales, List<string> recursosDisponibles, string tiempoEnCasa)
        {
            var listaRecursos = new List<string> { "Compromiso emocional" };
            Adoptante adoptante = new Adoptante
            {
                Id = await GetMaxIdAsync(this.context.Adoptantes),
                IdUsuario = idusuario,
                Nombre = nombre,
                TipoVivienda = tipoVivienda,
                TieneJardin = tieneJardin,
                OtrosAnimales = otrosAnimales,
                RecursosDisponibles = listaRecursos,
                TiempoEnCasa = tiempoEnCasa
            };

            this.context.Adoptantes.Add(adoptante);
            await this.context.SaveChangesAsync();
        }

        public async Task RegisterRefugioAsync(int idusuario, string nombrerefugio, string contactorefugio, int cantidadanimales, int capacidadmaxima, double longitud, double latitud)
        {
            Refugio refugio = new Refugio
            {
                Id = await GetMaxIdAsync(this.context.Refugios),
                IdUsuario = idusuario,
                NombreRefugio = nombrerefugio,
                ContactoRefugio = contactorefugio,
                CantidadAnimales = cantidadanimales,
                CapacidadMaxima = capacidadmaxima,
                Latitud = longitud,
                Longitud = latitud
            };
            // Esta hecho a posta lo de poner en latitud longitud y viceversa
            Console.WriteLine(refugio);
            this.context.Refugios.Add(refugio);
            await this.context.SaveChangesAsync();
        }

        public async Task<string> GetFotoPerfilAsync(int usuarioId)
        {
            var perfil = await this.context.PerfilUsuario
                .Where(p => p.IdUsuario == usuarioId)
                .Select(p => p.FotoPerfil)
                .FirstOrDefaultAsync();

            return perfil;
        }


        public async Task<Usuario> LogInUserAsync(string nombreUsuario, string contrasena)
        {
            var consulta = from datos in this.context.Usuarios
                           where datos.NombreUsuario == nombreUsuario
                           select datos;
            Usuario user = await consulta.FirstOrDefaultAsync();
            if (user == null)
            {
                return null;
            }
            else
            {
                string salt = user.Salt;
                byte[] temp = HelperCriptography.EncryptPassword(contrasena, salt);
                byte[] passBytes = user.ContrasenaEncriptada;
                bool response = HelperCriptography.CompararArrays(temp, passBytes);
                if (response == true)
                {
                    return user;
                }
                else
                {
                    return null;
                }
                return user;
            }

        }

        public async Task<VistaPerfilAdoptante> GetPerfilAdoptante(int idusuario)
        {
            string sql = "CALL SP_PERFILADOPTANTE(@p_idusuario)";
            var pamIdUsuario = new MySql.Data.MySqlClient.MySqlParameter("@p_idusuario", idusuario);
            var consulta = this.context.VistaPerfilAdoptante
                .FromSqlRaw(sql, pamIdUsuario)
                .AsEnumerable();
            return consulta.FirstOrDefault();
        }




        public async Task<VistaPerfilRefugio> GetPerfilRefugio(int idusuario)
        {
            string sql = "CALL SP_PERFILREFUGIO(@p_idusuario)";
            var pamIdUsuario = new MySql.Data.MySqlClient.MySqlParameter("@p_idusuario", idusuario);
            var consulta = this.context.VistaPerfilRefugio
                .FromSqlRaw(sql, pamIdUsuario).AsEnumerable();

            return consulta.FirstOrDefault();
        }


        public async Task<bool> EsFavorito(int idusuario, int idmascota)
        {
            var adoptante = await this.context.Adoptantes
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
            return await this.context.Favoritos.AnyAsync(f => f.IdAdoptante == adoptante.Id && f.IdMascota == idmascota);
        }

        public async Task<bool> InsertMascotaFavorita(int idusuario, int idmascota)
        {
            var adoptante = await this.context.Adoptantes
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);

            var favorito = new Favorito
            {
                Id = await GetMaxIdAsync(this.context.Favoritos),
                IdAdoptante = adoptante.Id,
                IdMascota = idmascota,
                FechaAgregado = DateTime.Now
            };
            this.context.Favoritos.Add(favorito);
            await this.context.SaveChangesAsync();
            return true;

        }

        public async Task<bool> EliminarFavorito(int idusuario, int idmascota)
        {
            var adoptante = await this.context.Adoptantes
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
            var favorito = await this.context.Favoritos
                .FirstOrDefaultAsync(f => f.IdAdoptante == adoptante.Id && f.IdMascota == idmascota);

            if (favorito == null)
            {
                return false;
            }
            else
            {
                this.context.Favoritos.Remove(favorito);
                await this.context.SaveChangesAsync();
                return true;
            }
        }

        public async Task<List<MascotaCard>> ObtenerMascotasFavoritas(int idusuario)
        {
            Console.WriteLine("IDUSUARIO", idusuario);
            var adoptante = await this.context.Adoptantes
                .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
            Console.WriteLine("IDA", adoptante.Id);

            string sql = "CALL SP_OBTENERMASCOTASFAVORITAS(@p_idadoptante)";
            var pamAdoptante = new MySql.Data.MySqlClient.MySqlParameter("@p_idadoptante", adoptante.Id);

            List<MascotaCard> favoritas = await this.context.MascotasFavoritas
                .FromSqlRaw(sql, pamAdoptante)
                .ToListAsync();

            return favoritas;
        }


        public async Task<List<MascotaAdoptada>> ObtenerMascotasAdoptadas(int idusuario)
        {
            var adoptante = await this.context.Adoptantes
                .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);

            string sql = "CALL SP_OBTENERMASCOTASADOPTADAS(@p_idadoptante)";
            var pamAdoptante = new MySql.Data.MySqlClient.MySqlParameter("@p_idadoptante", adoptante.Id);

            List<MascotaAdoptada> adoptadas = await this.context.MascotasAdoptadas
                .FromSqlRaw(sql, pamAdoptante)
                .ToListAsync();

            return adoptadas;
        }


        public async Task<DateTime?> ObtenerUltimaAccionFavorito(int idusuario, int idmascota)
        {
            var adoptante = await this.context.Adoptantes
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
            var favorito = await this.context.Favoritos
                .Where(f => f.IdAdoptante == adoptante.Id && f.IdMascota == idmascota)
                .OrderByDescending(f => f.FechaAgregado)
                .FirstOrDefaultAsync();
            return favorito?.FechaAgregado;
        }

        public async Task GuardarUltimaAccionFavorito(int idusuario, int idmascota, DateTime fecha)
        {
            var adoptante = await this.context.Adoptantes
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
            var favorito = new Favorito
            {
                IdAdoptante = adoptante.Id,
                IdMascota = idmascota,
                FechaAgregado = fecha
            };
            this.context.Favoritos.Add(favorito);
            await this.context.SaveChangesAsync();
        }

        public async Task<Mascota> GetDetallesMascotaAsync(int idmascota)
        {
            return await this.context.Mascotas
                .Include(m => m.Refugio)
                .ThenInclude(r => r.Usuario)
                .ThenInclude(u => u.PerfilUsuario).
                FirstOrDefaultAsync(m => m.Id == idmascota);
        }

        public async Task<Refugio> GetDetallesRefugioAsync(int idrefugio)
        {
            return await this.context.Refugios
                .Include(l => l.Usuario)
                .Include(l => l.Usuario.PerfilUsuario).
                FirstOrDefaultAsync(m => m.Id == idrefugio);
        }

        public async Task<Refugio> GetRefugio(int idusuario)
        {
            var usuario = await this.context.Usuarios
            .FirstOrDefaultAsync(a => a.Id == idusuario);
            return await this.context.Refugios
                .Include(l => l.Usuario)
                .Include(l => l.Usuario.PerfilUsuario).
                FirstOrDefaultAsync(m => m.IdUsuario == usuario.Id);
        }

        public async Task<List<Mascota>> GetMascotasPorRefugioAsync(int idrefugio)
        {
            var refugio = await this.context.Refugios
                .Where(r => r.Id == idrefugio)
                .FirstOrDefaultAsync();

            if (refugio == null)
                return new List<Mascota>();

            List<Mascota> mascotas = await this.context.Mascotas
                .Where(m => m.IdRefugio == refugio.Id)
                .ToListAsync();

            return mascotas;
        }

        public async Task<SolicitudAdopcion> CrearSolicitudAdopcionAsync(int idusuario, int idmascota)
        {
            var adoptante = await this.context.Adoptantes
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);

            bool exp = false;

            if (adoptante.OtrosAnimales == true)
            {
                exp = true;
            }

            SolicitudAdopcion soli = new SolicitudAdopcion
            {
                Id = await GetMaxIdAsync(this.context.SolicitudesAdopcion),
                IdAdoptante = adoptante.Id,
                IdMascota = idmascota,
                Estado = "Pendiente",
                ExperienciaPrevia = exp,
                TipoVivienda = adoptante.TipoVivienda,
                OtrosAnimales = adoptante.OtrosAnimales,
                Recursos = adoptante.RecursosDisponibles,
                TiempoEnCasa = adoptante.TiempoEnCasa,
                FechaSolicitud = DateTime.Now,
                FechaRespuesta = null
            };

            this.context.SolicitudesAdopcion.Add(soli);
            await this.context.SaveChangesAsync();
            return soli;
        }

        public async Task<int?> IdRefugioPorMascotaAsync(int idMascota)
        {
            int? idRefugio = await this.context.Mascotas
                .Where(m => m.Id == idMascota)
                .Select(m => m.IdRefugio)
                .FirstOrDefaultAsync();
            return idRefugio;
        }
        public async Task<string> GetNombreMascotaAsync(int idMascota)
        {
            string nombreMascota = await this.context.Mascotas
                .Where(m => m.Id == idMascota)
                .Select(m => m.Nombre)
                .FirstOrDefaultAsync();
            return nombreMascota;
        }
        public async Task<bool> CrearNotificacionAsync(int idSolicitud, int idRefugio, string nombreMascota)
        {
            var refugio = await this.context.Refugios
                        .FirstOrDefaultAsync(a => a.Id == idRefugio);
            // Crear notificación para el adoptante
            Notificacion notificacion = new Notificacion
            {
                Id = await GetMaxIdAsync(this.context.Notificaciones),
                IdUsuario = refugio.IdUsuario,
                Tipo = "Mensaje",
                Fecha = DateTime.Now,
                Leido = false
            };


            notificacion.Mensaje = $"Te ha llegado una nueva solicitud por {nombreMascota}";
            notificacion.Url = $"/Refugio/DetallesSolicitud?idsolicitud={idSolicitud}";

            // Agregar la notificación a la base de datos
            this.context.Notificaciones.Add(notificacion);

            // Guardar todos los cambios
            await this.context.SaveChangesAsync();
            return true;
        }
        public async Task<bool> ExisteSolicitudAdopcionAsync(int idusuario, int idmascota)
        {
            var adoptante = await this.context.Adoptantes
                        .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);

            if (adoptante == null)
            {
                return false;
            }

            return await this.context.SolicitudesAdopcion
                .AnyAsync(s => s.IdAdoptante == adoptante.Id && s.IdMascota == idmascota);
        }

        public async Task<Refugio> GetRefugioByUsuarioIdAsync(int idUsuario)
        {
            return await this.context.Refugios
                .Include(r => r.Usuario)
                .Include(r => r.Usuario.PerfilUsuario)
                .FirstOrDefaultAsync(r => r.IdUsuario == idUsuario);
        }

        public async Task<int> GetSolicitudesPendientesCountAsync(int idRefugio)
        {
            return await this.context.SolicitudesAdopcion
                .Where(s => s.Mascota.IdRefugio == idRefugio && s.Estado == "Pendiente")
                .CountAsync();
        }

        public async Task<IEnumerable<Mascota>> GetMascotasByRefugioIdAsync(int idRefugio)
        {
            return await this.context.Mascotas
                .Where(m => m.IdRefugio == idRefugio)
                .ToListAsync();
        }

        public async Task<int> GetSolicitudesByEstadoAndRefugioAsync(int idRefugio, string estado)
        {
            return await this.context.SolicitudesAdopcion
                .Where(s => s.Mascota.IdRefugio == idRefugio && s.Estado == estado)
                .CountAsync();
        }

        public async Task<bool> CrearMascotaRefugioAsync(Mascota m, int idusuario)
        {
            var refugio = await this.context.Refugios
            .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);

            Mascota mascota = new Mascota()
            {
                Id = await GetMaxIdAsync(this.context.Mascotas),
                Nombre = m.Nombre,
                Especie = m.Especie,
                FechaNacimiento = m.FechaNacimiento,
                Tamano = m.Tamano,
                Sexo = m.Sexo,
                Castrado = m.Castrado,
                Vacunado = m.Vacunado,
                Desparasitado = m.Desparasitado,
                Sano = m.Sano,
                Microchip = m.Microchip,
                CompatibleConNinos = m.CompatibleConNinos,
                CompatibleConAdultos = m.CompatibleConAdultos,
                CompatibleConOtrosAnimales = m.CompatibleConOtrosAnimales,
                Personalidad = m.Personalidad,
                Descripcion = m.Descripcion,
                Estado = m.Estado,
                Latitud = m.Latitud,
                Longitud = m.Longitud,
                IdRefugio = refugio.Id,
                Foto = m.Foto,
                Vistas = 0,
                FechaRegistro = DateTime.Now
            };

            refugio.CantidadAnimales++;

            this.context.Mascotas.Add(mascota);
            await this.context.SaveChangesAsync();
            return true;
        }

        public async Task<Mascota> GetMascotaByIdAsync(int id)
        {
            return await this.context.Mascotas
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<bool> UpdateMascotaAsync(Mascota mascota)
        {
            Mascota mascotaActual = await this.context.Mascotas
                .FirstOrDefaultAsync(m => m.Id == mascota.Id);

            if (mascotaActual == null)
            {
                return false;
            }

            mascotaActual.Nombre = mascota.Nombre;
            mascotaActual.Especie = mascota.Especie;
            mascotaActual.FechaNacimiento = mascota.FechaNacimiento;
            mascotaActual.Tamano = mascota.Tamano;
            mascotaActual.Sexo = mascota.Sexo;
            mascotaActual.Castrado = mascota.Castrado;
            mascotaActual.Vacunado = mascota.Vacunado;
            mascotaActual.Desparasitado = mascota.Desparasitado;
            mascotaActual.Sano = mascota.Sano;
            mascotaActual.Microchip = mascota.Microchip;
            mascotaActual.CompatibleConNinos = mascota.CompatibleConNinos;
            mascotaActual.CompatibleConAdultos = mascota.CompatibleConAdultos;
            mascotaActual.CompatibleConOtrosAnimales = mascota.CompatibleConOtrosAnimales;
            mascotaActual.Personalidad = mascota.Personalidad;
            mascotaActual.Descripcion = mascota.Descripcion;
            mascotaActual.Estado = mascota.Estado;
            mascotaActual.Latitud = mascota.Latitud;
            mascotaActual.Longitud = mascota.Longitud;

            if (!string.IsNullOrEmpty(mascota.Foto))
            {
                mascotaActual.Foto = mascota.Foto;
            }

            await this.context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteMascotaAsync(int id)
        {
            using (var transaction = await this.context.Database.BeginTransactionAsync())
            {
                try
                {
                    // Obtener la mascota de la base de datos
                    Mascota mascota = await this.context.Mascotas
                        .FirstOrDefaultAsync(m => m.Id == id);

                    if (mascota == null)
                    {
                        return false;
                    }

                    // 1. Eliminar favoritos relacionados con esta mascota
                    var favoritos = await this.context.Favoritos
                        .Where(f => f.IdMascota == id)
                        .ToListAsync();

                    if (favoritos.Any())
                    {
                        this.context.Favoritos.RemoveRange(favoritos);
                    }

                    // 2. Eliminar solicitudes de adopción relacionadas con esta mascota
                    var solicitudes = await this.context.SolicitudesAdopcion
                        .Where(s => s.IdMascota == id)
                        .ToListAsync();

                    if (solicitudes.Any())
                    {
                        this.context.SolicitudesAdopcion.RemoveRange(solicitudes);
                    }

                    // 3. Buscar y eliminar historias de éxito relacionadas con esta mascota
                    var historias = await this.context.HistoriasExito
                        .Where(h => h.IdMascota == id)
                        .ToListAsync();

                    // Ahora eliminamos las historias
                    if (historias.Any())
                    {
                        this.context.HistoriasExito.RemoveRange(historias);
                    }

                    // 4. Finalmente, eliminar la mascota
                    this.context.Mascotas.Remove(mascota);

                    var refugio = await this.context.Refugios
                        .FirstOrDefaultAsync(a => a.Id == mascota.IdRefugio);
                    refugio.CantidadAnimales--;
                    // Guardar todos los cambios
                    await this.context.SaveChangesAsync();

                    // Confirmar la transacción
                    await transaction.CommitAsync();

                    return true;
                }
                catch (Exception)
                {
                    // Si ocurre algún error, deshacer la transacción
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
        public async Task<List<SolicitudAdopcion>> GetSolicitudesRefugioAsync(int idusuario)
        {
            var refugio = await this.context.Refugios
                .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);

            List<SolicitudAdopcion> solicitudes = await this.context.SolicitudesAdopcion
                .Include(s => s.Mascota)
                .Include(s => s.Adoptante)
                .Include(s => s.Adoptante.Usuario)
                .Where(s => s.Mascota.IdRefugio == refugio.Id)
                .OrderByDescending(s => s.FechaSolicitud)
                .ToListAsync();

            return solicitudes;
        }
        public async Task<SolicitudAdopcion> GetSolicitudByIdAsync(int idusuario, int idsolicitud)
        {
            var refugio = await this.context.Refugios
                .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);

            SolicitudAdopcion solicitudes = await this.context.SolicitudesAdopcion
                .Include(s => s.Mascota)
                .Include(s => s.Adoptante)
                .Include(s => s.Adoptante.Usuario)
                .Where(s => s.Id == idsolicitud)
                .OrderByDescending(s => s.FechaSolicitud)
                .FirstOrDefaultAsync();

            return solicitudes;
        }

        public async Task<bool> ProcesarSolicitudAdopcionAsync(int idSolicitud, string nuevoEstado)
        {
            // Obtener la solicitud con su mascota relacionada y el adoptante
            SolicitudAdopcion solicitud = await this.context.SolicitudesAdopcion
                .Include(s => s.Mascota)
                .Include(s => s.Adoptante)
                .FirstOrDefaultAsync(s => s.Id == idSolicitud);

            if (solicitud == null)
            {
                return false;
            }

            // Actualizar el estado de la solicitud
            solicitud.Estado = nuevoEstado;
            solicitud.FechaRespuesta = DateTime.Now;

            // Crear notificación para el adoptante
            Notificacion notificacion = new Notificacion
            {
                Id = await GetMaxIdAsync(this.context.Notificaciones),
                IdUsuario = solicitud.Adoptante.IdUsuario,
                Tipo = "Aprobación",
                Fecha = DateTime.Now,
                Leido = false
            };

            // Configurar el mensaje según el estado
            if (nuevoEstado == "Aprobada")
            {
                notificacion.Mensaje = $"¡Felicidades! Tu solicitud para adoptar a {solicitud.Mascota.Nombre} ha sido aprobada.";
                notificacion.Url = $"/Adoptante/DetallesMascota?idmascota={solicitud.Mascota.Id}";

                // Actualizar el estado de la mascota
                solicitud.Mascota.Estado = "Adoptado";

                // Eliminar todos los registros de favoritos relacionados con esta mascota
                var favoritosAEliminar = await this.context.Favoritos
                    .Where(f => f.IdMascota == solicitud.IdMascota)
                    .ToListAsync();
                this.context.Favoritos.RemoveRange(favoritosAEliminar);

                // Rechazar automáticamente otras solicitudes pendientes para esta mascota
                var otrasSolicitudes = await this.context.SolicitudesAdopcion
                    .Where(s => s.IdMascota == solicitud.IdMascota &&
                           s.Id != solicitud.Id &&
                           s.Estado == "Pendiente")
                    .ToListAsync();

                foreach (var otraSolicitud in otrasSolicitudes)
                {
                    otraSolicitud.Estado = "Rechazada";
                    otraSolicitud.FechaRespuesta = DateTime.Now;

                    // Crear notificación para los adoptantes rechazados
                    Notificacion notificacionRechazo = new Notificacion
                    {
                        Id = await GetMaxIdAsync(this.context.Notificaciones),
                        IdUsuario = otraSolicitud.Adoptante.IdUsuario,
                        Tipo = "Aprobación",
                        Mensaje = $"Lo sentimos, tu solicitud para adoptar a {solicitud.Mascota.Nombre} ha sido rechazada porque la mascota ya ha sido adoptada.",
                        Url = $"/Adoptante/Listado",
                        Fecha = DateTime.Now,
                        Leido = false
                    };

                    this.context.Notificaciones.Add(notificacionRechazo);
                }
            }
            else if (nuevoEstado == "Rechazada")
            {
                notificacion.Mensaje = $"Lo sentimos, tu solicitud para adoptar a {solicitud.Mascota.Nombre} ha sido rechazada.";
                notificacion.Url = $"/Mascotas/Listado";
            }

            // Agregar la notificación a la base de datos
            this.context.Notificaciones.Add(notificacion);

            // Guardar todos los cambios
            await this.context.SaveChangesAsync();
            return true;
        }

        public async Task<List<Notificacion>> GetNotificacionesUsuarioAsync(int idUsuario, int pagina, int tamañoPagina)
        {
            return await this.context.Notificaciones
                .Where(n => n.IdUsuario == idUsuario)
                .OrderByDescending(n => n.Fecha)
                .Skip((pagina - 1) * tamañoPagina)
                .Take(tamañoPagina)
                .ToListAsync();
        }

        public async Task<int> GetTotalNotificacionesUsuarioAsync(int idUsuario)
        {
            return await this.context.Notificaciones
                .Where(n => n.IdUsuario == idUsuario)
                .CountAsync();
        }

        public async Task<int> GetTotalNotificacionesNoLeidasAsync(int idUsuario)
        {
            return await this.context.Notificaciones
                .Where(n => n.IdUsuario == idUsuario && n.Leido == false)
                .CountAsync();
        }

        public async Task<bool> MarcarNotificacionComoLeidaAsync(int idNotificacion, int idUsuario)
        {
            var notificacion = await this.context.Notificaciones
                .FirstOrDefaultAsync(n => n.Id == idNotificacion && n.IdUsuario == idUsuario);

            if (notificacion == null)
            {
                return false;
            }

            notificacion.Leido = true;
            await this.context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> MarcarTodasNotificacionesComoLeidasAsync(int idUsuario)
        {
            var notificaciones = await this.context.Notificaciones
                .Where(n => n.IdUsuario == idUsuario && n.Leido == false)
                .ToListAsync();

            foreach (var notificacion in notificaciones)
            {
                notificacion.Leido = true;
            }

            await this.context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> EliminarNotificacionAsync(int idNotificacion, int idUsuario)
        {
            var notificacion = await this.context.Notificaciones
                .FirstOrDefaultAsync(n => n.Id == idNotificacion && n.IdUsuario == idUsuario);

            if (notificacion == null)
            {
                return false;
            }

            this.context.Notificaciones.Remove(notificacion);
            await this.context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> HayNotificacionesNuevasDesdeAsync(int idUsuario, DateTime desde)
        {
            var nuevas = await this.context.Notificaciones
                .Where(n => n.IdUsuario == idUsuario && n.Fecha > desde)
                .AnyAsync();

            return nuevas;
        }
        public async Task<List<Mensaje>> GetMensajesConversacionAsync(int usuarioActualId, int otroUsuarioId)
        {
            return await this.context.Mensajes
                .Where(m => (m.IdEmisor == usuarioActualId && m.IdReceptor == otroUsuarioId) ||
                            (m.IdEmisor == otroUsuarioId && m.IdReceptor == usuarioActualId))
                .OrderBy(m => m.Fecha)
                .Include(m => m.Emisor)
                .Include(m => m.Receptor)
                .ToListAsync();
        }

        public async Task<List<ConversacionViewModel>> GetConversacionesAdoptanteAsync(int usuarioId)
        {
            // Obtenemos todos los usuarios con los que ha conversado
            var usuariosConversaciones = await this.context.Mensajes
                .Where(m => m.IdEmisor == usuarioId || m.IdReceptor == usuarioId)
                .Select(m => m.IdEmisor == usuarioId ? m.IdReceptor : m.IdEmisor)
                .Distinct()
                .ToListAsync();

            var conversaciones = new List<ConversacionViewModel>();

            foreach (var otroUsuarioId in usuariosConversaciones)
            {
                // Obtenemos el último mensaje de cada conversación
                var ultimoMensaje = await this.context.Mensajes
                    .Where(m => (m.IdEmisor == usuarioId && m.IdReceptor == otroUsuarioId) ||
                                (m.IdEmisor == otroUsuarioId && m.IdReceptor == usuarioId))
                    .OrderByDescending(m => m.Fecha)
                    .FirstOrDefaultAsync();

                if (ultimoMensaje != null)
                {
                    // Obtenemos la información del usuario (refugio)
                    var otroUsuario = await this.context.Usuarios
                        .FirstOrDefaultAsync(u => u.Id == otroUsuarioId);

                    var refugio = await this.context.Refugios
                        .Include(refugio => refugio.Usuario.PerfilUsuario)
                        .FirstOrDefaultAsync(r => r.IdUsuario == otroUsuarioId);

                    var conversacion = new ConversacionViewModel
                    {
                        UsuarioId = otroUsuarioId,
                        Foto = refugio.Usuario.PerfilUsuario.FotoPerfil,
                        NombreUsuario = refugio.Usuario.NombreUsuario,
                        UltimoMensaje = ultimoMensaje.Contenido,
                        FechaUltimoMensaje = ultimoMensaje.Fecha,
                        NoLeidos = await this.context.Mensajes
                            .CountAsync(m => m.IdEmisor == otroUsuarioId &&
                                      m.IdReceptor == usuarioId &&
                                      !m.Leido)
                    };

                    conversaciones.Add(conversacion);
                }
            }

            return conversaciones.OrderByDescending(c => c.FechaUltimoMensaje).ToList();
        }

        public async Task<List<ConversacionViewModel>> GetConversacionesRefugioAsync(int usuarioId)
        {
            // Obtenemos todos los usuarios con los que ha conversado
            var usuariosConversaciones = await this.context.Mensajes
                .Where(m => m.IdEmisor == usuarioId || m.IdReceptor == usuarioId)
                .Select(m => m.IdEmisor == usuarioId ? m.IdReceptor : m.IdEmisor)
                .Distinct()
                .ToListAsync();

            var conversaciones = new List<ConversacionViewModel>();

            foreach (var otroUsuarioId in usuariosConversaciones)
            {
                // Obtenemos el último mensaje de cada conversación
                var ultimoMensaje = await this.context.Mensajes
                    .Where(m => (m.IdEmisor == usuarioId && m.IdReceptor == otroUsuarioId) ||
                                (m.IdEmisor == otroUsuarioId && m.IdReceptor == usuarioId))
                    .OrderByDescending(m => m.Fecha)
                    .FirstOrDefaultAsync();

                if (ultimoMensaje != null)
                {
                    // Obtenemos la información del usuario (refugio)
                    var otroUsuario = await this.context.Usuarios
                        .FirstOrDefaultAsync(u => u.Id == otroUsuarioId);

                    var adoptante = await this.context.Adoptantes
                        .Include(adoptante => adoptante.Usuario.PerfilUsuario)
                        .FirstOrDefaultAsync(r => r.IdUsuario == otroUsuarioId);

                    var conversacion = new ConversacionViewModel
                    {
                        UsuarioId = otroUsuarioId,
                        Foto = adoptante.Usuario.PerfilUsuario.FotoPerfil,
                        NombreUsuario = adoptante.Usuario.NombreUsuario,
                        UltimoMensaje = ultimoMensaje.Contenido,
                        FechaUltimoMensaje = ultimoMensaje.Fecha,
                        NoLeidos = await this.context.Mensajes
                            .CountAsync(m => m.IdEmisor == otroUsuarioId &&
                                      m.IdReceptor == usuarioId &&
                                      !m.Leido)
                    };

                    conversaciones.Add(conversacion);
                }
            }

            return conversaciones.OrderByDescending(c => c.FechaUltimoMensaje).ToList();
        }

        public async Task<Mensaje> AgregarMensajeAsync(int emisorId, int receptorId, string contenido)
        {
            var mensaje = new Mensaje
            {
                Id = await GetMaxIdAsync(this.context.Mensajes),
                IdEmisor = emisorId,
                IdReceptor = receptorId,
                Contenido = contenido,
                Fecha = DateTime.Now,
                Leido = false
            };

            this.context.Mensajes.Add(mensaje);
            await this.context.SaveChangesAsync();
            return mensaje;
        }

        public async Task<bool> ActualizarDescripcionAsync(int idusuario, string descripcion)
        {
            try
            {
                var refugio = await GetPerfilRefugio(idusuario);
                if (refugio == null)
                    return false;

                refugio.Descripcion = descripcion;
                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ActualizarDetallesRefugioAsync(int idUsuario, string contacto, int cantidadAnimales, int capacidadMaxima)
        {
            try
            {
                var refugio = await GetPerfilRefugio(idUsuario);
                if (refugio == null)
                    return false;

                refugio.ContactoRefugio = contacto;
                refugio.CantidadAnimales = cantidadAnimales;
                refugio.CapacidadMaxima = capacidadMaxima;

                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ActualizarUbicacionRefugioAsync(int idUsuario, double latitud, double longitud)
        {
            try
            {
                var refugio = await GetRefugioByUsuarioIdAsync(idUsuario);
                if (refugio == null)
                    return false;

                // Redondea los valores a 6 decimales
                refugio.Latitud = Math.Round(latitud, 6);
                refugio.Longitud = Math.Round(longitud, 6);

                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> ActualizarPerfilRefugioAsync(int idUsuario, string email, string nombreRefugio, string contactoRefugio)
        {
            try
            {
                var usuario = await context.Usuarios.FindAsync(idUsuario);
                if (usuario == null)
                    return false;

                usuario.Email = email;

                var refugio = await context.Refugios.FirstOrDefaultAsync(r => r.IdUsuario == idUsuario);
                if (refugio == null)
                    return false;

                refugio.NombreRefugio = nombreRefugio;
                refugio.ContactoRefugio = contactoRefugio;

                await context.SaveChangesAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> ActualizarFotoPerfilAsync(int idUsuario, string nombreArchivo)
        {
            try
            {
                var refugio = await GetPerfilRefugio(idUsuario);
                if (refugio == null)
                    return null;

                // Guardar el nombre de archivo anterior para retornarlo posiblemente
                string archivoAnterior = refugio.FotoPerfil;

                // Actualizar con el nuevo nombre de archivo
                refugio.FotoPerfil = nombreArchivo;
                await context.SaveChangesAsync();

                // Obtener la nueva foto de perfil para retornarla
                return await GetFotoPerfilAsync(idUsuario);
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool> MarcarMensajesComoLeidosAsync(int usuarioActualId, int otroUsuarioId)
        {
            try
            {
                // Obtener directamente los mensajes no leídos que necesitamos actualizar
                // con tracking habilitado (por defecto en EF Core)
                var mensajesNoLeidos = await context.Mensajes
                    .Where(m => m.IdEmisor == otroUsuarioId &&
                                m.IdReceptor == usuarioActualId &&
                                !m.Leido)
                    .ToListAsync();

                // Marcar cada mensaje como leído
                foreach (var mensaje in mensajesNoLeidos)
                {
                    mensaje.Leido = true;
                }

                // Guardar los cambios
                await context.SaveChangesAsync();

                // Retornar verdadero si hubo cambios, falso si no había mensajes para marcar
                return mensajesNoLeidos.Any();
            }
            catch (Exception ex)
            {
                // Es mejor registrar la excepción para depuración
                Console.WriteLine($"Error al marcar mensajes como leídos: {ex.Message}");
                return false;
            }
        }



        //public async Task<Adoptante> GetAdoptanteChatByUsuarioId(int idusuario)
        //{
        //    return await this.context.Adoptantes
        //        .Include(adoptante => adoptante.Usuario.PerfilUsuario)
        //        .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
        //}


        public async Task<bool> ActualizarDescripcionAdoptante(int idUsuario, string descripcion)
        {
            try
            {
                var adoptante = await this.GetPerfilAdoptante(idUsuario);
                if (adoptante == null) return false;

                adoptante.Descripcion = descripcion;
                await this.context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Opcionalmente, registrar excepción
                Console.WriteLine($"Error al actualizar descripción: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActualizarDetallesAdoptante(int idUsuario, VistaPerfilAdoptante modelo)
        {
            try
            {
                var adoptante = await this.GetPerfilAdoptante(idUsuario);
                if (adoptante == null) return false;

                adoptante.TipoVivienda = modelo.TipoVivienda;
                adoptante.RecursosDisponibles = modelo.RecursosDisponibles;
                adoptante.TiempoEnCasa = modelo.TiempoEnCasa;
                adoptante.TieneJardin = modelo.TieneJardin;
                adoptante.OtrosAnimales = modelo.OtrosAnimales;

                await this.context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Opcionalmente, registrar excepción
                Console.WriteLine($"Error al actualizar detalles: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActualizarPerfilAdoptante(int idUsuario, string email, string nombre)
        {
            try
            {
                var usuario = await this.context.Usuarios.FindAsync(idUsuario);
                if (usuario != null)
                {
                    usuario.Email = email;
                }

                var adoptante = await this.context.Adoptantes.FirstOrDefaultAsync(a => a.IdUsuario == idUsuario);
                if (adoptante != null)
                {
                    adoptante.Nombre = nombre;
                }

                await this.context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Opcionalmente, registrar excepción
                Console.WriteLine($"Error al actualizar perfil: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActualizarFotoPerfilAdoptante(int idUsuario, string fileName)
        {
            try
            {
                var adoptante = await this.GetPerfilAdoptante(idUsuario);
                if (adoptante == null) return false;

                adoptante.FotoPerfil = fileName;
                await this.context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Opcionalmente, registrar excepción
                Console.WriteLine($"Error al actualizar foto de perfil: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> ActualizarFotoMascota(int idMascota, string nuevaFoto)
        {
            try
            {
                Mascota mascota = await this.context.Mascotas.FindAsync(idMascota);
                if (mascota != null)
                {
                    mascota.Foto = nuevaFoto;
                    await this.context.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public async Task<bool> IncrementarVistasMascota(int idMascota)
        {
            try
            {
                var mascota = await this.context.Mascotas.FindAsync(idMascota);
                if (mascota == null) return false;

                mascota.Vistas++;
                await this.context.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                // Opcionalmente, registrar excepción
                Console.WriteLine($"Error al incrementar vistas: {ex.Message}");
                return false;
            }
        }



        public async Task<Adoptante> GetAdoptanteByUsuarioIdAsync(int idUsuario)
        {
            return await this.context.Adoptantes.
                Include(a => a.Usuario)
               .Include(a => a.Usuario.PerfilUsuario)
               .FirstOrDefaultAsync(a => a.IdUsuario == idUsuario);
        }


        public async Task<Adoptante> GetAdoptanteChatByUsuarioId(int idusuario)
        {
            return await this.context.Adoptantes
                .Include(adoptante => adoptante.Usuario.PerfilUsuario)
                .FirstOrDefaultAsync(a => a.IdUsuario == idusuario);
        }

        public async Task<Refugio> GetRefugioChatByIdAsync(int refugioId)
        {
            return await this.context.Refugios
                .Include(refugio => refugio.Usuario.PerfilUsuario)
                .FirstOrDefaultAsync(r => r.Id == refugioId);
        }

        public async Task<Refugio> GetRefugioChatDosByIdAsync(int idusuariorefugio)
        {
            return await this.context.Refugios
                .Include(refugio => refugio.Usuario.PerfilUsuario)
                .FirstOrDefaultAsync(r => r.IdUsuario == idusuariorefugio);
        }
    }
}
