using GalgameManager.Server.Data;
using Microsoft.EntityFrameworkCore;

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
        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

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
        return result;
    }
}
