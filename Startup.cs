using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using ZuvoPetApiAWS.Repositories;
using ZuvoPetApiGatewayAWS.Data;
using ZuvoPetApiGatewayAWS.Helpers;
using ZuvoPetApiGatewayAWS.Services;
using Amazon.S3;
using Scalar.AspNetCore;
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

        services.AddAWSService<IAmazonS3>();
        services.AddSingleton<ServiceStorageS3>();

        services.AddEndpointsApiExplorer();

        // Swagger + JWT
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
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                options.JsonSerializerOptions.WriteIndented = true;
            });
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHttpsRedirection();

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "ZuvoPet API v1");
            c.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
            c.DefaultModelsExpandDepth(-1);
        });

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", async context =>
            {
                context.Response.Redirect("/swagger");
                await Task.CompletedTask;
            });
        });
    }
}
