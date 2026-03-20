using PokerFace.Api.Hubs;
using PokerFace.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();
builder.Services.AddSingleton<AppStatsService>();
builder.Services.AddSingleton<ITableService, TableService>();

// CORS is needed if Client and API are on different ports
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("https://localhost:7000", "http://localhost:5000", "https://localhost:7001", "http://localhost:5001", "https://localhost:7293", "https://pokerplanning.it", "http://pokerplanning.it")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthorization();

app.MapControllers();
app.MapHub<PokerHub>("/pokerhub");

app.Run();
