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
            long.TryParse(Environment.GetEnvironmentVariable("LOG_CHAT_ID"), out var chatId)
                ? chatId
                : null;
        
        private static readonly string telegramBotToken = ConfigReader.ReadBotToken();

        private static readonly TelegramBotClient botClient = new TelegramBotClient(telegramBotToken);

        public static readonly List<UserData> userDataList = UserDataStore.ReadUserData();

        // URLs of the web page to scrape
        private static readonly List<string> powerOutageUrls = new List<string>
        {
            "http://www.epsdistribucija.rs/planirana-iskljucenja-beograd/Dan_0_Iskljucenja.htm",
            "http://www.epsdistribucija.rs/planirana-iskljucenja-beograd/Dan_1_Iskljucenja.htm",
            "http://www.epsdistribucija.rs/planirana-iskljucenja-beograd/Dan_2_Iskljucenja.htm",
            "http://www.epsdistribucija.rs/planirana-iskljucenja-beograd/Dan_3_Iskljucenja.htm",
        };

        private static readonly List<string> waterOutageUrls = new List<string>
        {
            "https://www.bvk.rs/planirani-radovi/",
        };

        private static readonly List<string> waterUnplannedOutageUrls = new List<string>
        {
            "https://www.bvk.rs/kvarovi-na-mrezi/",
        };

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

        public void Stop()
        {
            LogAsync($"Service stopping on {Environment.MachineName}").GetAwaiter().GetResult();
        }

        private async Task MessageReceiver()
        {
            int offset = 0; // Identifier of the first update to be returned

            while (true)
            {
                try
                {
                    var updates = await botClient.GetUpdatesAsync(offset);

                    foreach (var update in updates)
                    {
                        if (update.Type == Telegram.Bot.Types.Enums.UpdateType.Message)
                        {
                            var message = update.Message;
                            if (message != null && message.Text != null)
                            {
                                if (userRegistrationData.TryGetValue(message.Chat.Id, out var registrationData))
                                {
                                    await HandleUserResponse(message, registrationData);
                                }
                                else if (message.Text.StartsWith("/"))
                                {
                                    await HandleCommand(message);
                                }
                            }
                        }

                        offset = update.Id + 1;
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception
                    LogAsync($"MessageReceiver Exception: {ex.Message}").GetAwaiter().GetResult();
                }

                await Task.Delay(1000); // Delay to prevent excessive polling (adjust as needed)
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
                    registrationData.State = UserRegistrationState.AwaitingDistrictName;
                    await SendMessageAsync(chatId, "Please enter your district name in ЋИРИЛИЦА:");
                    break;
                case UserRegistrationState.AwaitingDistrictName:
                    registrationData.UserData.DistrictName = message.Text;
                    registrationData.State = UserRegistrationState.AwaitingStreetName;
                    await SendMessageAsync(chatId, "Please enter your street name in ЋИРИЛИЦА:");
                    break;
                case UserRegistrationState.AwaitingStreetName:
                    registrationData.UserData.StreetName = message.Text;
                    userRegistrationData.Remove(chatId); // Registration complete
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
            var messageText = message.Text.Split(' ');

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
        }

        private static async Task UnregisterUser(long chatId)
        {
            var users = userDataList.Where(u => u.ChatId == chatId);
            if (users.Count() == 0)
            {
                await SendMessageAsync(chatId, "You are not registered.");
            }
            else
            {
                foreach (var user in users)
                {

                    userDataList.Remove(user);
                    UserDataStore.WriteUserData(userDataList); // Update the stored data
                }

                await SendMessageAsync(chatId, "You have been successfully unregistered.");
            }
        }

        private static async Task DisplayUserInfo(long chatId)
        {
            var users = userDataList.Where(u => u.ChatId == chatId);
            if (users.Count() == 0)
            {
                await SendMessageAsync(chatId, "You are not currently registered.");
            }
            else
            {
                string userInfo = "";
                foreach (var user in users)
                {
                    userInfo +=
                        $"Friendly Name: {user.FriendlyName}\n" +
                        $"District Name: {user.DistrictName}\n" +
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
                await botClient.SendTextMessageAsync(logChatId.Value, message);
            }
        }

        private static async Task SendMessageAsync(long chatId, string message)
        {
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
                        // Get the district name from the first cell
                        string district = cells[0].InnerText.Trim();

                        // Get the street name from the second cell
                        string streets = cells[2].InnerText.Trim();

                        foreach (var user in userDataList)
                        {
                            if (user.StreetName == null)
                            {
                                continue;
                            }

                            // Check if the street name occurs in the same row as the correct district name
                            if (district == user.DistrictName
                                && streets.IndexOf(user.StreetName, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                string streetWithNumber = streets.Substring(streets.IndexOf(user.StreetName, StringComparison.OrdinalIgnoreCase));
                                streetWithNumber = streetWithNumber.Substring(0, streets.IndexOf(','));

                                Console.WriteLine(
                                    $"Power outage detected. {user.FriendlyName}, {user.DistrictName}, {streetWithNumber}, {user.ChatId}");

                                int daysLeftUntilOutage = powerOutageUrls.IndexOf(url);

                                await SendMessageAsync(
                                    user.ChatId,
                                    $"Power outage will occur in {daysLeftUntilOutage} days in {user.DistrictName}, {streetWithNumber}.");
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

                        foreach (var user in userDataList)
                        {
                            if (user.StreetName == null
                                || user.DistrictName == null)
                            {
                                continue;
                            }

                            string declinationRoot = user.StreetName.Substring(0, user.StreetName.Length - 2);

                            // Check if the street name occurs in the same entry as the correct district name
                            if (nodeText.IndexOf(user.DistrictName, StringComparison.OrdinalIgnoreCase) >= 0
                                && nodeText.IndexOf(declinationRoot, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                Console.WriteLine(
                                    $"Water outage detected. {user.FriendlyName}, {user.DistrictName}, {user.StreetName}, {user.ChatId}");

                                await SendMessageAsync(
                                    user.ChatId,
                                    $"Water outage might occurr in {user.DistrictName}, {user.StreetName}.\n{nodeText}");
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

                                foreach (var user in userDataList)
                                {
                                    if (user.StreetName == null
                                        || user.DistrictName == null)
                                    {
                                        continue;
                                    }

                                    // Example: Check for the string "example" in each li element (case-insensitive)
                                    if (text.IndexOf(user.DistrictName, StringComparison.OrdinalIgnoreCase) >= 0
                                        && text.IndexOf(user.StreetName, StringComparison.OrdinalIgnoreCase) >= 0)
                                    {
                                        Console.WriteLine($"Water outage detected. {user.FriendlyName}, {user.DistrictName}, {user.StreetName}, {user.ChatId}");

                                        await SendMessageAsync(
                                            user.ChatId,
                                            $"Water outage might be happening in {user.DistrictName}, {user.StreetName}.\n{text}");
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static async Task CheckAndNotifyParkingTicketsAsync()
        {
            string licensePlate = "BG677XX";
            string url = "https://www.parking-servis.co.rs/lat/edpk";
            string searchKeyword = "NEMA EVIDENTIRANE ELEKTRONSKE";

            // Set up ChromeDriver
            ChromeOptions options = new ChromeOptions();
            //options.AddArgument("--headless"); // Run in headless mode (without opening a browser window)
            using (IWebDriver driver = new ChromeDriver(options))
            {
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
}
