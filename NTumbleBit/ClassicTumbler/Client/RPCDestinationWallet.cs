﻿using Microsoft.Extensions.Logging;
using System;
using NBitcoin;
using Newtonsoft.Json.Linq;
using NTumbleBit.Logging;
using Blockcore.Features.RPC;

namespace NTumbleBit.ClassicTumbler.Client
{
    public class RPCDestinationWallet : IDestinationWallet
    {
        RPCClient _RPC;
        public RPCDestinationWallet(RPCClient client)
        {
            _RPC = client ?? throw new ArgumentNullException("client");
        }

        public KeyPath GetKeyPath(Script script)
        {
            var address = script.GetDestinationAddress(_RPC.Network);
            if (address == null)
                return null;
            var result = (JObject)_RPC.SendCommand(RPCOperations.validateaddress, address.ToString()).Result;
            if (result["hdkeypath"] == null)
                return null;
            var path = new KeyPath(result["hdkeypath"].Value<string>());
            Logs.Wallet.LogInformation($"Created address {address} of with HD path {path}");
            return path;
        }

        public Script GetNewDestination()
        {
            return _RPC.GetNewAddress().ScriptPubKey;
        }
    }
}
