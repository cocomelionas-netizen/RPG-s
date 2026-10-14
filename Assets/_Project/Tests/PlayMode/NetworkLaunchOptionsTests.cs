using NUnit.Framework;
using RPG.Networking;

namespace RPG.Tests
{
    public class NetworkLaunchOptionsTests
    {
        [Test]
        public void NoArguments_UsesDefaults()
        {
            NetworkLaunchOptions options = NetworkLaunchOptions.Parse(new[] { "RPG-s.exe" });

            Assert.AreEqual(NetworkLaunchMode.None, options.Mode);
            Assert.AreEqual(NetworkLaunchOptions.DefaultAddress, options.Address);
            Assert.AreEqual(NetworkLaunchOptions.DefaultPort, options.Port);
        }

        [Test]
        public void Server_WithPort()
        {
            NetworkLaunchOptions options = NetworkLaunchOptions.Parse(new[] { "RPG-s.exe", "-batchmode", "-nographics", "-server", "-port", "7800" });

            Assert.AreEqual(NetworkLaunchMode.Server, options.Mode);
            Assert.AreEqual(7800, options.Port);
        }

        [Test]
        public void Client_WithAddress_IsCaseInsensitive()
        {
            NetworkLaunchOptions options = NetworkLaunchOptions.Parse(new[] { "RPG-s.exe", "-CLIENT", "-ip", "192.168.1.20" });

            Assert.AreEqual(NetworkLaunchMode.Client, options.Mode);
            Assert.AreEqual("192.168.1.20", options.Address);
        }

        [Test]
        public void InvalidPort_KeepsDefault()
        {
            NetworkLaunchOptions options = NetworkLaunchOptions.Parse(new[] { "-host", "-port", "notaport" });

            Assert.AreEqual(NetworkLaunchMode.Host, options.Mode);
            Assert.AreEqual(NetworkLaunchOptions.DefaultPort, options.Port);
        }

        [Test]
        public void MissingValue_AtEnd_IsIgnored()
        {
            NetworkLaunchOptions options = NetworkLaunchOptions.Parse(new[] { "-client", "-ip" });

            Assert.AreEqual(NetworkLaunchMode.Client, options.Mode);
            Assert.AreEqual(NetworkLaunchOptions.DefaultAddress, options.Address);
        }
    }
}
