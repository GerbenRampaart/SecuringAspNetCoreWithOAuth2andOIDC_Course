using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using ImageGallery.Client.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using System.Security.Claims;
using System.Linq;

namespace ImageGallery.Client
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();

            // register an IHttpContextAccessor so we can access the current
            // HttpContext in services by injecting it
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // register an IImageGalleryHttpClient
            services.AddScoped<IImageGalleryHttpClient, ImageGalleryHttpClient>();

            //https://www.codeproject.com/Articles/1205745/Identity-Server-with-ASP-NET-Core

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(options => {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.LoginPath;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(options =>
            {
                options.Authority = "https://localhost:44389/";
                options.RequireHttpsMetadata = true;
                options.ClientId = "ImageGalleryClient";

                // https://leastprivilege.com/2017/11/15/missing-claims-in-the-asp-net-core-2-openid-connect-handler/
                options.Scope.Clear();
                options.Scope.Add("openid");
                options.Scope.Add("profile");

                options.ResponseType = "code id_token";
                //options.CallbackPath = new PathString("...");
                //options.SignedOutCallbackPath = new PathString("...");
                options.SignInScheme = "Cookies";
                options.SaveTokens = true;
                options.ClientSecret = "secret";
                options.GetClaimsFromUserInfoEndpoint = true;
                options.Events = new OpenIdConnectEvents()
                {
                    OnTokenValidated = tokenValidatedContext =>
                    {
                        var identity = tokenValidatedContext.Principal.Identity as ClaimsIdentity;
                        var subjectClaim = identity.Claims.FirstOrDefault(z => z.Type == "sub");

                        // With help from:
                        // https://app.pluralsight.com/library/courses/asp-dotnet-core-oauth2-openid-connect-securing/discussion
                        var newClaimsIdentity = new ClaimsIdentity(
                            tokenValidatedContext.Principal.Identity.AuthenticationType, // <-- I think this was the part that took me a while to get.
                            "given_name",
                            "role");

                        newClaimsIdentity.AddClaim(subjectClaim);
                        // https://app.pluralsight.com/library/courses/asp-dotnet-core-oauth2-openid-connect-securing/discussion

                        // https://github.com/fhrn71/IdentityServer4Demo/blob/master/ImageGallery.Client/Startup.cs
                        tokenValidatedContext.Principal = new ClaimsPrincipal(newClaimsIdentity);


                        return Task.CompletedTask;
                    },

                    OnUserInformationReceived = userInformationReceivedContext =>
                    {
                        return Task.FromResult(0);
                    }
                };
            });

        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Shared/Error");
            }

            app.UseStaticFiles();
            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Gallery}/{action=Index}/{id?}");
            });
        }
    }
}
