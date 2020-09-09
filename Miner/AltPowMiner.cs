using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using BchainSimServices;

using BSimClient.Entities.Extensions;

using Grpc.Net.Client;

using Polly;
using Polly.Retry;

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
        private BlockProgress preferredBlock;
        private RetryPolicy<byte[]> MiningPolicy;

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
            SetMiningPolicy(miningParams);
        }

        public override async Task StartAsync()
        {
            await RegisterOnNetworkAsync();

            await Task.Run(async () =>
            {
                await MineAsync();
            }, miningCancellationToken);
        }

        private async Task MineAsync()
        {
            while (true)
            {
                CheckConfig(); // not awaited, the new config is updated in maximum 1 extra cycle

                // switch block if required and log
                var blockThatMakesSense = await FindBestBlockToMineOnAsync();
                if (!blockThatMakesSense.IsSameBlockAs(preferredBlock))
                {
                    PostBlockSwitchUpdate(preferredBlock, blockThatMakesSense);
                }
                preferredBlock = blockThatMakesSense; // the newer object has newer info, unconditionally update the local copy 

                var stopwatch = Stopwatch.StartNew();
                var pow = GenerateProofOfWork(miningParams.RoundBlockChallengeSize);
                stopwatch.Stop();

                var timeTaken = stopwatch.ElapsedMilliseconds;
                PostMiningUpdate(pow, timeTaken);
            }
        }

        public readonly GetChainProgressIn getChainProgressIn = new GetChainProgressIn();
        private async Task<BlockProgress> FindBestBlockToMineOnAsync()
        {
            var gko = await globalKnowledgeClient.GetChainProgressAsync(getChainProgressIn);
            var blockProgresses = gko.BlockProgress.ToList();

            // find min diff
            BlockProgress bpWithMinDiff = null;
            int minDiff = int.MaxValue;
            foreach (BlockProgress progress in blockProgresses)
            {
                var maxProgress = progress.MinerRoundBlockProgress.Count > 0 ? progress.MinerRoundBlockProgress.Max(lm => lm.Value) : 0;
                int diff = 0;
                if (progress.MinerRoundBlockProgress.TryGetValue(minerInfo.MinerId, out var selfProgress))
                {
                    diff = maxProgress - selfProgress;
                }
                else // self progress is zero
                {
                    diff = maxProgress;
                }

                if (minDiff > diff) // gets assigned to first block
                {
                    minDiff = diff;
                    bpWithMinDiff = progress;
                }
            }

            return bpWithMinDiff;
        }

        private async void PostMiningUpdate(byte[] pow, long timeTaken)
        {
            string powString = Convert.ToBase64String(pow);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            PostUpdate($"Round block mined in {timeTaken} ms.", "MiningUpdate", 2, powString);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            await globalKnowledgeClient.PutChainProgressAsync(new BlockProgressIn
            {
                BlockIndex = preferredBlock.BlockIndex,
                BlockOrdinal = preferredBlock.BlockOrdinal,
                Pow = powString,
                BlockProgress = preferredBlock.MinerRoundBlockProgress.ContainsKey(minerInfo.MinerId) ? preferredBlock.MinerRoundBlockProgress[minerInfo.MinerId] + 1 : 1,
                TimeAtProgress = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds
            });
        }

        private async void PostBlockSwitchUpdate(BlockProgress old, BlockProgress neu)
        {
            string message = "Switching to a different block" +
                $"{{ index {old?.BlockIndex}->{neu.BlockIndex}, ordinal {old?.BlockOrdinal}->{neu.BlockOrdinal}}}.";
            string tag = "TacticalUpdate";
            int logLevel = 2;

            await PostUpdate(message, tag, logLevel);
        }

        private async Task PostUpdate(string message, string tag, int logLevel, string token = "")
        {
            await logClient.WriteAsync(new LogMessage
            {
                LogLevel = logLevel,
                Message = message,
                Token = token,
                MinerId = minerInfo.MinerId,
                Tag = tag,
                Timestamp = (long)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalMilliseconds,
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
                var mp = await configClient.GetMiningParamsAsync(minerInfo);
                if(mp.RoundBlockChallengeSize != miningParams?.RoundBlockChallengeSize)
                    SetMiningPolicy(mp);
                
                miningParams = mp;
            }
        }

        private void SetMiningPolicy(MiningParams mp)
        {
            var sleepMillis = 100 / minerInfo.HashPower;
            MiningPolicy = Policy
                .HandleResult<byte[]>((bytes) => CheckProofOfWork(hasher, bytes, mp.RoundBlockChallengeSize) == false)
                .WaitAndRetryForever((i) => TimeSpan.FromMilliseconds(sleepMillis));
        }

        protected override byte[] GenerateProofOfWork(int challengeSize)
        {
            Func<byte[]> generator = () => Guid.NewGuid().ToByteArray(); // fairly random work
            var pow = MiningPolicy.Execute(generator);
            return pow;
        }

        private static bool CheckProofOfWork(HashAlgorithm hasher, byte[] pow, int challengeSize)
        {
            var hash = hasher.ComputeHash(pow);
            int challengeIndex = challengeSize / 8;
            int bitsToBeZeroAtChallengeIndex = challengeSize % 8;

            bool invalid = false;
            int i = 0;
            while (i < challengeIndex)
            {
                if (hash[i] != 0x00)
                {
                    invalid = true;
                    break;
                }
                i++;
            }

            switch (bitsToBeZeroAtChallengeIndex)
            {
                case 0:
                    invalid |= false;
                    break;
                case 1:
                    invalid |= ((hash[challengeIndex] & 0x80) != 0);
                    break;
                case 2:
                    invalid |= ((hash[challengeIndex] & 0xC0) != 0);
                    break;
                case 3:
                    invalid |= ((hash[challengeIndex] & 0xE0) != 0);
                    break;
                case 4:
                    invalid |= ((hash[challengeIndex] & 0xF0) != 0);
                    break;
                case 5:
                    invalid |= ((hash[challengeIndex] & 0xF8) != 0);
                    break;
                case 6:
                    invalid |= ((hash[challengeIndex] & 0xFC) != 0);
                    break;
                case 7:
                    invalid |= ((hash[challengeIndex] & 0xFE) != 0);
                    break;
            }

            return !invalid;
        }
    }
}
