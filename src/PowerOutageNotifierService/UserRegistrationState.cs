namespace PowerOutageNotifier.PowerOutageNotifierService
{
    /// <summary>
    /// Enum representing the state of the user registration process.
    /// </summary>
    public enum UserRegistrationState
    {
        /// <summary>
        /// The user is not in the registration process.
        /// </summary>
        None,

        /// <summary>
        /// We are awaiting the friendly name of the user.
        /// </summary>
        AwaitingFriendlyName,

        /// <summary>
        /// We are awaiting the municipality name of the user.
        /// </summary>
        AwaitingMunicipalityName,

        /// <summary>
        /// We are awaiting the street name of the user.
        /// </summary>
        AwaitingStreetName,
    }
}
