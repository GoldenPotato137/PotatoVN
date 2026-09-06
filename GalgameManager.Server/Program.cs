using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Helpers;
using GalgameManager.Server.Repositories;
using GalgameManager.Server.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Minio;
using Swashbuckle.AspNetCore.Filters;

namespace GalgameManager.Server;

// ReSharper disable once ClassNeverInstantiated.Global
public class Program
{
    public static string Version { get; } = Assembly.GetExecutingAssembly()
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0";

    /// <summary>搜刮器代理端点的限流策略名</summary>
    public const string PhraserRateLimitPolicy = "phraser";

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
        builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
        builder.Services.AddScoped<IStaffRepository, StaffRepository>();
        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<IOssService, OssService>();
        builder.Services.AddScoped<IBangumiService, BangumiService>();
        builder.Services.AddScoped<IGalgameService, GalgameService>();
        builder.Services.AddScoped<IStaffService, StaffService>();
        builder.Services.AddSingleton<IHikarinagiService, HikarinagiService>();
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
        // .AddJsonOptions(options =>
        // {
        //     options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        // });
        builder.Services.AddAutoMapper(_ => { }, typeof(Program));

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

            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "PotatoVN.Server", Version = $"v{Version}",
                Description = "PotatoVN 同步服务器\n最新更新：galgame新增playcount字段",
            });
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
        // 搜刮器代理端点限流：未登录用户按IP限速360次/分钟，登录用户不限速
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PhraserRateLimitPolicy, context =>
            {
                if (context.User.Identity?.IsAuthenticated == true)
                    return RateLimitPartition.GetNoLimiter("authenticated");
                var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 360,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                });
            });
        });

        WebApplication app = builder.Build();

        // DataBase Migration
        using (IServiceScope scope = app.Services.CreateScope())
        {
            try
            {
                scope.ServiceProvider.GetRequiredService<DataContext>().Database.Migrate();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to migrate database:\n{e.Message}{e.StackTrace}");
                return;
            }

        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseCors("AllowAll");
        app.UseAuthentication();
        app.UseRateLimiter();
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

        result = CheckBoolValue("AppSettings:Minio:UseSSL", out _) && result;
        result = CheckBoolValue("AppSettings:Bangumi:OAuth2Enable", out var isBgmOAuth2Enable) && result;
        if (isBgmOAuth2Enable)
        {
            result = Check("AppSettings:Bangumi:AppId") && result;
            result = Check("AppSettings:Bangumi:AppSecret") && result;
            result = Check("AppSettings:Bangumi:RedirectUri") && result;
        }
        result = CheckBoolValue("AppSettings:Hikarinagi:Enable", out var isHikarinagiEnable) && result;
        result = CheckBoolValue("AppSettings:Hikarinagi:OAuth2Enable", out var isHikarinagiOAuth2Enable) && result;
        if (isHikarinagiEnable || isHikarinagiOAuth2Enable)
        {
            result = Check("AppSettings:Hikarinagi:ClientId") && result;
            result = Check("AppSettings:Hikarinagi:ClientSecret") && result;
        }
        if (isHikarinagiOAuth2Enable)
            result = Check("AppSettings:Hikarinagi:RedirectUri") && result;
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
