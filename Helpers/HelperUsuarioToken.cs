using Newtonsoft.Json;
using System.Security.Claims;
using ZuvoPetNugetAWS.Dtos;

namespace ZuvoPetApiGatewayAWS.Helpers
{
    public class HelperUsuarioToken
    {
        private IHttpContextAccessor contextAccessor;

        public HelperUsuarioToken(IHttpContextAccessor contextAccessor)
        {
            this.contextAccessor = contextAccessor;
        }

        public UsuarioTokenDTO GetUsuario()
        {
            // Busca el claim específico "UserData" donde guardaste la info del usuario
            Claim claim = this.contextAccessor.HttpContext?
                .User.FindFirst(x => x.Type == "UserData");

            if (claim == null)
            {
                return null;
            }

            // Obtiene el valor del claim (JSON cifrado)
            string json = claim.Value;

            // Descifra el JSON usando tu helper existente
            string jsonUsuario = HelperCriptography.DecryptString(json);

            // Convierte el JSON a un objeto UsuarioTokenModel
            UsuarioTokenDTO model = JsonConvert.DeserializeObject<UsuarioTokenDTO>(jsonUsuario);
            return model;
        }

        public int GetAuthenticatedUserId()
        {
            var usuario = GetUsuario();
            return usuario.IdUsuario;
        }
    }
}
