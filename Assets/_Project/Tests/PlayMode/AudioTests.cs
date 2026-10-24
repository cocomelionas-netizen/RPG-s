using System;
using System.IO;
using NUnit.Framework;
using RPG.World;

namespace RPG.Tests
{
    /// <summary>Sounds and music: every sound has a file, every zone mood a track.</summary>
    public class AudioTests
    {
        private const string Folder = "Assets/_Project/Audio";

        [Test]
        public void EverySound_HasAFile()
        {
            foreach (Sfx kind in Enum.GetValues(typeof(Sfx)))
                Assert.IsTrue(File.Exists($"{Folder}/Sfx/{kind}1.ogg"), $"{kind}1.ogg is missing.");
        }

        [Test]
        public void EveryMood_HasATrack()
        {
            foreach (string mood in new[] { GameAudio.MusicTown, GameAudio.MusicFields, GameAudio.MusicForest, GameAudio.MusicDark,
                         GameAudio.MusicDungeon })
                Assert.IsTrue(File.Exists($"{Folder}/Music/{mood}.ogg"), $"No {mood} music.");
            Assert.IsTrue(File.Exists($"{Folder}/Ambience/{GameAudio.AmbienceForest}.ogg"));
            Assert.IsTrue(File.Exists($"{Folder}/Ambience/{GameAudio.AmbienceCave}.ogg"));
        }

        [Test]
        public void Mute_IsRemembered()
        {
            bool was = GameAudio.Muted;
            GameAudio.Muted = !was;
            Assert.AreEqual(!was, GameAudio.Muted);
            GameAudio.Muted = was;
        }
    }
}
