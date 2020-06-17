using Microsoft.AspNetCore.Hosting;
using TCPServer;
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc;
namespace NTumbleBit.ClassicTumbler.Server
{
    public class ActionResultException : Exception
    {
        public ActionResultException(IActionResult result)
        {
            _Result = result ?? throw new ArgumentNullException(nameof(result));
        }

        private readonly IActionResult _Result;
        public IActionResult Result
        {
            get
            {
                return _Result;
            }
        }
    }
    public static class Extensions
    {
        //So IDispose on the CustomThreadPool get called
        class ThreadPoolWrapper : IDisposable
        {
            public ThreadPoolWrapper()
            {
                ThreadPool = new CustomThreadPool(30, "MainController Processor");
            }
            public CustomThreadPool ThreadPool
            {
                get; set;
            }

            public void Dispose()
            {
                ThreadPool.Dispose();
            }
        }
        public static ActionResultException AsException(this IActionResult actionResult)
        {
            return new ActionResultException(actionResult);
        }
        public static IWebHostBuilder UseAppConfiguration(this IWebHostBuilder builder, TumblerRuntime runtime)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(provider =>
                 {
                     return runtime;
                 });
                services.AddSingleton<ThreadPoolWrapper>();
                services.AddSingleton<CustomThreadPool>(s => s.GetRequiredService<ThreadPoolWrapper>().ThreadPool);
            });
            builder.UseTCPServer(new ServerOptions(runtime.LocalEndpoint)
            {
                IncludeHeaders = false
            });
            return builder;
        }
    }
}

