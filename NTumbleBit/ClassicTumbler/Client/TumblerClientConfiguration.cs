﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.IO;
using NBitcoin;
using Microsoft.Extensions.Logging;
using NTumbleBit.Logging;
using System.Net;
using NTumbleBit.Configuration;
using System.Net.Sockets;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NTumbleBit.ClassicTumbler.Client.ConnectionSettings;

namespace NTumbleBit.ClassicTumbler.Client
{
	public class OutputWalletConfiguration
	{
		public BitcoinExtPubKey RootKey
		{
			get; set;
		}

		public KeyPath KeyPath
		{
			get; set;
		}

		public RPCArgs RPCArgs
		{
			get; set;
		}
	}

	public class TumblerClientConfiguration
	{
		public string ConfigurationFile
		{
			get;
			set;
		}
		public string DataDir
		{
			get;
			set;
		}

		public Network Network
		{
			get; set;
		}

		public bool OnlyMonitor
		{
			get; set;
		}

		public bool CheckIp
		{
			get; set;
		} = true;

		public bool Cooperative
		{
			get;
			set;
		}
		public TumblerUrlBuilder TumblerServer
		{
			get;
			set;
		}

		public ConnectionSettingsBase BobConnectionSettings
		{
			get; set;
		} = new ConnectionSettingsBase();

		public ConnectionSettingsBase AliceConnectionSettings
		{
			get; set;
		} = new ConnectionSettings.ConnectionSettingsBase();

		public OutputWalletConfiguration OutputWallet
		{
			get; set;
		} = new OutputWalletConfiguration();

		public RPCArgs RPCArgs
		{
			get; set;
		} = new RPCArgs();
		public bool AllowInsecure
		{
			get;
			set;
		} = false;
		public string TorPath
		{
			get;
			set;
		}
		public bool TorMandatory
		{
			get;
			set;
		} = true;

		public TumblerClientConfiguration LoadArgs(NetworksSelector networkSet, String[] args)
		{
			ConfigurationFile = args.Where(a => a.StartsWith("-conf=", StringComparison.Ordinal)).Select(a => a.Substring("-conf=".Length).Replace("\"", "")).FirstOrDefault();
			DataDir = args.Where(a => a.StartsWith("-datadir=", StringComparison.Ordinal)).Select(a => a.Substring("-datadir=".Length).Replace("\"", "")).FirstOrDefault();
			if(DataDir != null && ConfigurationFile != null)
			{
				var isRelativePath = Path.GetFullPath(ConfigurationFile).Length > ConfigurationFile.Length;
				if(isRelativePath)
				{
					ConfigurationFile = Path.Combine(DataDir, ConfigurationFile);
				}
			}

			Network = args.Contains("-testnet", StringComparer.OrdinalIgnoreCase) ? networkSet.Testnet() :
				args.Contains("-regtest", StringComparer.OrdinalIgnoreCase) ? networkSet.Regtest() :
				networkSet.Mainnet();

			if (ConfigurationFile != null)
			{
				AssetConfigFileExists();
				var configTemp = TextFileConfiguration.Parse(File.ReadAllText(ConfigurationFile));
				Network = configTemp.GetOrDefault<bool>("testnet", false) ? networkSet.Testnet() :
					configTemp.GetOrDefault<bool>("regtest", false) ? networkSet.Regtest() :
					networkSet.Mainnet();
			}

			if (DataDir == null)
			{
				DataDir = DefaultDataDirectory.GetDefaultDirectory("NTumbleBit", Network);
			}

			if(ConfigurationFile == null)
			{
				ConfigurationFile = GetDefaultConfigurationFile(DataDir, Network);
			}
			Logs.Configuration.LogInformation("Network: " + Network);

			Logs.Configuration.LogInformation("Data directory set to " + DataDir);
			Logs.Configuration.LogInformation("Configuration file set to " + ConfigurationFile);

			if(!Directory.Exists(DataDir))
				throw new ConfigurationException("Data directory does not exists");

			var consoleConfig = new TextFileConfiguration(args);
			var config = TextFileConfiguration.Parse(File.ReadAllText(ConfigurationFile));
			consoleConfig.MergeInto(config, true);
			config.AddAlias("server", "tumbler.server");

			OnlyMonitor = config.GetOrDefault<bool>("onlymonitor", false);
			Cooperative = config.GetOrDefault<bool>("cooperative", true);
			TumblerServer = config.GetOrDefault("tumbler.server", null as TumblerUrlBuilder);
			TorPath = config.GetOrDefault<string>("torpath", "tor");

			RPCArgs = RPCArgs.Parse(config, Network);

			if(!OnlyMonitor && TumblerServer == null)
				throw new ConfigException("tumbler.server not configured");

			try
			{
				var key = config.GetOrDefault("outputwallet.extpubkey", null as string);
				if(key != null)
					OutputWallet.RootKey = new BitcoinExtPubKey(key, Network);
			}
			catch
			{
				throw new ConfigException("outputwallet.extpubkey is not configured correctly");
			}

			OutputWallet.KeyPath = new KeyPath("0");
			string keyPathString = config.GetOrDefault("outputwallet.keypath", null as string);
			if(keyPathString != null)
			{
				try
				{
					OutputWallet.KeyPath = new KeyPath(keyPathString);
				}
				catch
				{
					throw new ConfigException("outputwallet.keypath is not configured correctly");
				}
			}

			if(OutputWallet.KeyPath.ToString().Contains("'"))
				throw new ConfigException("outputwallet.keypath should not contain any hardened derivation");

			if(OutputWallet.RootKey != null && OutputWallet.RootKey.Network != Network)
				throw new ConfigException("outputwallet.extpubkey is pointing an incorrect network");

			OutputWallet.RPCArgs = RPCArgs.Parse(config, Network, "outputwallet");

			AliceConnectionSettings = ConnectionSettingsBase.ParseConnectionSettings("alice", config);
			BobConnectionSettings = ConnectionSettingsBase.ParseConnectionSettings("bob", config);

			AllowInsecure = config.GetOrDefault<bool>("allowinsecure", IsTest(Network));
			return this;
		}

