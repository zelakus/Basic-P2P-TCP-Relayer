using System;

namespace P2P_Relayer.Gateway
{
    class Program
    {
        static void Main(string[] args)
        {
            int port = 5371;
            var server = new Server(port);
            Console.WriteLine($"Server is working on {port}");

            Console.ReadLine();
            server.Stop();
        }
    }
}
