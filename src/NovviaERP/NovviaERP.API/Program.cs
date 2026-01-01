using NovviaERP.Core.Data;
using NovviaERP.Core.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<JtlDbContext>();

// Services registrieren
var connectionString = builder.Configuration.GetConnectionString("JtlDatabase")
    ?? "Server=24.134.81.65,2107\\NOVVIAS05;Database=Mandant_2;Integrated Security=true;TrustServerCertificate=true";
builder.Services.AddSingleton(sp => new EdifactService(connectionString));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true, ValidateIssuerSigningKey = true,
            ValidIssuer = "NovviaERP", ValidAudience = "NovviaERP",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("YourSuperSecretKeyHere32Chars!!"))
        };
    });
builder.Services.AddCors(o => o.AddPolicy("All", p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseCors("All");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
