using Serilog;
using StenParser;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((hostContext, services, loggerConfiguration) =>
{
    var configuration = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        .Build();

    loggerConfiguration
        .ReadFrom.Configuration(configuration);
});

Log.Logger.Information("Working directory: {WorkingDirectory}", Directory.GetCurrentDirectory());

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.Configure<StenParserOptions>(builder.Configuration);
builder.Services.AddSingleton<ParserService>();
builder.Services.AddSingleton<SerialService>();
builder.Services.AddSingleton<IHostedService, SerialService>(sp => sp.GetService<SerialService>()!);


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    //app.UseHsts();
}

//app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
