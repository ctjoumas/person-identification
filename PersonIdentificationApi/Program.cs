using PersonIdentification.FaceService;
using PersonIdentificationApi.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args);

builder.Services.AddScoped<IFaceService, FaceService>();
builder.Services.AddScoped<ISegmentation, Segmentation>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.AddConsole(); // Add the Console provider

// Register the PersonGroupRepository with dependency injection
#pragma warning disable CS8604 // Possible null reference argument.
builder.Services.AddScoped<PersonGroupRepository>(provider =>
    new PersonGroupRepository(builder.Configuration.GetConnectionString("DefaultConnection")));
#pragma warning restore CS8604 // Possible null reference argument.

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
        c.RoutePrefix = "";
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();