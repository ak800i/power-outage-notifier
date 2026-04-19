namespace Tests
{
    using PowerOutageNotifier.PowerOutageNotifierService;

    [TestClass]
    public class StreetNumberMatchingTests
    {
        [TestMethod]
        public void NumberInSimpleRange_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 29-35", "31В"));
        }

        [TestMethod]
        public void NumberNotInSimpleRange_ReturnsFalse()
        {
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 55-57", "31В"));
        }

        [TestMethod]
        public void ExactSingleNumberMatch_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 22А", "22"));
        }

        [TestMethod]
        public void SingleNumberNoMatch_ReturnsFalse()
        {
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 22А", "31В"));
        }

        [TestMethod]
        public void RangeWithLetterSuffixOnEndpoints_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 15Б-41", "31В"));
        }

        [TestMethod]
        public void SameNumberDifferentLetterSuffix_ReturnsTrue()
        {
            // 46А-46Б → range 46 to 46
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 46А-46Б", "46В"));
        }

        [TestMethod]
        public void MultipleCommaSegments_MatchInSecond_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 10-28,34-42,46А-46Б,15Б-41", "38"));
        }

        [TestMethod]
        public void MultipleCommaSegments_NoMatch_ReturnsFalse()
        {
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 10-12,46А-46Б", "31В"));
        }

        [TestMethod]
        public void NoColonInInput_FallbackTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА", "31В"));
        }

        [TestMethod]
        public void AllSegmentsUnparseable_FallbackTrue()
        {
            // ББ is "без броја" (no number) — can't parse → fallback
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: ББ", "31В"));
        }

        [TestMethod]
        public void UserNumberWithLetterSuffix_MatchesRange()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 2-48", "31В"));
        }

        [TestMethod]
        public void UserNumberWithLetterSuffix_OutOfRange()
        {
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 2-10", "31В"));
        }

        [TestMethod]
        public void UserNumberIsExactBoundary_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 10-31", "31"));
        }

        [TestMethod]
        public void UserNumberIsExactLowerBoundary_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 31-50", "31В"));
        }

        [TestMethod]
        public void UserNumberNoDigits_FallbackTrue()
        {
            // User street number has no numeric part — can't compare, fallback to notify
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 10-20", "ББ"));
        }

        [TestMethod]
        public void MixedParseableAndUnparseable_MatchFound()
        {
            // ББ is unparseable but 29-35 matches
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: ББ,29-35", "31В"));
        }

        [TestMethod]
        public void MixedParseableAndUnparseable_NoMatch()
        {
            // ББ is unparseable but 55-57 doesn't match — at least one segment was parsed
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("КЕЛТСКА: ББ,55-57", "31В"));
        }

        [TestMethod]
        public void EmptyAfterColon_FallbackTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: ", "31В"));
        }

        [TestMethod]
        public void RealWorldExample_NotInRange()
        {
            // User at 31В, outage at 55-57 — should NOT notify
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 55-57", "31В"));
        }

        [TestMethod]
        public void RealWorldExample_InRange()
        {
            // User at 31В, outage at 20-40 — should notify
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 20-40", "31В"));
        }
    }
}
