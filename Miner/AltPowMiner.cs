using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

using BchainSimServices;

using Grpc.Net.Client;

namespace BSimClient.Miner
{
    public class AltPowMiner : MinerBase
    {
        private readonly GrpcChannel grpcChannel;
        private readonly Config.ConfigClient configClient;
        private readonly Log.LogClient logClient;
        private long lastConfigRefreshTimestamp = 0;
        private readonly CancellationToken miningCancellationToken;
        private static readonly DateTime epochTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private object miningParamsLock = new object();
        private MiningParams miningParams;

        public AltPowMiner(MinerInfo minerInfo, GrpcChannel grpcChannel, CancellationToken cancellationToken) : base(minerInfo)
        {
            this.grpcChannel = grpcChannel;
            configClient = new Config.ConfigClient(grpcChannel);
            logClient = new Log.LogClient(grpcChannel);
            this.miningCancellationToken = cancellationToken;
        }

        public async Task RegisterOnNetworkAsync()
        {
            miningParams = await configClient.RegisterAsync(minerInfo);
        }

        internal async void StartAsync()
        {
            await Task.Run(async () =>
            {
                while (true)
                {
                    await RegisterOnNetworkAsync();

                    CheckConfig();

                    var timeTaken = GenerateProofOfWork(miningParams.RoundBlockChallengeSize);

                    PostMiningUpdate(timeTaken);
                }
            }, miningCancellationToken);
        }

        private void PostMiningUpdate(long timeTaken)
        {
            logClient.WriteAsync(new LogMessage
            {
                LogLevel = 2,
                Message = $"Round block mined in \t{timeTaken} ms.",
                MinerId = minerInfo.MinerId,
                Tag = "MiningUpdate",
                Timestamp = (long) DateTime.UtcNow.Subtract(epochTime).TotalMilliseconds
            }) ;
        }

        /// <summary>
        /// Check for config every 30 seconds
        /// </summary>
        private async void CheckConfig()
        {
            var ts = Stopwatch.GetTimestamp();
            if (ts - lastConfigRefreshTimestamp > 30000)
            {
                lastConfigRefreshTimestamp = ts;
                miningParams = await configClient.GetMiningParamsAsync(minerInfo);
            }
        }

        protected override long GenerateProofOfWork(int challengeSize)
        {
            var stopwatch = Stopwatch.StartNew();
            int challengeIndex = challengeSize / 8;
            int bitsToBeZeroAtChallengeIndex = challengeSize % 8;
            bool doItAgain = true;

            while (doItAgain)
            {
                var hash = hasher.ComputeHash(Guid.NewGuid().ToByteArray());
                doItAgain = false;

                int i = 0;
                while (i < challengeIndex)
                {
                    if (hash[i] != 0x00)
                    {
                        doItAgain = true;
                        break;
                    }
                    i++;
                }

                switch (bitsToBeZeroAtChallengeIndex)
                {
                    case 0:
                        doItAgain |= false;
                        break;
                    case 1:
                        doItAgain |= !((hash[challengeIndex] & 0xFE) == 0);
                        break;
                    case 2:
                        doItAgain |= !((hash[challengeIndex] & 0xFC) == 0);
                        break;
                    case 3:
                        doItAgain |= !((hash[challengeIndex] & 0xF8) == 0);
                        break;
                    case 4:
                        doItAgain |= !((hash[challengeIndex] & 0xF0) == 0);
                        break;
                    case 5:
                        doItAgain |= !((hash[challengeIndex] & 0xE0) == 0);
                        break;
                    case 6:
                        doItAgain |= !((hash[challengeIndex] & 0xC0) == 0);
                        break;
                    case 7:
                        doItAgain |= !((hash[challengeIndex] & 0x80) == 0);
                        break;
                }
            }
            stopwatch.Stop();
            return stopwatch.ElapsedMilliseconds;
        }
    }
}
