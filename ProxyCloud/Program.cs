using ProxyCloud;
var builder = Startup.CreateHostBuilder(args);
var app = builder.Build();

// NOTE: If raise a error try to run in IIS Express
app.Run();