		private bool IsTest(Network network)
		{
			return network.NetworkType == NetworkType.Regtest || network.NetworkType == NetworkType.Testnet;
		}

		public static string GetDefaultConfigurationFile(string dataDirectory, Network network)
		{
			var config = Path.Combine(dataDirectory, "client.config");
			Logs.Configuration.LogInformation("Configuration file set to " + config);
			if(!File.Exists(config))
			{
				Logs.Configuration.LogInformation("Creating configuration file");
				StringBuilder builder = new StringBuilder();
				builder.AppendLine("####Common Commands####");
				builder.AppendLine("#Connection to the input wallet. TumbleBit.CLI will try to autoconfig based on default settings of Bitcoin Core.");
				builder.AppendLine("#rpc.url=http://localhost:" + network.DefaultRPCPort + "/");
				builder.AppendLine("#rpc.user=bitcoinuser");
				builder.AppendLine("#rpc.password=bitcoinpassword");
				builder.AppendLine("#rpc.cookiefile=yourbitcoinfolder/.cookie");

				builder.AppendLine("#Tumbler server to connect to");
				builder.AppendLine("#tumbler.server=");
				builder.AppendLine();
				builder.AppendLine("#Configuration of the output wallet");
				builder.AppendLine("#outputwallet.extpubkey=xpub");
				builder.AppendLine("#outputwallet.keypath=0");
				builder.AppendLine();
				builder.AppendLine();
				builder.AppendLine("####Connection Commands####");
				builder.AppendLine("#Making Alice or Bob pass through TOR (Recommended, the circuit will change for each cycle/persona)");
				builder.AppendLine("#The default settings you run `tor -controlport 9051 -cookieauthentication 1`");
				builder.AppendLine("#alice.proxy.type=tor");
				builder.AppendLine("#alice.proxy.server=127.0.0.1:9051");
				builder.AppendLine("#alice.proxy.password=padeiwmnfw");
				builder.AppendLine("#alice.proxy.cookiefile=/var/run/tor/control.authcookie");
				builder.AppendLine("#or");
				builder.AppendLine("#bob.proxy.type=tor");
				builder.AppendLine("#bob.proxy.server=127.0.0.1:9051");
				builder.AppendLine("#bob.proxy.password=padeiwmnfw");
				builder.AppendLine("#bob.proxy.cookiefile=/var/run/tor/control.authcookie");
				builder.AppendLine();
				builder.AppendLine("#Making Alice or Bob pass through a HTTP Proxy");
				builder.AppendLine("#alice.proxy.type=http");
				builder.AppendLine("#alice.proxy.server=http://127.0.0.1:8118/");
				builder.AppendLine("#alice.proxy.username=dpowqkwkpd");
				builder.AppendLine("#alice.proxy.password=padeiwmnfw");
				builder.AppendLine("#or");
				builder.AppendLine("#bob.proxy.type=http");
				builder.AppendLine("#bob.proxy.server=http://127.0.0.1:8118/");
				builder.AppendLine("#bob.proxy.username=dpowqkwkpd");
				builder.AppendLine("#bob.proxy.password=padeiwmnfw");
				builder.AppendLine();
				builder.AppendLine("#Making Alice or Bob pass through a SOCKS Proxy");
				builder.AppendLine("#alice.proxy.type=socks");
				builder.AppendLine("#alice.proxy.server=127.0.0.1:9050");
				builder.AppendLine("#or");
				builder.AppendLine("#bob.proxy.type=socks");
				builder.AppendLine("#bob.proxy.server=127.0.0.1:9050");
				builder.AppendLine();
				builder.AppendLine("#Disabling any proxy");
				builder.AppendLine("#alice.proxy.type=none");
				builder.AppendLine("#or");
				builder.AppendLine("#bob.proxy.type=none");


				builder.AppendLine();
				builder.AppendLine();

				builder.AppendLine("####Debug Commands####");
				builder.AppendLine("#Whether or not signature for the escape transaction is transmitted to the Tumbler (default: true)");
				builder.AppendLine("#cooperative=false");
				builder.AppendLine("#Whether or not IP sharing between Bob and Alice is authorized (default: true for testnets, false for mainnet)");
				builder.AppendLine("#allowinsecure=true");
				File.WriteAllText(config, builder.ToString());
			}
			return config;
		}

		private void AssetConfigFileExists()
		{
			if(!File.Exists(ConfigurationFile))
				throw new ConfigurationException("Configuration file does not exists");
		}

	}
}
