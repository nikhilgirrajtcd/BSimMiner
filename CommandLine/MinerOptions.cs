using CommandLine;
using System;
using System.Collections.Generic;
using System.Text;

namespace BSimClient.CommandLine
{
    public class MinerOptions
    {
        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }

        [Option("name", Default = null, HelpText = "The name of the miner (a string without spaces)", Required = true)]
        public String MinerName { get; set; }

        [Option('f', "hash-power-factor", Default = null, HelpText = "Hash power of the miner. Default is 1.0", Required = true, Min = 0, Max = 10)]
        public double HashPowerFactor { get; set; }
    }
}
