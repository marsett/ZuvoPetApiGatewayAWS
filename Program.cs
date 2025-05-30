using Microsoft.EntityFrameworkCore;
using ZuvoPetApiAWS.Repositories;
using ZuvoPetApiGatewayAWS.Data;
using System.Text.Json.Serialization;
using Scalar.AspNetCore;  // Remove the duplicate below
using ZuvoPetApiGatewayAWS.Helpers;
using ZuvoPetApiGatewayAWS.Services;
using Amazon.S3;
using Newtonsoft.Json;
using ZuvoPetNugetAWS.Models;
// using Scalar.AspNetCore;  // This is a duplicate

var builder = WebApplication.CreateBuilder(args);

// Remove Azure Key Vault registration and usage

// Get all secrets from AWS
string miSecreto = await HelperSecretsManager.GetSecretAsync();
KeysModel model = JsonConvert.DeserializeObject<KeysModel>(miSecreto);

// Use AWS secrets
string secretConnectionString = model.MySql;
string secretAudience = model.Audience;
string secretIssuer = model.Issuer;
string secretSecretKey = model.SecretKey;
string secretIterate = model.Iterate;
string secretKey = model.Key;
string secretSalt = model.Salt;
string bucketName = model.BucketName; // If you need it

HelperCriptography.Initialize(
    secretSalt,
    secretIterate,
    secretKey
);
builder.Services.AddHttpContextAccessor();

HelperActionServicesOAuth helper = new HelperActionServicesOAuth(
    secretIssuer,
    secretAudience,
    secretSecretKey
);
builder.Services.AddSingleton<HelperActionServicesOAuth>(helper);
builder.Services.AddScoped<HelperUsuarioToken>();
builder.Services.AddAuthentication(helper.GetAuthenticateSchema())
    .AddJwtBearer(helper.GetJwtBearerOptions());

builder.Services.AddAWSService<IAmazonS3>();
builder.Services.AddSingleton<ServiceStorageS3>();


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

string connectionString = secretConnectionString;
builder.Services.AddTransient<IRepositoryZuvoPet, RepositoryZuvoPet>();
builder.Services.AddDbContext<ZuvoPetContext>
    (options => options.UseMySQL(connectionString));
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = true;
    });

var app = builder.Build();

// The order of middleware is important
app.UseHttpsRedirection();

// Enable Swagger in all environments
app.UseSwagger();
app.UseSwaggerUI();

// Authentication should come before authorization
app.UseAuthentication();
app.UseAuthorization();

// Map controllers before Scalar
app.MapControllers();
app.MapScalarApiReference();

// This should be at the end
app.MapGet("/", context => {
    context.Response.Redirect("/swagger");
    return Task.CompletedTask;
});

app.Run();