using MAQ_API.Data;
using MAQ_API.Options;
using MAQ_API.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0, 36))));
builder.Services.AddScoped<ITempahanRamadanService, TempahanRamadanService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();
builder.Services.AddScoped<INewsService, NewsService>();

builder.Services.AddOptions<HerepayOptions>()
    .Bind(builder.Configuration.GetSection(HerepayOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<IHerepayClient, HerepayClient>((sp, client) =>
{
    var opt = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<HerepayOptions>>().Value;
    client.BaseAddress = new Uri(opt.BaseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("SecretKey", opt.SecretKey);
    client.DefaultRequestHeaders.Add("XApiKey", opt.ApiKey);
    client.Timeout = TimeSpan.FromSeconds(4); // callback must answer within ~5 s
});
builder.Services.AddScoped<IPaymentService, PaymentService>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// Configure the HTTP request pipeline.

    app.UseSwagger();
    app.UseSwaggerUI();

app.UseCors("Frontend");


app.UseHttpsRedirection();

app.MapControllers();

app.Run();
