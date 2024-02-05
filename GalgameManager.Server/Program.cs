using System.Text;
using GalgameManager.Server.Contracts;
using GalgameManager.Server.Data;
using GalgameManager.Server.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
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
            options.UseMySQL(builder.Configuration.GetConnectionString("DefaultConnection")!);
        });
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddControllers();
        
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

        app.UseAuthorization();


        app.MapControllers();

        app.Run();
    }

    private static bool CheckEnv(WebApplicationBuilder builder)
    {
        var result = true;
        if (string.IsNullOrEmpty(builder.Configuration.GetConnectionString("DefaultConnection")!))
        {
            Console.WriteLine("Connection string is not set.");
            result = false;
        }
        if (string.IsNullOrEmpty(builder.Configuration["AppSettings:JwtKey"]))
        {
            Console.WriteLine("JwtKey is not set.");
            result = false;
        }
        return result;
    }
}
