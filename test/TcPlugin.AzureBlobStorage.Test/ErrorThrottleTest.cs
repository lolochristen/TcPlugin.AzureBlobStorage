using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TcPlugin.AzureBlobStorage.Storage;

namespace TcPlugin.AzureBlobStorage.Test
{
    [TestClass]
    public class ErrorThrottleTest
    {
        private static readonly DateTime Start = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

        [TestMethod]
        public void SameReason_IsShownOncePerWindow()
        {
            var throttle = new ErrorThrottle(TimeSpan.FromSeconds(30));

            Assert.IsTrue(throttle.ShouldShow("offline", Start));
            Assert.IsFalse(throttle.ShouldShow("offline", Start.AddSeconds(10)));
            Assert.IsFalse(throttle.ShouldShow("offline", Start.AddSeconds(25)));
        }

        [TestMethod]
        public void SameReason_IsShownAgainAfterQuietWindow()
        {
            var throttle = new ErrorThrottle(TimeSpan.FromSeconds(30));

            Assert.IsTrue(throttle.ShouldShow("offline", Start));
            Assert.IsTrue(throttle.ShouldShow("offline", Start.AddSeconds(31)));
        }

        [TestMethod]
        public void RepeatedFailures_KeepTheWindowOpen()
        {
            var throttle = new ErrorThrottle(TimeSpan.FromSeconds(30));

            Assert.IsTrue(throttle.ShouldShow("offline", Start));
            Assert.IsFalse(throttle.ShouldShow("offline", Start.AddSeconds(20)));
            Assert.IsFalse(throttle.ShouldShow("offline", Start.AddSeconds(40)));
        }

        [TestMethod]
        public void DifferentReason_IsShown()
        {
            var throttle = new ErrorThrottle(TimeSpan.FromSeconds(30));

            Assert.IsTrue(throttle.ShouldShow("offline", Start));
            Assert.IsTrue(throttle.ShouldShow("denied", Start.AddSeconds(1)));
        }
    }
}
