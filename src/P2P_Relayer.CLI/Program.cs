using Newtonsoft.Json;
using P2P_Relayer.CLI;
using System;
using System.IO;
using System.Net;

namespace P2P_Relayer
{
    class Program
    {
        internal static Config Config;

        static void Main()
        {
            Console.WriteLine("Loading config...");
            Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(@"config.json"));

            Console.WriteLine("Starting client...");
            var client = new Client();
            client.Connect(IPEndPoint.Parse(Config.EndPoint));

            while (true)
                Console.ReadLine();
        }
    }
}
