using System;

using CommandLine;

namespace BSimClient.CommandLine
{
    public class MinerOptions
    {

        [Option('n', "name", Default = null, HelpText = "The name of the miner (a string without spaces)", Required = true)]
        public String MinerName { get; set; }

        [Option('h', "hash-power-factor", Default = null, HelpText = "Hash power of the miner. Default is 1.0", Required = true)]
        public double HashPowerFactor { get; set; }
    }
}
