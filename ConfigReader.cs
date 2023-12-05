using System.Globalization;
using CsvHelper;

namespace PowerOutageNotifier
{
    public class ConfigReader
    {
        readonly static private string botTokenFilePath = Path.Combine(Directory.GetCurrentDirectory(), "bot-token.txt");


        /// <summary>
        /// Example file contents:
        /// 
        /// 123456:AAAAAAA
        /// </summary>
        public static string ReadBotToken() =>
            File.ReadAllText(botTokenFilePath);
    }
}
