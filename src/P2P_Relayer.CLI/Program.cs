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
            Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(@"config.json"));

            var client = new Client();
            Console.WriteLine("Client is working...");
            client.Connect(IPEndPoint.Parse(Config.EndPoint));

            while (true)
                Console.ReadLine();
        }
    }
}
