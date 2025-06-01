using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ZuvoPetApiAWS.Repositories;
using ZuvoPetApiGatewayAWS.Data;
using ZuvoPetApiGatewayAWS.Helpers;
using ZuvoPetApiGatewayAWS.Services;
using Amazon.S3;
using System.Text.Json.Serialization;
using ZuvoPetNugetAWS.Models;
using Newtonsoft.Json;

namespace ZuvoPetApiGatewayAWS;

public class Startup
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;

    public Startup(IConfiguration configuration, IWebHostEnvironment env)
    {
        _configuration = configuration;
        _env = env;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        try
        {
            // Obtener secretos de AWS de forma síncrona (solo se hace una vez al arrancar)
            var miSecretoTask = HelperSecretsManager.GetSecretAsync();
            miSecretoTask.Wait();
            KeysModel model = JsonConvert.DeserializeObject<KeysModel>(miSecretoTask.Result);

            // Usar los secretos
            string secretConnectionString = model.MySql;
            string secretAudience = model.Audience;
            string secretIssuer = model.Issuer;
            string secretSecretKey = model.SecretKey;
            string secretIterate = model.Iterate;
            string secretKey = model.Key;
            string secretSalt = model.Salt;
            string bucketName = model.BucketName;

            HelperCriptography.Initialize(secretSalt, secretIterate, secretKey);

            services.AddHttpContextAccessor();

            var helper = new HelperActionServicesOAuth(secretIssuer, secretAudience, secretSecretKey);
            services.AddSingleton(helper);
            services.AddScoped<HelperUsuarioToken>();
            services.AddAuthentication(helper.GetAuthenticateSchema())
                .AddJwtBearer(helper.GetJwtBearerOptions());

            // AWS Services
            services.AddDefaultAWSOptions(_configuration.GetAWSOptions());
            services.AddAWSService<IAmazonS3>();
            services.AddSingleton<ServiceStorageS3>();

            services.AddEndpointsApiExplorer();

            // Swagger habilitado en todos los ambientes para que funcione en Lambda
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "ZuvoPet API",
                    Version = "v1",
                    Description = "API para la gestión de ZuvoPet"
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Scheme = "oauth2",
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
            });

            services.AddTransient<IRepositoryZuvoPet, RepositoryZuvoPet>();
            services.AddDbContext<ZuvoPetContext>(options => options.UseMySQL(secretConnectionString));

            // En ConfigureServices en Startup.cs
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                    options.JsonSerializerOptions.WriteIndented = true;

                    // Estas configuraciones son necesarias para que se serialicen propiedades de solo lectura
                    options.JsonSerializerOptions.IncludeFields = true;
                    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never;

                    // Esta configuración es crítica para propiedades de solo lectura/calculadas
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

                    // AÑADIR ESTA LÍNEA para tratar propiedades calculadas correctamente
                    options.JsonSerializerOptions.IgnoreReadOnlyProperties = false;
                });


            // CORS para API Gateway
            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyMethod()
                          .AllowAnyHeader();
                });
            });
        }
        catch (Exception ex)
        {
            // Log del error para debugging
            Console.WriteLine($"Error en ConfigureServices: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        try
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // En producción (Lambda), usar manejo de errores más simple
                app.UseExceptionHandler("/error");
            }

            // Swagger configurado para funcionar con API Gateway Lambda
            app.UseSwagger(c =>
            {
                c.RouteTemplate = "swagger/{documentName}/swagger.json";
                c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
                {
                    // Configurar servidor base para API Gateway
                    swaggerDoc.Servers = new List<Microsoft.OpenApi.Models.OpenApiServer>
                    {
                        new Microsoft.OpenApi.Models.OpenApiServer
                        {
                            Url = $"{httpReq.Scheme}://{httpReq.Host.Value}/Prod"
                        }
                    };
                });
            });

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/Prod/swagger/v1/swagger.json", "ZuvoPet API v1");
                c.RoutePrefix = "swagger";
                c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
                c.DefaultModelsExpandDepth(-1);
            });

            // NO usar HTTPS redirect en Lambda - API Gateway maneja esto
            // app.UseHttpsRedirection();

            app.UseCors();
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();

                // Endpoint raíz que redirige a swagger
                endpoints.MapGet("/", async context =>
                {
                    context.Response.Redirect("/swagger");
                });

                // Endpoint de health check
                endpoints.MapGet("/health", async context =>
                {
                    await context.Response.WriteAsync("OK");
                });
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en Configure: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
            throw;
        }
    }
}