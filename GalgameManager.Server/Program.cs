using System.Text;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Helpers;
using GalgameManager.Server.Repositories;
using GalgameManager.Server.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using Swashbuckle.AspNetCore.Filters;

namespace GalgameManager.Server;

// ReSharper disable once ClassNeverInstantiated.Global
public class Program
{
    public static void Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        if (CheckEnv(builder) == false)
        {
            Console.WriteLine("Environment is not set correctly. Please check your environment variables. Exiting...");
            return;
        }

        // Add services to the container.
        builder.Services.AddDbContext<DataContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")!);
        });
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<IGalgameRepository, GalgameRepository>();
        builder.Services.AddScoped<IGalgameDeletedRepository, GalgameDeletedRepository>();
        builder.Services.AddScoped<IPlayLogRepository, PlayLogRepository>();
        builder.Services.AddScoped<IOssRecordRepository, OssRecordRepository>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IOssService, OssService>();
        builder.Services.AddScoped<IBangumiService, BangumiService>();
        builder.Services.AddScoped<IGalgameService, GalgameService>();
        builder.Services.AddMinio(client =>
        {
            client.WithEndpoint(builder.Configuration["AppSettings:Minio:EndPoint"])
                .WithCredentials(
                    builder.Configuration["AppSettings:Minio:AccessKey"], 
                    builder.Configuration["AppSettings:Minio:SecretKey"])
                .WithSSL(Convert.ToBoolean(builder.Configuration["AppSettings:Minio:UseSSL"] ?? "False"));
        });
        builder.Services.AddControllers(options =>
        {
            options.Conventions.Add(new RouteConvention());
        });
        
        // Enable logging
        builder.Logging.AddConsole();
        
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Name = "Authorization",
                Type = SecuritySchemeType.ApiKey,
            });
                
            options.OperationFilter<SecurityRequirementsOperationFilter>();
                
            options.SwaggerDoc("v1", new OpenApiInfo {Title = "PotatoVN.Server", Version = "v1"});
            options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "PotatoVN.Server.xml"));
        });
        builder.Services.AddAuthentication().AddJwtBearer(x =>
        {
            x.TokenValidationParameters = new TokenValidationParameters
            {
                IssuerSigningKey =
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["AppSettings:JwtKey"]!)),
                ValidateIssuerSigningKey = true,
                ValidateAudience = false,
                ValidateIssuer = false,
            };
        });
        builder.Services.AddCors(x =>
        {
            x.AddPolicy("AllowAll", corsPolicyBuilder =>
            {
                corsPolicyBuilder.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader();
            });
        });

        WebApplication app = builder.Build();
        
        // DataBase Migration
        using (IServiceScope scope = app.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<DataContext>().Database.Migrate();
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment()) 
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseCors("AllowAll");
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }

    private static bool CheckEnv(WebApplicationBuilder builder)
    {
        var result = true;
        result = Check("ConnectionStrings:DefaultConnection") && result;
        result = Check("AppSettings:JwtKey") && result;
        result = Check("AppSettings:Minio:EndPoint") && result;
        result = Check("AppSettings:Minio:AccessKey") && result;
        result = Check("AppSettings:Minio:SecretKey") && result;
        result = Check("AppSettings:Minio:EventToken") && result;
        
        result = CheckBoolValue("AppSettings:Minio:UseSSL", out _) && result;
        result = CheckBoolValue("AppSettings:Bangumi:OAuth2Enable", out var isBgmOAuth2Enable) && result;
        if (isBgmOAuth2Enable)
        { 
            result = Check("AppSettings:Bangumi:AppId") && result;
            result = Check("AppSettings:Bangumi:AppSecret") && result;
            result = Check("AppSettings:Bangumi:RedirectUri") && result;
        }
        result = CheckBoolValue("AppSettings:User:Bangumi", out _) && result;
        result = CheckBoolValue("AppSettings:User:Default", out _) && result;
        result = CheckLongValue("AppSettings:User:OssSize", out _) && result;
        
        return result;

        bool Check(string key)
        {
            if (string.IsNullOrEmpty(builder.Configuration[key]))
            {
                Console.WriteLine($"{key} is not set.");
                return false;
            }
            return true;
        }

        bool CheckBoolValue(string key, out bool value)
        {
            if (string.IsNullOrEmpty(builder.Configuration[key]) == false && 
                bool.TryParse(builder.Configuration[key], out _) == false)
            {
                Console.WriteLine($"{key} is is not a valid boolean value.");
                value = false;
                return false;
            }
            value = Convert.ToBoolean(builder.Configuration[key]);
            return true;
        }

        bool CheckLongValue(string key, out long value)
        {
            if (string.IsNullOrEmpty(builder.Configuration[key]) == false &&
                long.TryParse(builder.Configuration[key], out _) == false)
            {
                Console.WriteLine($"{key} is is not a valid number(long) value.");
                value = 0;
                return false;
            }
            value = Convert.ToInt64(builder.Configuration[key]);
            return true;
        }
    }
}
