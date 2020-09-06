using System;
using System.Diagnostics;
using System.Linq;
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
        private readonly GlobalKnowledge.GlobalKnowledgeClient globalKnowledgeClient;
        private long lastConfigRefreshTimestamp = 0;
        private readonly CancellationToken miningCancellationToken;
        
        private object miningParamsLock = new object();
        private MiningParams miningParams;

        public AltPowMiner(MinerInfo minerInfo, GrpcChannel grpcChannel, CancellationToken cancellationToken) : base(minerInfo)
        {
            this.grpcChannel = grpcChannel;
            this.configClient = new Config.ConfigClient(grpcChannel);
            this.logClient = new Log.LogClient(grpcChannel);
            this.globalKnowledgeClient = new GlobalKnowledge.GlobalKnowledgeClient(grpcChannel);
            this.miningCancellationToken = cancellationToken;
        }

        public async Task RegisterOnNetworkAsync()
        {
            miningParams = await configClient.RegisterAsync(minerInfo);
        }

        public override async Task StartAsync()
        {
            await RegisterOnNetworkAsync();

            await Task.Run(async () =>
            {
                MineAsync();
            }, miningCancellationToken);
        }

        private async Task MineAsync()
        {
            while (true)
            {
                CheckConfig(); // not awaited, the new config is updated in maximum 1 extra cycle
                FindBestBlockToMineOn();
                var stopwatch = Stopwatch.StartNew();
                var pow = GenerateProofOfWork(miningParams.RoundBlockChallengeSize);
                stopwatch.Stop();
                var timeTaken = stopwatch.ElapsedMilliseconds;

                PostMiningUpdate(timeTaken); 
            }
        }

        private void FindBestBlockToMineOn()
        {
            var miningState = GetMiningUpdateAsync();
        }

        private async Task<BlockProgress> GetMiningUpdateAsync()
        {
            var gko = await globalKnowledgeClient.GetChainProgressAsync(new NothingGk());
            var blockProgresses = gko.BlockProgress.ToList();
            var bestBlock = blockProgresses.First(bp => bp.RoundBlockProgress == blockProgresses.Min(_ => _.RoundBlockProgress));
            return bestBlock;
            
        }

        private async void PostMiningUpdate(long timeTaken)
        {
            await globalKnowledgeClient.PutChainProgressAsync(new BlockProgressIn
            {
                // 
            });

            await logClient.WriteAsync(new LogMessage
            {
                LogLevel = 2,
                Message = $"Round block mined in {timeTaken} ms.",
                MinerId = minerInfo.MinerId,
                Tag = "MiningUpdate",
                Timestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds
            });
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

        protected override byte[] GenerateProofOfWork(int challengeSize)
        {
            int challengeIndex = challengeSize / 8;
            int bitsToBeZeroAtChallengeIndex = challengeSize % 8;
            bool doItAgain = true;
            byte[] pow;

            do
            {
                pow = Guid.NewGuid().ToByteArray();
                var hash = hasher.ComputeHash(pow);
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
                        doItAgain |= !((hash[challengeIndex] & 0xFE) == hash[challengeIndex]);
                        break;
                    case 2:
                        doItAgain |= !((hash[challengeIndex] & 0xFC) == hash[challengeIndex]);
                        break;
                    case 3:
                        doItAgain |= !((hash[challengeIndex] & 0xF8) == hash[challengeIndex]);
                        break;
                    case 4:
                        doItAgain |= !((hash[challengeIndex] & 0xF0) == hash[challengeIndex]);
                        break;
                    case 5:
                        doItAgain |= !((hash[challengeIndex] & 0xE0) == hash[challengeIndex]);
                        break;
                    case 6:
                        doItAgain |= !((hash[challengeIndex] & 0xC0) == hash[challengeIndex]);
                        break;
                    case 7:
                        doItAgain |= !((hash[challengeIndex] & 0x80) == hash[challengeIndex]);
                        break;
                }
            } while (doItAgain);
            return pow;
        }
    }
}
