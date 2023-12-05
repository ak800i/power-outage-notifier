using System.Globalization;
using CsvHelper;

namespace PowerOutageNotifier
{
    public class ConfigReader
    {
        /// <summary>
        /// Example file contents:
        /// 
        /// Friendly Name,Chat ID,District Name,Street Name
        /// PositiveTest,123456,Палилула,САВЕ МРКАЉА
        /// </summary>
        readonly static private string csvFilePath = Path.Combine(Directory.GetCurrentDirectory(), "userdata.csv");
        readonly static private string botTokenFilePath = Path.Combine(Directory.GetCurrentDirectory(), "bot-token.txt");

        public static List<UserData> ReadUserData()
        {
            using (var reader = new StreamReader(csvFilePath))
            using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
            {
                List<UserData> userDataList = csv.GetRecords<UserData>().ToList();

                // Use the userDataList as needed
                return userDataList;
            }
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
