// AgricHub.API/Program.cs
using AgricHub.API.Extension;
using AgricHub.BLL.Implementations.BusinessServices;
using AgricHub.Contracts;
using AgricHub.DAL;
using AgricHub.DAL.Context;
using AgricHub.DAL.Context.Seeders;
using AgricHub.DAL.Seeders;
using AgricHub.Presentation.Filters;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.FileProviders;
using Microsoft.OpenApi.Models;
using System.Reflection;
var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureCors();
builder.Services.ConfigureIISIntegration();
builder.Services.Configure<FormOptions>(o =>
{
    o.ValueLengthLimit         = int.MaxValue;
    o.MultipartBodyLengthLimit = int.MaxValue;
    o.MemoryBufferThreshold    = int.MaxValue;
});
builder.Services.ConfigureIdentity();
builder.Services.ConfigureEmail(builder.Configuration);
builder.Services.ConfigureJWT(builder.Configuration);
builder.Services.ConfigureSqlContext(builder.Configuration);
builder.Services.AddScoped<ValidationFilterAttribute>();
builder.Services.AddCors(o => o.AddPolicy("Angular",
    p => p.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddControllers()
    .AddApplicationPart(typeof(AgricHub.Presentation.AssemblyReference).Assembly);
builder.Services.AddSwaggerGen(c =>
{
    c.EnableAnnotations();
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "AgricHub", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.ApiKey,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "JWT Authorization header using the Bearer scheme.\r\n\r\nEnter 'Bearer' [space] and then your token.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\""
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddScoped<IUnitOfWork, UnitOfWork<AgricHubDbContext>>();
// ← Pass configuration so Cloudinary / storage can be registered conditionally
builder.Services.ConfigureServices(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddAutoMapper(Assembly.Load("AgricHub.BLL"));

// Periodically sweeps expired no-show grace periods and pending-approval
// windows (auto-release escrow) — runs every 5 minutes for the app's lifetime.
builder.Services.AddHostedService<ExpirySweepService>();

var app = builder.Build();
// ── Seeders ────────────────────────────────────────────────────────────────────
await AdminSeeder.SeedAsync(app.Services);
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgricHubDbContext>();
    await PlatformSettingsSeeder.SeedAsync(db);
}
// ── Middleware ─────────────────────────────────────────────────────────────────
app.ConfigureExceptionHandler();
app.UseCors("Angular");
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(Directory.GetCurrentDirectory(), "Resources")),
    RequestPath  = new PathString("/Resources")
});
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.All
});
app.UseAuthentication();
/* app.UseHttpsRedirection(); */
app.UseAuthorization();
app.MapControllers();
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "AgricHub v1");
});
app.Run();