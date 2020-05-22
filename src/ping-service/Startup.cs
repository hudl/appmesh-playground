using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ping_service
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            var dnsNamespace = Environment.GetEnvironmentVariable("APPMESH_NAMESPACE");
            services.AddHttpClient("pong-service", configuration =>
            {
                configuration.BaseAddress = new Uri($"http://pong-service.{dnsNamespace}:5000");
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();
            var clientFactory = app.ApplicationServices.GetRequiredService<IHttpClientFactory>();
            var pongClient = clientFactory.CreateClient("pong-service");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    try
                    {
                        var branchHeader = context.Request.Headers.TryGetValue("x-branch-header", out var values);
                        var request = new HttpRequestMessage(HttpMethod.Get, "/");
                        if (values.Any()) request.Headers.Add("x-branch-header", values.AsEnumerable());
                        var response = await pongClient.SendAsync(request);
                        if (response.IsSuccessStatusCode)
                        {
                            await context.Response.WriteAsync($"{await response.Content.ReadAsStringAsync()}");
                        }
                        else
                        {
                            await context.Response.WriteAsync($"ERROR Calling pong-service: {response.StatusCode}");
                        }
                    }
                    catch (Exception e)
                    {
                        await context.Response.WriteAsync($"EXCEPTION: {e.Message}");
                    }
                });
            });
        }
    }
}
