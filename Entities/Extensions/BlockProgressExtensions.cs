using BchainSimServices;

namespace BSimClient.Entities.Extensions
{
    public static class BlockProgressExtensions
    {
        public static bool IsSameBlockAs(this BlockProgress blockProgress, BlockProgress anotherBlockProgress)
        {
            return blockProgress?.BlockIndex == anotherBlockProgress?.BlockIndex && 
                blockProgress?.BlockOrdinal == anotherBlockProgress?.BlockOrdinal;
        }
    }
}
