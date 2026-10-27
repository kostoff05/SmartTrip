using Microsoft.EntityFrameworkCore;
using SmartTrip.Data;
using SmartTrip.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// SQLite
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Кеширане
builder.Services.AddMemoryCache();

// Weather API
builder.Services.AddHttpClient<WeatherService>();

// Gemini AI
builder.Services.AddHttpClient<AiService>();

// OpenStreetMap
builder.Services.AddHttpClient<OpenStreetMapService>(
    client =>
    {
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "SmartTripUniversityProject/1.0 (contact: kostov2005@abv.bg)");

        client.Timeout = TimeSpan.FromSeconds(60);
    });

var app = builder.Build();

// HTTP pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();