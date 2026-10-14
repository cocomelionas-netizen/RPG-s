using System;

namespace RPG.Networking
{
    public enum NetworkLaunchMode
    {
        /// <summary>Do not start automatically; wait for the connection panel.</summary>
        None,
        Server,
        Host,
        Client,
    }

    /// <summary>
    /// Network settings read from the command line, for example:
    /// <c>RPG-s.exe -batchmode -nographics -server -port 7777</c> or <c>RPG-s.exe -client -ip 192.168.1.10</c>.
    /// </summary>
    public readonly struct NetworkLaunchOptions
    {
        public const string DefaultAddress = "127.0.0.1";
        public const ushort DefaultPort = 7777;

        public readonly NetworkLaunchMode Mode;
        public readonly string Address;
        public readonly ushort Port;

        public NetworkLaunchOptions(NetworkLaunchMode mode, string address, ushort port)
        {
            Mode = mode;
            Address = address;
            Port = port;
        }

        public static NetworkLaunchOptions FromCommandLine() => Parse(Environment.GetCommandLineArgs());

        public static NetworkLaunchOptions Parse(string[] args)
        {
            var mode = NetworkLaunchMode.None;
            string address = DefaultAddress;
            ushort port = DefaultPort;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLowerInvariant();
                bool hasValue = i + 1 < args.Length;

                switch (arg)
                {
                    case "-server": mode = NetworkLaunchMode.Server; break;
                    case "-host": mode = NetworkLaunchMode.Host; break;
                    case "-client": mode = NetworkLaunchMode.Client; break;
                    case "-ip" when hasValue: address = args[++i]; break;
                    case "-port" when hasValue && ushort.TryParse(args[i + 1], out ushort parsed):
                        port = parsed;
                        i++;
                        break;
                }
            }

            return new NetworkLaunchOptions(mode, address, port);
        }
    }
}
