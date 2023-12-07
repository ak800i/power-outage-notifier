namespace Tests
{
    using PowerOutageNotifier.PowerOutageNotifierService;

    /// <summary>
    /// Tests for the <see cref="LatinToCyrillicConverter"/> class.
    /// </summary>
    [TestClass]
    public class LatinToCyrillicConverterTests
    {
        /// <summary>
        /// Tests the <see cref="LatinToCyrillicConverter.ConvertLatinToCyrillic(string)"/> method.
        /// </summary>
        [TestMethod]
        public void ConvertLatinToCyrillicTest()
        {
            Assert.AreEqual("Хусињских рудара", LatinToCyrillicConverter.ConvertLatinToCyrillic("Husinjskih rudara"));
            Assert.AreEqual("Хусињских рудара".ToUpper(), LatinToCyrillicConverter.ConvertLatinToCyrillic("HUSINJSKIH RUDARA"));
            Assert.AreEqual("Хусињских рудара".ToUpper(), LatinToCyrillicConverter.ConvertLatinToCyrillic("HUSINjSKIH RUDARA"));
            Assert.AreEqual("Хусињских рудара 88".ToUpper(), LatinToCyrillicConverter.ConvertLatinToCyrillic("HUSINjSKIH RUDARA 88"));
            Assert.AreEqual("Хусињских рудара", LatinToCyrillicConverter.ConvertLatinToCyrillic("Хусињских рудара"));
        }
    }
}