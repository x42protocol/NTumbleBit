﻿using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using NBitcoin;
using Microsoft.Extensions.Logging;
using NTumbleBit.Services;
using NTumbleBit.Logging;
using NTumbleBit.Configuration;
using NTumbleBit.ClassicTumbler.CLI;

namespace NTumbleBit.ClassicTumbler.Server.CLI
{
	public partial class Program
	{
		public static void Main(string[] args)
		{
			new Program().Run(args);
		}
		public void Run(string[] args)
		{
			NetworksSelector networkSet = x42.Networks.Networks.x42;
			var argsConf = new TextFileConfiguration(args);
			var debug = argsConf.GetOrDefault<bool>("debug", false);

			ConsoleLoggerProcessor loggerProcessor = new ConsoleLoggerProcessor();
			Logs.Configure(new FuncLoggerFactory(i => new CustomerConsoleLogger(i, Logs.SupportDebug(debug), null, loggerProcessor)));

			using(var interactive = new Interactive())
			{
				var config = new TumblerConfiguration();
				config.LoadArgs(networkSet, args);
				try
				{
					var runtime = TumblerRuntime.FromConfiguration(config, new TextWriterClientInteraction(Console.Out, Console.In));
					interactive.Runtime = new ServerInteractiveRuntime(runtime);
					StoppableWebHost host = null;
					if(!config.OnlyMonitor)
					{
						host = new StoppableWebHost(() => new WebHostBuilder()
						.UseAppConfiguration(runtime)
						.UseContentRoot(Directory.GetCurrentDirectory())
						.UseStartup<Startup>()
						.Build());
					}

					var job = new BroadcasterJob(interactive.Runtime.Services);
					job.Start();
					interactive.Services.Add(job);

					var tor = new TorRegisterJob(config, runtime);
					tor.Start();
					interactive.Services.Add(tor);

					if(!config.OnlyMonitor)
					{
						host.Start();
						interactive.Services.Add(host);
					}

					interactive.StartInteractive();
				}
				catch(ConfigException ex)
				{
					if(!string.IsNullOrEmpty(ex.Message))
						Logs.Configuration.LogError(ex.Message);
				}
				catch(InterruptedConsoleException) { }
				catch(Exception exception)
				{
					Logs.Tumbler.LogError("Exception thrown while running the server");
					Logs.Tumbler.LogError(exception.ToString());
				}
			}
		}
	}
}

