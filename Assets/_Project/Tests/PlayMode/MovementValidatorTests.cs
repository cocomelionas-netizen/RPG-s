using NUnit.Framework;
using RPG.Networking;

namespace RPG.Tests
{
    public class MovementValidatorTests
    {
        private const float Speed = 3f;
        private const float Step = 0.05f;

        [Test]
        public void NormalWalking_IsAlwaysAccepted()
        {
            var validator = new MovementValidator(Speed, 0.0);
            for (int i = 1; i <= 200; i++)
                Assert.IsTrue(validator.TryMove(Speed * Step, i * Step), $"Step {i} rejected");
        }

        [Test]
        public void Teleport_IsRejected()
        {
            var validator = new MovementValidator(Speed, 0.0);
            Assert.IsFalse(validator.TryMove(5f, Step));
        }

        [Test]
        public void DoubleSpeed_IsRejectedWithinOneSecond()
        {
            var validator = new MovementValidator(Speed, 0.0);
            bool rejected = false;
            for (int i = 1; i <= 20 && !rejected; i++)
                rejected = !validator.TryMove(Speed * 2f * Step, i * Step);

            Assert.IsTrue(rejected);
        }

        [Test]
        public void JitterBurst_IsAccepted()
        {
            var validator = new MovementValidator(Speed, 0.0);
            // Four updates are delayed by the network and arrive at the same moment.
            double arrival = 4 * Step;
            for (int i = 0; i < 4; i++)
                Assert.IsTrue(validator.TryMove(Speed * Step, arrival), $"Update {i} rejected");
        }

        [Test]
        public void RejectedMove_DoesNotSpendBudget()
        {
            var validator = new MovementValidator(Speed, 0.0);
            float before = validator.Budget;

            Assert.IsFalse(validator.TryMove(100f, 0.0));
            Assert.AreEqual(before, validator.Budget, 0.0001f);
        }
    }
}
