namespace PowerOutageNotifier
{
    public class ConfigReader
    {
        readonly static private string botTokenFilePath = Path.Combine(Directory.GetCurrentDirectory(), "bot-token.txt");

        static ConfigReader()
        {
#if DEBUG
            // Path when in Debug mode (local Windows file)
            botTokenFilePath = Path.Combine(Directory.GetCurrentDirectory(), "bot-token.txt");
#else
            // Path when in Release mode (Docker volume)
            botTokenFilePath = Path.Combine("/config", "bot-token.txt");
#endif
        }

        /// <summary>
        /// Example file contents:
        /// 
        /// 123456:AAAAAAA
        /// </summary>
        public static string ReadBotToken() =>
            File.ReadAllText(botTokenFilePath);
    }
}
