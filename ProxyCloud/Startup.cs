using System.Net;
using System.Reflection.PortableExecutable;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json.Linq;
using ProxyAPISupport;
namespace ProxyCloud
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            var entryPoint = (string)configuration.GetValue(typeof(string), "EntryPoint", null);
            var privateKey = (string)configuration.GetValue(typeof(string), "PrivateKey", null);
            var IpsEnabledToUploadUpdates = (string)configuration.GetValue(typeof(string), "IpsEnabledToUploadUpdates", null);
            AppSync.WebRepository.IpAllowedToUpload = string.IsNullOrEmpty(IpsEnabledToUploadUpdates) ? null : IpsEnabledToUploadUpdates.Split(' ').ToList();

#if DEBUG
            entryPoint = "localhost";
            var defaultPrivateKey = @"CcXdtnzt8EzdCnYfU0XrQai4Ems3GDywSkOyGXvA8pk=";
            var defaultPubblicKey = @"AgR2WgAkdJJSCIvhzriqLxAcpSOGblFcJ15KAVx/nMf6";
#elif RELEASE
            var defaultPrivateKey = @"ZYR4X4nHlvF3xRxAc8eA0aDqw9wvcqXE2dp6mvK9PR8=";
            var defaultPpubblicKey = @"AujbGstMDTBo2hAxcbXJjAdinn1X00KEdyyvDMOxt5fT";
#else
            // Key generators!
            var _mnemo = new Mnemonic(Wordlist.English, WordCount.Twelve);
            var _hdRoot = _mnemo.DeriveExtKey();
            var _privateKey = _hdRoot.PrivateKey;
            var defaultPubblicKey = Convert.ToBase64String(_privateKey.ToBytes());
            var pubblicKey = Convert.ToBase64String(_privateKey.PubKey.ToBytes());
#endif
            privateKey ??= defaultPrivateKey;

            Communication.Initialize(privateKey, entryPoint);
        }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors();
        }
        private static IHostBuilder Builder;
        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            Builder = Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>());
            return Builder;
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            AppSync.WebRepository.ContentRootPath = env.WebRootPath;
            MapEndpoints.LogRepository = Path.Combine(env.WebRootPath, "logs");


            //added to support download from wwwroot
            app.UseStaticFiles(new StaticFileOptions()
            {
                ServeUnknownFileTypes = true
            });

            app.UseCors(builder =>
              builder
             .AllowAnyOrigin()
             .AllowAnyMethod()
             .AllowAnyHeader());

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context => await MapEndpoints.Status(context));
                endpoints.MapGet("/data", async context => await MapEndpoints.DataGet(context));
                endpoints.MapPost("/data", async context => await MapEndpoints.DataPost(context));

                // Support for updating the Cloud Server application. Allows you to upload a new version of the application.
                endpoints.MapPost("/upload", async context => await AppSync.WebRepository.AppUpload(context));
            });
        }
    }
}
