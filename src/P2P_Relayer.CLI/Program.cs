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

        const string configName = "config.json";
        static void Main()
        {
            //Get config
            if (!File.Exists(configName))
            {
                Console.WriteLine("Creating default config...");
                Config = new Config();
                File.WriteAllText(configName, JsonConvert.SerializeObject(Config));
            }
            else
            {
                Console.WriteLine("Loading config...");
                Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configName));
            }

            //Start host/client
            IClient client;
            if (Config.IsHost)
            {
                Console.WriteLine("Starting host...");
                client = new Host();
            }
            else
            {
                Console.WriteLine("Starting client...");
                client = new Client();
            }

            //Connect to gateway
            client.Connect(IPEndPoint.Parse(Config.EndPoint), isGateway: true);

            while (true)
                Console.ReadLine();
        }
    }
}
