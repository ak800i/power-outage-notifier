namespace PowerOutageNotifier.PowerOutageNotifierService
{
    using CsvHelper;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// Store which reads and writes the user data to a CSV file.
    /// </summary>
    public class UserDataStore
    {
        /// <summary>
        /// Example file contents:
        /// 
        /// Friendly Name,Chat ID,Municipality Name,Street Name
        /// PositiveTest,123456,Палилула,САВЕ МРКАЉА
        /// </summary>
        private static readonly string csvFilePath;

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

        /// <summary>
        /// Reads the user data from the store.
        /// </summary>
        /// <returns>List of <see cref="UserData"/> objects.</returns>
        public static List<UserData> ReadUserData()
        {
            // Check if the file exists
            if (!File.Exists(csvFilePath))
            {
                // Create an empty file with just the headers
                File.WriteAllText(csvFilePath, "Friendly Name,Chat ID,Municipality Name,Street Name\n");
            }

            using StreamReader reader = new StreamReader(csvFilePath);
            using CsvReader csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            return csv.GetRecords<UserData>().ToList();
        }

        /// <summary>
        /// Writes the user data to the store.
        /// </summary>
        /// <param name="userDataList">The complete list of users to persist.</param>
        public static void WriteUserData(List<UserData> userDataList)
        {
            using StreamWriter writer = new StreamWriter(csvFilePath);
            using CsvWriter csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
            csv.WriteRecords(userDataList);
        }
    }
}
