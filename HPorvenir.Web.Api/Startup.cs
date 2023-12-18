using Dapper.Logging;
using HPorvenir.Authentication;
using HPorvenir.Storage;
using HPorvenir.User.DAL;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System;
using System.Data.SqlClient;
using System.Text;

namespace HPorvenir.Web.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            var mySecret = "asdv234234^&%&^%&^hjsdfb2%%%";
            
            var key = Encoding.ASCII.GetBytes(mySecret);           
            var mySecurityKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(mySecret));

            services.AddScoped<IStorage,BlobStorage>();
            services.AddScoped<AuthManager, AuthManager>();
            services.AddScoped<UserDAL, UserDAL>();

            services.AddCors(options =>
            {
                options.AddPolicy(name: "all",
                                  builder =>
                                  {
                                      builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();

                                  });
            });

            services.AddDbConnectionFactory(prv => new SqlConnection(Configuration.GetConnectionString("PorvenirDatabase")));

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                 .AddJwtBearer(x =>
                 {
                     x.RequireHttpsMetadata = false;
                     x.SaveToken = true;
                     x.TokenValidationParameters = new TokenValidationParameters
                     {
                         ValidateIssuerSigningKey = false,
                         IssuerSigningKey = mySecurityKey,
                         ValidateIssuer = false,
                         ValidateAudience = false,
                         ValidateLifetime = true,
                         // set clockskew to zero so tokens expire exactly at token expiration time (instead of 5 minutes later)
                         ClockSkew = TimeSpan.Zero
                     };
                     x.IncludeErrorDetails = true;
                     x.Events = new JwtBearerEvents()
                     {
                         OnAuthenticationFailed = context =>
                         {
                             //context.Response.StatusCode = HttpStatusCodes.AuthenticationFailed;
                             context.Response.ContentType = "application/json";
                             //var err = this.Environment.IsDevelopment() ? context.Exception.ToString() : "An error occurred processing your authentication.";
                             //var result = JsonConvert.SerializeObject(new { err });
                             //return context.Response.wri;
                             return null;
                         }
                         
                     };
                 });


            services.AddApplicationInsightsTelemetry(Configuration["APPINSIGHTS_CONNECTIONSTRING"]);
            services.AddControllers().AddNewtonsoftJson();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Your API", Version = "v1" });
            });


        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseSwagger();
            app.UseHttpsRedirection();            
            app.UseRouting();
            app.UseCors("all");           
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedProto
            });
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Your API V1");
                // The Swagger UI is available at /swagger/index.html
            });
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            


        }
    }
}
