using LPS.Server.WebManager.Services;
using LPS.Common.Debug;
using LPS.Server.MessageQueue;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins("https://localhost:7087", "https://localhost:44403");
    });
});

Logger.Init("web_manager");

var mqConfig = JToken.ReadFrom(new JsonTextReader(new StreamReader("./Config/mq_conf.json"))).ToObject<MessageQueueClient.MqConfig>() !;
MessageQueueClient.InitConnectionFactory(mqConfig);

var serverService = new ServerService();
serverService.Init();
builder.Services.AddSingleton(serverService);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseCors();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapFallbackToFile("index.html");

app.Run();