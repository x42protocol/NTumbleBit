﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Diagnostics;
using NTumbleBit.Logging;
using System.Net;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace NTumbleBit.ClassicTumbler.Server
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit http://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IObjectModelValidator, NoObjectModelValidator>();
            services.AddMvcCore(o =>
            {
                o.Filters.Add(new ActionResultExceptionFilter());
                o.Filters.Add(new TumblerExceptionFilter());
                o.InputFormatters.Add(new BitcoinInputFormatter());
                o.OutputFormatters.Add(new BitcoinOutputFormatter());
                o.EnableEndpointRouting = false;
            });

            services.AddLogging(o =>
            {
                o.AddFilter("Microsoft.AspNetCore.Hosting.Internal.WebHost", LogLevel.Error);
                o.AddFilter("Microsoft.AspNetCore.Mvc", LogLevel.Error);
                o.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Error);
                o.AddFilter("TCPServer", LogLevel.Error);
                o.AddConsole();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IServiceProvider serviceProvider)
        {
            app.Use(req =>
            {
                return async (ctx) =>
                {
                    DateTimeOffset before = DateTimeOffset.UtcNow;
                    try
                    {
                        await req(ctx);
                    }
                    catch (Exception ex)
                    {
                        Logs.Tumbler.LogCritical(new EventId(), ex, "Unhandled exception thrown by the Tumbler Service");
                        if (ex is WebException webEx)
                        {
                            try
                            {
                                var httpResp = ((HttpWebResponse)webEx.Response);
                                var reader = new StreamReader(httpResp.GetResponseStream(), Encoding.UTF8);
                                Logs.Tumbler.LogCritical($"Web Exception {(int)httpResp.StatusCode} {reader.ReadToEnd()}");
                            }
                            catch { }
                        }
                        throw;
                    }
                    finally
                    {
                        var timeSpent = DateTimeOffset.UtcNow - before;
                        var timeSpentStr = $"{timeSpent.TotalSeconds.ToString("0.00")} seconds spent on {ctx.Request.Path}";
                        if (timeSpent > TimeSpan.FromMinutes(1.0))
                        {
                            Logs.Tumbler.LogCritical("Overload detected: " + timeSpentStr);
                        }
                        else
                        {
                            Logs.Tumbler.LogDebug(timeSpentStr);
                        }
                    }
                };
            });

            app.UseMvc();


            var builder = serviceProvider.GetService<ConfigurationBuilder>() ?? new ConfigurationBuilder();
            Configuration = builder.Build();

            var config = serviceProvider.GetService<TumblerRuntime>();
            var options = GetMVCOptions(serviceProvider);
            Serializer.RegisterFrontConverters(options, config.Network);
        }


        public IConfiguration Configuration
        {
            get; set;
        }

        private static JsonSerializerSettings GetMVCOptions(IServiceProvider serviceProvider)
        {
            return serviceProvider.GetRequiredService<IOptions<JsonSerializerSettings>>().Value;
        }
    }

    internal class NoObjectModelValidator : IObjectModelValidator
    {
        public void Validate(ActionContext actionContext, ValidationStateDictionary validationState, string prefix, object model)
        {

        }
    }
}
