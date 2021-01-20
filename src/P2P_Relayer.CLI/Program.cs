using Newtonsoft.Json;
using P2P_Relayer.CLI;
using System;
using System.IO;

namespace P2P_Relayer
{
    class Program
    {

        static void Main()
        {
            var config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(@"config.json"));


            Console.WriteLine("hey");
        }
    }
}
