namespace PowerOutageNotifier
{
    using CsvHelper.Configuration.Attributes;

    /// <summary>
    /// Represents the data of a single user.
    /// </summary>
    public class UserData
    {
        /// <summary>
        /// The friendly name of the user.
        /// This column should be unique.
        /// </summary>
        [Name("Friendly Name")]
        public string? FriendlyName { get; set; }

        /// <summary>
        /// The chat ID of the user.
        /// </summary>
        [Name("Chat ID")]
        public long ChatId { get; set; }

        /// <summary>
        /// The name of the municipality the user subscribes for.
        /// </summary>
        [Name("District Name")]
        public string? MunicipalityName { get; set; }

        /// <summary>
        /// The name of the street the user subscribes for.
        /// </summary>
        [Name("Street Name")]
        public string? StreetName { get; set; }
    }
}
