using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace PowerOutageNotifier
{
    public class MainService
    {
        private static readonly long? logChatId =
            long.TryParse(Environment.GetEnvironmentVariable("LOG_CHAT_ID"), out long chatId)
                ? chatId
                : null;

        private static readonly string telegramBotToken = ConfigReader.ReadBotToken();

        private static readonly TelegramBotClient botClient = new TelegramBotClient(telegramBotToken);

        public static readonly List<UserData> userDataList = UserDataStore.ReadUserData();

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

        public Task Start(TimeSpan? frequency = null)
        {
            if (frequency == null)
            {
                frequency = TimeSpan.FromHours(1);
            }

            LogAsync($"Service running on {Environment.MachineName}").GetAwaiter().GetResult();

            Task messageReceiverTask = this.MessageReceiver(); // Start the message receiver task

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

                            Thread.Sleep(frequency.Value);
                        }
                        catch (Exception ex)
                        {
                            LogAsync($"Exception in periodic task: {ex.Message}").GetAwaiter().GetResult();
                        }
                    }
                })
            );
        }

        public void Stop() => LogAsync($"Service stopping on {Environment.MachineName}").GetAwaiter().GetResult();

        private async Task MessageReceiver()
        {
            int offset = 0; // Identifier of the first update to be returned

            while (true)
            {
                try
                {
                    Update[] updates = await botClient.GetUpdatesAsync(offset);

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
                                    await this.HandleUserResponse(message, registrationData);
                                }
                            }
                        }

                        offset = update.Id + 1;
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception
                    await LogAsync($"MessageReceiver Exception: {ex}");
                }

                await Task.Delay(1000); // Delay to prevent excessive polling
            }
        }

        private async Task HandleUserResponse(Message message, (UserRegistrationState State, UserData UserData) registrationData)
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
                    await SendMessageAsync(chatId, "Please enter your EXACT municipality name (example: Novi Beograd)");
                    break;
                case UserRegistrationState.AwaitingMunicipalityName:
                    registrationData.UserData.MunicipalityName = LatinToCyrillicConverter.ConvertLatinToCyrillic(message.Text);
                    registrationData.State = UserRegistrationState.AwaitingStreetName;
                    await SendMessageAsync(chatId, "Please enter your EXACT street name, without the number (example: Husinjskih Rudara)");
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
            IEnumerable<UserData> users = userDataList.Where(u => u.ChatId == chatId);
            if (users.Count() == 0)
            {
                await SendMessageAsync(chatId, "You are not registered.");
            }
            else
            {
                foreach (UserData? user in users)
                {

                    _ = userDataList.Remove(user);
                    UserDataStore.WriteUserData(userDataList); // Update the stored data
                }

                await SendMessageAsync(chatId, "You have been successfully unregistered.");
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
                _ = await botClient.SendTextMessageAsync(logChatId.Value, message);
            }
        }

        private static async Task SendMessageAsync(long chatId, string message)
        {
            Console.WriteLine($"sending message... chatId={chatId} message={message}");
            await botClient.SendTextMessageAsync(chatId, message);
        }

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
                                streetWithNumber = streetWithNumber[..streets.IndexOf(',')];

                                int daysLeftUntilOutage = powerOutageUrls.IndexOf(url);

                                await SendMessageAsync(
                                    user.ChatId,
                                    $"Power outage will occur in {daysLeftUntilOutage} days in {user.MunicipalityName}, {streetWithNumber}.");
                            }
                        }
                    }
                }
            }
        }

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
                                await SendMessageAsync(
                                    user.ChatId,
                                    $"Water outage might occurr in {user.MunicipalityName}, {user.StreetName}.\n{nodeText}");
                            }
                        }
                    }
                }
            }
        }

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
                                        await SendMessageAsync(
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

        // TODO - implement so that it can run in a docker container
        public static async Task CheckAndNotifyParkingTicketsAsync()
        {
            string licensePlate = "BG677XX";
            string url = "https://www.parking-servis.co.rs/lat/edpk";
            string searchKeyword = "NEMA EVIDENTIRANE ELEKTRONSKE";

            // Set up ChromeDriver
            ChromeOptions options = new ChromeOptions();
            //options.AddArgument("--headless"); // Run in headless mode (without opening a browser window)
            using IWebDriver driver = new ChromeDriver(options);
            driver.Navigate().GoToUrl(url);

            // Find the input field and enter the license plate
            IWebElement inputElement = driver.FindElement(By.CssSelector("input[name='fine']"));
            inputElement.Clear();
            inputElement.SendKeys(licensePlate);

            // Find and click the submit button
            IWebElement submitButton = driver.FindElement(By.CssSelector("button[type='submit']"));
            submitButton.Click();

            // Wait for the presence of the result message
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            By resultLocator = By.CssSelector("div.entry-text.no-edpk-message");
            IWebElement resultElement = wait.Until(ExpectedConditions.ElementIsVisible(resultLocator));

            // Check if the result element contains the keyword
            if (resultElement.Text.Contains(searchKeyword))
            {
                Console.WriteLine($"The keyword '{searchKeyword}' was found on the website.");
            }
            else
            {
                await SendMessageAsync(
                    userDataList.Where(
                        user => user.FriendlyName != null && user.FriendlyName.Contains("Ajanko"))
                    .First()
                    .ChatId,
                    $"There is a parking fine at {url}");

                await SendMessageAsync(
                    userDataList.Where(
                        user => user.FriendlyName != null && user.FriendlyName.Contains("Ajanko"))
                    .First()
                    .ChatId,
                    licensePlate);
            }
        }
    }
}
