using System;
using System.Collections.Generic;
using System.Threading;

using BchainSimServices;

using BSimClient.CommandLine;
using BSimClient.Configuration;
using BSimClient.Miner;

using CommandLine;

namespace BSimClient
{
    class Program
    {
        static void Main(string[] args)
        {
            /**
             * arguments 
             * --name "unique-name" 
             * --hash-power "some factor"
             * --config "location"
             */
            var minerOptionsParsed = Parser.Default.ParseArguments<MinerOptions>(args);
            minerOptionsParsed.WithParsed<MinerOptions>(minerOptions =>
            {
                Application.StartupParameters = minerOptions;
            });

            minerOptionsParsed.WithNotParsed((IEnumerable<Error> errors) =>
            {
                foreach (Error error in errors)
                {
                    Console.Error.WriteLine(error);
                }
            });

            var minerInfo = new MinerInfo
            {
                HashPower = Application.StartupParameters.HashPowerFactor,
                MinerId = Application.StartupParameters.MinerName
            };
            
            var cancellationToken = new CancellationToken();
            MinerBase miner = new AltPowMiner(minerInfo, Application.Channel, cancellationToken);
            miner.StartAsync().Wait();
        }

    }
}
