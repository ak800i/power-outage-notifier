namespace Tests
{
    using PowerOutageNotifier;

    [TestClass]
    public class LatinToCyrillicConverterTests
    {
        [TestMethod]
        public void TestMethod1()
        {
            Assert.AreEqual("Хусињских рудара", LatinToCyrillicConverter.ConvertLatinToCyrillic("Husinjskih rudara"));
            Assert.AreEqual("Хусињских рудара".ToUpper(), LatinToCyrillicConverter.ConvertLatinToCyrillic("HUSINJSKIH RUDARA"));
            Assert.AreEqual("Хусињских рудара".ToUpper(), LatinToCyrillicConverter.ConvertLatinToCyrillic("HUSINjSKIH RUDARA"));
            Assert.AreEqual("Хусињских рудара 88".ToUpper(), LatinToCyrillicConverter.ConvertLatinToCyrillic("HUSINjSKIH RUDARA 88"));
            Assert.AreEqual("Хусињских рудара", LatinToCyrillicConverter.ConvertLatinToCyrillic("Хусињских рудара"));
        }
    }
}