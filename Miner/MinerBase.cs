using System.Security.Cryptography;
using System.Threading.Tasks;

using BchainSimServices;

namespace BSimClient.Miner
{
    public abstract class MinerBase
    {
        protected readonly MinerInfo minerInfo;
        protected readonly SHA256 hasher;

        protected MinerBase(MinerInfo minerInfo)
        {
            this.minerInfo = minerInfo;
            this.hasher = SHA256.Create();
        }

        protected abstract byte[] GenerateProofOfWork(int challengeSize);

        public abstract Task StartAsync();
    }
}
