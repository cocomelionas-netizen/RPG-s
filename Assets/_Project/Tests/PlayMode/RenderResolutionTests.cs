using NUnit.Framework;
using RPG.CameraSystem;

namespace RPG.Tests
{
    /// <summary>The world is drawn at most about 1080 pixels high; the HUD stays at the screen's size.</summary>
    public class RenderResolutionTests
    {
        [Test]
        public void ScaleFor_KeepsSmallScreens_AndShrinksBigOnes()
        {
            Assert.AreEqual(1f, RenderResolution.ScaleFor(720));
            Assert.AreEqual(1f, RenderResolution.ScaleFor(1080));
            Assert.AreEqual(0.5f, RenderResolution.ScaleFor(2160), 0.001f);
            Assert.AreEqual(0.25f, RenderResolution.ScaleFor(10000), 0.001f, "Never below a quarter");
        }
    }
}
