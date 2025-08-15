namespace PowerOutageNotifier.PowerOutageNotifierService
{
    using HtmlAgilityPack;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using Telegram.Bot;
    using Telegram.Bot.Exceptions;
    using Telegram.Bot.Types;

    /// <summary>
    /// This class contains the main service logic.
    /// </summary>
    public class MainService
    {
        private static readonly long? logChatId =
            long.TryParse(Environment.GetEnvironmentVariable("LOG_CHAT_ID"), out long value)
                ? value
                : null;

        private static readonly bool? enableReaderOnBot =
            bool.TryParse(Environment.GetEnvironmentVariable("enable_reader_on_bot"), out bool value)
                ? value
                : null;

        private static readonly string telegramBotToken = ConfigReader.ReadBotToken();

        // Use a shared HttpClient with a sane timeout to avoid long hangs and allow retries
        private static readonly HttpClient httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };

        private static readonly TelegramBotClient botClient = new TelegramBotClient(telegramBotToken, httpClient);

        private static readonly List<UserData> userDataList = UserDataStore.ReadUserData();

        // URLs of the web page to scrape
        private static readonly List<string> powerOutageUrls =
        [
            "http://www.epsdistribucija.rs/planirana-iskljucenja-beograd/Dan_0_Iskljucenja.htm",
            "http://www.epsdistribucija.rs/planirana-iskljucenja-beograd/Dan_1_Iskljucenja.htm",
            "http://www.epsdistribucija.rs/planirana-iskljucenja-beograd/Dan_2_Iskljucenja.htm",
            "http://www.epsdistribucija.rs/planirana-iskljucenja-beograd/Dan_3_Iskljucenja.htm",
        ];

        private static readonly List<string> waterOutageUrls =
        [
            "https://www.bvk.rs/planirani-radovi/",
        ];

        private static readonly List<string> waterUnplannedOutageUrls =
        [
            "https://www.bvk.rs/kvarovi-na-mrezi/",
        ];

        internal enum NotificationType
        {
            PowerOutage,
            PlannedWaterOutage,
            UnplannedWaterOutage,
        }

        /// <summary>
        /// Starts the service.
        /// </summary>
        /// <param name="frequency">How often the scraping should occur.</param>
        /// <returns>Awaitable void.</returns>
        public Task Start(TimeSpan? frequency = null)
        {
            if (frequency == null)
            {
                frequency = TimeSpan.FromHours(1);
            }

            Version? version = Assembly.GetExecutingAssembly().GetName().Version;
            if (version != null)
            {
                string versionInfo = $"v{version.Major}.{version.Minor}.{version.Build}.{version.Revision}";
                LogAsync($"Service running on {Environment.MachineName}, Version: {versionInfo}").GetAwaiter().GetResult();
            }
            else
            {
                LogAsync($"Service running on {Environment.MachineName}, Version information not available").GetAwaiter().GetResult();
            }

            Task messageReceiverTask = Task.CompletedTask;
            if (enableReaderOnBot.HasValue && enableReaderOnBot.Value)
            {
                messageReceiverTask = this.MessageReceiver(); // Start the message receiver task
            }

            return Task.WhenAll(
                messageReceiverTask,
                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            await Task.WhenAll(
                                CheckAndNotifyPowerOutageAsync(),
                                CheckAndNotifyWaterOutageAsync(),
                                CheckAndNotifyUnplannedWaterOutageAsync());

                            await Task.Delay(frequency.Value);
                        }
                        catch (WebException ex)
                        {
                            if (ex.Message.Contains("Resource temporarily unavailable"))
                            {
                                // do not log the exception
                            }
                            else
                            {
                                await LogAsync($"Exception in periodic task: {ex}");
                            }
                        }
                        catch (Exception ex)
                        {
                            await LogAsync($"Exception in periodic task: {ex}");
                        }
                    }
                })
            );
        }

        /// <summary>
        /// Stops the service.
        /// </summary>
        public void Stop() =>
            LogAsync($"Service stopping on {Environment.MachineName}")
            .GetAwaiter()
            .GetResult();

        private async Task MessageReceiver()
        {
            int offset = 0; // Identifier of the first update to be returned
            // TODO - do we need to persist this offset?

            while (true)
            {
                try
                {
                    // Use long polling with a timeout to reduce request rate and avoid 429
                    Update[] updates = await botClient.GetUpdatesAsync(offset: offset, timeout: 30);

                    foreach (Update update in updates)
                    {
                        if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
                        {
                            Message? message = update.Message;
                            if (message != null && message.Text != null)
                            {
                                if (message.Text.StartsWith("/"))
                                {
                                    _ = userRegistrationData.Remove(message.Chat.Id);
                                    await HandleCommand(message);
                                }
                                else if (userRegistrationData.TryGetValue(message.Chat.Id, out (UserRegistrationState State, UserData UserData) registrationData))
                                {
                                    await HandleUserResponse(message, registrationData);
                                }
                            }
                        }

                        offset = update.Id + 1;
                    }
                }
                catch (ApiRequestException ex)
                {
                    // Handle Telegram API errors (e.g., flood control / rate limits)
                    int? retryAfter = ex.Parameters?.RetryAfter;
                    if (ex.ErrorCode == 429 || retryAfter.HasValue)
                    {
                        int seconds = retryAfter ?? 5;
                        await LogAsync($"MessageReceiver rate limited. Retrying after {seconds}s.");
                        await Task.Delay(TimeSpan.FromSeconds(seconds));
                    }
                    else if (ex.ErrorCode == 401)
                    {
                        // Invalid token or unauthorized. Log and back off, but don't crash.
                        await LogAsync("MessageReceiver unauthorized (401). Check bot token.");
                        await Task.Delay(TimeSpan.FromSeconds(30));
                    }
                    else if (ex.ErrorCode >= 500)
                    {
                        // Telegram server error - transient
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                    else
                    {
                        await LogAsync($"MessageReceiver ApiRequestException: {ex.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
                catch (RequestException ex)
                {
                    if (ex.HttpStatusCode == HttpStatusCode.ServiceUnavailable
                        || ex.HttpStatusCode == HttpStatusCode.InternalServerError
                        || ex.Message.Contains("Resource temporarily unavailable"))
                    {
                        // transient network/server issues; back off briefly
                        await Task.Delay(TimeSpan.FromSeconds(2));
                    }
                    else
                    {
                        await LogAsync($"MessageReceiver RequestException: {ex.Message}");
                        await Task.Delay(TimeSpan.FromSeconds(5));
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception
                    await LogAsync($"MessageReceiver Exception:\n{ex}");
                    await Task.Delay(TimeSpan.FromSeconds(5));
                }

                await Task.Delay(TimeSpan.FromSeconds(2)); // Delay to prevent excessive polling
            }
        }

        private static async Task HandleUserResponse(Message message, (UserRegistrationState State, UserData UserData) registrationData)
        {
            long chatId = message.Chat.Id;

            switch (registrationData.State)
            {
                case UserRegistrationState.AwaitingFriendlyName:
                    // Check if FriendlyName is unique
                    if (userDataList.Any(u => u.FriendlyName == message.Text))
                    {
                        await SendMessageAsync(chatId, "This friendly name is already in use. Please start over with /register.");
                        return;
                    }

                    registrationData.UserData.FriendlyName = message.Text;
                    registrationData.State = UserRegistrationState.AwaitingMunicipalityName;
                    await SendMessageAsync(chatId, "Please enter your municipality name (example: Novi Beograd)");
                    break;
                case UserRegistrationState.AwaitingMunicipalityName:
                    registrationData.UserData.MunicipalityName = LatinToCyrillicConverter.ConvertLatinToCyrillic(message.Text);
                    registrationData.State = UserRegistrationState.AwaitingStreetName;
                    await SendMessageAsync(chatId, "Please enter your street name, without the number (example: šumadijska)\n" +
                        "Note: You must use cyrillic letters or letters like ćčšž...");
                    await SendMessageAsync(chatId, "Please enter your street name, without the number (example: šumadijska)");
                    break;
                case UserRegistrationState.AwaitingStreetName:
                    registrationData.UserData.StreetName = LatinToCyrillicConverter.ConvertLatinToCyrillic(message.Text);
                    _ = userRegistrationData.Remove(chatId); // Registration complete
                    await RegisterUser(registrationData.UserData);
                    break;
            }

            userRegistrationData[chatId] = registrationData; // Update the user's registration state
        }

        private static readonly Dictionary<long, (UserRegistrationState State, UserData UserData)> userRegistrationData = new Dictionary<long, (UserRegistrationState, UserData)>();

        private static async Task HandleCommand(Message message)
        {
            if (message == null || message.Text == null)
            {
                return;
            }

            string[] messageText = message.Text.Split(' ');

            switch (messageText.First())
            {
                case "/register":
                    userRegistrationData[message.Chat.Id] = (UserRegistrationState.AwaitingFriendlyName, new UserData { ChatId = message.Chat.Id });
                    await SendMessageAsync(message.Chat.Id, "Please enter your friendly name:");
                    break;
                case "/unregister":
                    await UnregisterUser(message.Chat.Id);
                    break;
                case "/aboutme":
                    await DisplayUserInfo(message.Chat.Id);
                    break;
            }
        }

        private static async Task RegisterUser(UserData userData)
        {
            userDataList.Add(userData);
            UserDataStore.WriteUserData(userDataList); // Persist the new user
            await SendMessageAsync(userData.ChatId, $"You have been successfully registered as {userData.FriendlyName}.");
            await LogAsync($"User registered:{userData.FriendlyName}, {userData.MunicipalityName}, {userData.StreetName}");
        }

        private static async Task UnregisterUser(long chatId)
        {
            IEnumerable<UserData> users = userDataList.Where(u => u.ChatId == chatId).ToList();
            if (users.Count() == 0)
            {
                await SendMessageAsync(chatId, "You are not registered.");
            }
            else
            {
                foreach (UserData? user in users)
                {

                    _ = userDataList.Remove(user);
                }

                UserDataStore.WriteUserData(userDataList); // Update the stored data
                await SendMessageAsync(chatId, "You have been successfully unregistered.");

                foreach (UserData? user in users)
                {
                    await LogAsync($"User unregistered:{user.FriendlyName}, {user.MunicipalityName}, {user.StreetName}");
                }
            }
        }

        private static async Task DisplayUserInfo(long chatId)
        {
            IEnumerable<UserData> users = userDataList.Where(u => u.ChatId == chatId);
            if (users.Count() == 0)
            {
                await SendMessageAsync(chatId, "You are not currently registered.");
            }
            else
            {
                string userInfo = "";
                foreach (UserData? user in users)
                {
                    userInfo +=
                        $"Friendly Name: {user.FriendlyName}\n" +
                        $"Municipality Name: {user.MunicipalityName}\n" +
                        $"Street Name: {user.StreetName}\n\n";
                }

                await SendMessageAsync(chatId, $"Here is the information I have on you:\n{userInfo}");
            }
        }

        private static async Task LogAsync(string message)
        {
            Console.WriteLine(message);
            if (logChatId.HasValue)
            {
                try
                {
                    _ = await botClient.SendTextMessageAsync(logChatId.Value, message);
                }
                catch (Exception ex)
                {
                    // Logging to Telegram failed; write to console and continue
                    Console.WriteLine($"Failed to send log to Telegram: {ex.Message}");
                }
            }
        }

        private static readonly Dictionary<(NotificationType, long, string), DateTime> lastNotificationTimes = [];

        private static async Task NotifyUserAsync(NotificationType notificationType, long chatId, string message)
        {
            // Check if it's past noon and if a notification has not been
            // sent today for the specific user and specific notification type
            if (DateTime.Now.Hour >= 12)
            {
                var key = (notificationType, chatId, message);
                if (lastNotificationTimes.TryGetValue(key, out DateTime lastNotificationTime))
                {
                    if (DateTime.Today > lastNotificationTime.Date)
                    {
                        await SendMessageAsync(chatId, message);
                        lastNotificationTimes[key] = DateTime.Now; // Update last notification time
                    }
                }
                else
                {
                    await SendMessageAsync(chatId, message);
                    lastNotificationTimes.Add(key, DateTime.Now); // Add new entry for user
                }
            }
        }

        private static async Task SendMessageAsync(long chatId, string message)
        {
            Console.WriteLine($"sending message... chatId={chatId} message={message}");

            int attempt = 0;
            const int maxAttempts = 3;
            TimeSpan delay = TimeSpan.FromSeconds(2);

            while (true)
            {
                try
                {
                    _ = await botClient.SendTextMessageAsync(chatId, message);
                    return;
                }
                catch (ApiRequestException ex)
                {
                    int? retryAfter = ex.Parameters?.RetryAfter;

                    if (ex.ErrorCode == 429 || retryAfter.HasValue)
                    {
                        int seconds = retryAfter ?? 5;
                        Console.WriteLine($"Rate limited when sending to {chatId}. Retrying after {seconds}s.");
                        await Task.Delay(TimeSpan.FromSeconds(seconds));
                        // do not increment attempt on known rate limit; Telegram dictates backoff
                        continue;
                    }
                    else if (ex.ErrorCode == 403)
                    {
                        // Bot was blocked by the user or cannot message the user; log and stop retrying
                        await LogAsync($"Cannot send message to {chatId}: forbidden (user blocked bot or no permission).");
                        return;
                    }
                    else if (ex.ErrorCode >= 500)
                    {
                        // Server-side error; retry with backoff
                        attempt++;
                        if (attempt >= maxAttempts)
                        {
                            await LogAsync($"Failed to send message to {chatId} after {attempt} attempts (server error): {ex.Message}");
                            return;
                        }

                        await Task.Delay(delay);
                        delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                    }
                    else
                    {
                        // Other API error; do a limited retry then give up
                        attempt++;
                        if (attempt >= maxAttempts)
                        {
                            await LogAsync($"Failed to send message to {chatId} after {attempt} attempts: {ex.Message}");
                            return;
                        }

                        await Task.Delay(delay);
                        delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                    }
                }
                catch (TaskCanceledException ex)
                {
                    // Timeout or cancellation; retry with backoff
                    attempt++;
                    if (attempt >= maxAttempts)
                    {
                        await LogAsync($"Timeout sending message to {chatId} after {attempt} attempts: {ex.Message}");
                        return;
                    }

                    await Task.Delay(delay);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                }
                catch (Exception ex)
                {
                    // Unexpected error; retry a couple of times then give up
                    attempt++;
                    if (attempt >= maxAttempts)
                    {
                        await LogAsync($"Unexpected error sending message to {chatId} after {attempt} attempts: {ex.Message}");
                        return;
                    }

                    await Task.Delay(delay);
                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                }
            }
        }

        /// <summary>
        /// Checks for power outages and notifies the users.
        /// </summary>
        /// <returns>Awaitable void.</returns>
        public static async Task CheckAndNotifyPowerOutageAsync()
        {
            foreach (string url in powerOutageUrls)
            {
                // Create a new HtmlWeb instance
                HtmlWeb web = new HtmlWeb();

                // Load the HTML document from the specified URL
                HtmlDocument document = web.Load(url);

                // Find the table rows in the document
                HtmlNodeCollection rows = document.DocumentNode.SelectNodes("//table//tr");

                // Iterate through the rows
                foreach (HtmlNode row in rows)
                {
                    // Find the cells in the current row
                    HtmlNodeCollection cells = row.SelectNodes("td");

                    // Check if the row has the correct number of cells
                    if (cells != null && cells.Count >= 3)
                    {
                        // Get the municipality name from the first cell
                        string municipality = cells[0].InnerText.Trim();

                        // Get the street name from the second cell
                        string streets = cells[2].InnerText.Trim();

                        foreach (UserData user in userDataList)
                        {
                            if (user.StreetName == null)
                            {
                                continue;
                            }

                            // Check if the street name occurs in the same row as the correct municipality name
                            if (municipality == user.MunicipalityName
                                && streets.IndexOf(user.StreetName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                string streetWithNumber = streets[streets.IndexOf(user.StreetName, StringComparison.OrdinalIgnoreCase)..];
                                int commaIndex = streetWithNumber.IndexOf(',');
                                if (commaIndex >= 0)
                                {
                                    streetWithNumber = streetWithNumber[..commaIndex];
                                }

                                int daysLeftUntilOutage = powerOutageUrls.IndexOf(url);

                                try
                                {
                                    await NotifyUserAsync(
                                        NotificationType.PowerOutage,
                                        user.ChatId,
                                        $"Power outage will occur in {daysLeftUntilOutage} days in {user.MunicipalityName}, {streetWithNumber}.");
                                }
                                catch (Exception e)
                                {
                                    // Log and continue without crashing the periodic loop
                                    await LogAsync($"Failed to notify user {user.ChatId}: {e.Message}");
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks for water outages and notifies the users.
        /// </summary>
        /// <returns>Awaitable void.</returns>
        public static async Task CheckAndNotifyWaterOutageAsync()
        {
            foreach (string url in waterOutageUrls)
            {
                // Create a new HtmlWeb instance
                HtmlWeb web = new HtmlWeb();

                // Load the HTML document from the specified URL
                HtmlDocument document = web.Load(url);

                HtmlNodeCollection workNodes = document.DocumentNode.SelectNodes("//div[contains(@class, 'toggle_content')]");
                if (workNodes != null)
                {
                    foreach (HtmlNode workNode in workNodes)
                    {
                        string nodeText = workNode.InnerText;

                        foreach (UserData user in userDataList)
                        {
                            if (user.StreetName == null
                                || user.MunicipalityName == null)
                            {
                                continue;
                            }

                            string declinationRoot = user.StreetName[..^2];

                            // Check if the street name occurs in the same entry as the correct municipality name
                            if (nodeText.IndexOf(user.MunicipalityName, StringComparison.OrdinalIgnoreCase) >= 0
                                && nodeText.IndexOf(declinationRoot, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                await NotifyUserAsync(
                                    NotificationType.PlannedWaterOutage,
                                    user.ChatId,
                                    $"Water outage might occurr in {user.MunicipalityName}, {user.StreetName}.\n{nodeText}");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Checks for unplanned water outages and notifies the users.
        /// </summary>
        /// <returns>Awaitable void.</returns>
        public static async Task CheckAndNotifyUnplannedWaterOutageAsync()
        {
            foreach (string url in waterUnplannedOutageUrls)
            {
                // Create a new HtmlWeb instance
                HtmlWeb web = new HtmlWeb();

                // Load the HTML document from the specified URL
                HtmlDocument document = web.Load(url);

                HtmlNodeCollection divElements = document.DocumentNode.SelectNodes("//div[@class='toggle_content invers-color ' and @itemprop='text']");
                if (divElements != null)
                {
                    foreach (HtmlNode divElement in divElements)
                    {
                        // Find the ul element within each div element
                        HtmlNode ulElement = divElement.SelectSingleNode(".//ul");

                        if (ulElement != null)
                        {
                            // Iterate through each li element within the ul element
                            foreach (HtmlNode liElement in ulElement.Descendants("li"))
                            {
                                // Check for string occurrences
                                string text = liElement.InnerText;

                                foreach (UserData user in userDataList)
                                {
                                    if (user.StreetName == null
                                        || user.MunicipalityName == null)
                                    {
                                        continue;
                                    }

                                    // Example: Check for the string "example" in each li element (case-insensitive)
                                    if (text.IndexOf(user.MunicipalityName, StringComparison.OrdinalIgnoreCase) >= 0
                                        && text.IndexOf(user.StreetName, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        await NotifyUserAsync(
                                            NotificationType.UnplannedWaterOutage,
                                            user.ChatId,
                                            $"Water outage might be happening in {user.MunicipalityName}, {user.StreetName}.\n{text}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
