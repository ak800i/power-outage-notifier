using CsvHelper;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerOutageNotifier
{
    public class UserDataStore
    {
        /// <summary>
        /// Example file contents:
        /// 
        /// Friendly Name,Chat ID,District Name,Street Name
        /// PositiveTest,123456,Палилула,САВЕ МРКАЉА
        /// </summary>
        readonly static private string csvFilePath;

        static UserDataStore()
        {
#if DEBUG
            // Path when in Debug mode (local Windows file)
            csvFilePath = Path.Combine(Directory.GetCurrentDirectory(), "userdata.csv");
#else
            // Path when in Release mode (Docker volume)
            csvFilePath = Path.Combine("/config", "userdata.csv");
#endif
        }

        public static List<UserData> ReadUserData()
        {
            // Check if the file exists
            if (!File.Exists(csvFilePath))
            {
                // Create an empty file with just the headers
                File.WriteAllText(csvFilePath, "Friendly Name,Chat ID,District Name,Street Name\n");
            }

            using var reader = new StreamReader(csvFilePath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<UserData>().ToList();
        }

        public static void WriteUserData(List<UserData> userDataList)
        {
            using var writer = new StreamWriter(csvFilePath);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(userDataList);
        }
    }
}
