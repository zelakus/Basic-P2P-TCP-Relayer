namespace P2P_Relayer.CLI
{
    class Config
    {
        public string EndPoint { get; set; } = "127.0.0.1:5371";
        public string Token { get; set; } = "default-token";
        public bool IsHost { get; set; } = false;
        public int TargetPort { get; set; } = 80;
    }
}
