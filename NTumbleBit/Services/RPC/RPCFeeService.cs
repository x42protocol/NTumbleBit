using System;
using NBitcoin;

namespace NTumbleBit.Services.RPC
{
	public class RPCFeeService : IFeeService
	{
		Network network;

		public RPCFeeService(Network network)
		{
			this.network = network;
		}

		public FeeRate FallBackFeeRate
		{
			get; set;
		}
		public FeeRate MinimumFeeRate
		{
			get; set;
		}

		FeeRate _CachedValue;
		DateTimeOffset _CachedValueTime;
		TimeSpan CacheExpiration = TimeSpan.FromSeconds(60 * 5);
		public FeeRate GetFeeRate()
		{
			if(DateTimeOffset.UtcNow - _CachedValueTime > CacheExpiration)
			{
				var rate = FetchRate();
				_CachedValue = rate;
				_CachedValueTime = DateTimeOffset.UtcNow;
				return rate;
			}
			else
			{
				return _CachedValue;
			}
		}

		private FeeRate FetchRate()
		{
			var rate = new FeeRate(network.MinTxFee);
			if(rate == null)
				throw new FeeRateUnavailableException("The fee rate is unavailable");
			if(rate < MinimumFeeRate)
				rate = MinimumFeeRate;
			return rate;
		}
	}
}
