# Street Number Filtering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Filter power outage notifications by street number so users only get notified when their house number falls within the affected range.

**Architecture:** Add an optional `StreetNumber` field to `UserData`, a new registration step, and a pure static helper method that parses EPS street number ranges and checks if the user's number is included. Integrate into the existing `CheckAndNotifyPowerOutageAsync` flow.

**Tech Stack:** C# / .NET 8, MSTest, CsvHelper, HtmlAgilityPack

---

### Task 1: Add `StreetNumber` to Data Model

**Files:**
- Modify: `src/PowerOutageNotifierService/UserData.cs`
- Modify: `src/PowerOutageNotifierService/UserDataStore.cs`

- [ ] **Step 1: Add `StreetNumber` property to `UserData.cs`**

In `src/PowerOutageNotifierService/UserData.cs`, add after the `StreetName` property:

```csharp
/// <summary>
/// The street number of the user (optional).
/// When null, the user receives notifications for all numbers on their street.
/// </summary>
[Name("Street Number")]
public string? StreetNumber { get; set; }
```

- [ ] **Step 2: Update the CSV header in `UserDataStore.cs`**

In `src/PowerOutageNotifierService/UserDataStore.cs`, change the header line in the `ReadUserData` method from:

```csharp
File.WriteAllText(csvFilePath, "Friendly Name,Chat ID,Municipality Name,Street Name\n");
```

to:

```csharp
File.WriteAllText(csvFilePath, "Friendly Name,Chat ID,Municipality Name,Street Name,Street Number\n");
```

- [ ] **Step 3: Update the CSV header comment in `UserDataStore.cs`**

Change the example comment from:

```csharp
/// Friendly Name,Chat ID,Municipality Name,Street Name
/// PositiveTest,123456,Палилула,САВЕ МРКАЉА
```

to:

```csharp
/// Friendly Name,Chat ID,Municipality Name,Street Name,Street Number
/// PositiveTest,123456,Палилула,САВЕ МРКАЉА,31В
```

- [ ] **Step 4: Build to verify compilation**

Run: `dotnet build src/PowerOutageNotifierService/PowerOutageNotifier.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/PowerOutageNotifierService/UserData.cs src/PowerOutageNotifierService/UserDataStore.cs
git commit -m "feat: add optional StreetNumber field to UserData"
```

---

### Task 2: Add `AwaitingStreetNumber` Registration State

**Files:**
- Modify: `src/PowerOutageNotifierService/UserRegistrationState.cs`

- [ ] **Step 1: Add `AwaitingStreetNumber` enum value**

In `src/PowerOutageNotifierService/UserRegistrationState.cs`, add after `AwaitingStreetName`:

```csharp
/// <summary>
/// We are awaiting the street number of the user.
/// </summary>
AwaitingStreetNumber,
```

The full enum should now be: `None`, `AwaitingFriendlyName`, `AwaitingMunicipalityName`, `AwaitingStreetName`, `AwaitingStreetNumber`.

- [ ] **Step 2: Build to verify compilation**

Run: `dotnet build src/PowerOutageNotifierService/PowerOutageNotifier.csproj`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/PowerOutageNotifierService/UserRegistrationState.cs
git commit -m "feat: add AwaitingStreetNumber registration state"
```

---

### Task 3: Update Registration Flow in `MainService.cs`

**Files:**
- Modify: `src/PowerOutageNotifierService/MainService.cs`

- [ ] **Step 1: Change `AwaitingStreetName` case to transition to `AwaitingStreetNumber` instead of completing registration**

In `src/PowerOutageNotifierService/MainService.cs`, find the `AwaitingStreetName` case in `HandleUserResponse`:

```csharp
case UserRegistrationState.AwaitingStreetName:
    registrationData.UserData.StreetName = LatinToCyrillicConverter.ConvertLatinToCyrillic(message.Text);
    _ = userRegistrationData.Remove(chatId); // Registration complete
    await RegisterUser(registrationData.UserData);
    break;
```

Replace with:

```csharp
case UserRegistrationState.AwaitingStreetName:
    registrationData.UserData.StreetName = LatinToCyrillicConverter.ConvertLatinToCyrillic(message.Text);
    registrationData.State = UserRegistrationState.AwaitingStreetNumber;
    await SendMessageAsync(chatId, "Please enter your street number (example: 31В), or type 'skip' to receive notifications for all numbers on this street.");
    break;
```

- [ ] **Step 2: Add `AwaitingStreetNumber` case**

After the `AwaitingStreetName` case, add:

```csharp
case UserRegistrationState.AwaitingStreetNumber:
    if (message.Text != null
        && message.Text.Trim().Equals("skip", StringComparison.OrdinalIgnoreCase))
    {
        // User chose to skip — StreetNumber stays null
    }
    else
    {
        registrationData.UserData.StreetNumber = LatinToCyrillicConverter.ConvertLatinToCyrillic(message.Text);
    }

    _ = userRegistrationData.Remove(chatId); // Registration complete
    await RegisterUser(registrationData.UserData);
    break;
```

- [ ] **Step 3: Update `/aboutme` display to include street number**

In the `DisplayUserInfo` method, find:

```csharp
userInfo +=
    $"Friendly Name: {user.FriendlyName}\n" +
    $"Municipality Name: {user.MunicipalityName}\n" +
    $"Street Name: {user.StreetName}\n\n";
```

Replace with:

```csharp
userInfo +=
    $"Friendly Name: {user.FriendlyName}\n" +
    $"Municipality Name: {user.MunicipalityName}\n" +
    $"Street Name: {user.StreetName}\n" +
    $"Street Number: {user.StreetNumber ?? "(all numbers)"}\n\n";
```

- [ ] **Step 4: Build to verify compilation**

Run: `dotnet build src/PowerOutageNotifierService/PowerOutageNotifier.csproj`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/PowerOutageNotifierService/MainService.cs
git commit -m "feat: add street number step to registration flow"
```

---

### Task 4: Write Failing Tests for Range Matching

**Files:**
- Create: `test/StreetNumberMatchingTests.cs`

- [ ] **Step 1: Create test file with all test cases**

Create `test/StreetNumberMatchingTests.cs`:

```csharp
namespace Tests
{
    using PowerOutageNotifier.PowerOutageNotifierService;

    [TestClass]
    public class StreetNumberMatchingTests
    {
        [TestMethod]
        public void NumberInSimpleRange_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 29-35", "31В"));
        }

        [TestMethod]
        public void NumberNotInSimpleRange_ReturnsFalse()
        {
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 55-57", "31В"));
        }

        [TestMethod]
        public void ExactSingleNumberMatch_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 22А", "22"));
        }

        [TestMethod]
        public void SingleNumberNoMatch_ReturnsFalse()
        {
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 22А", "31В"));
        }

        [TestMethod]
        public void RangeWithLetterSuffixOnEndpoints_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 15Б-41", "31В"));
        }

        [TestMethod]
        public void SameNumberDifferentLetterSuffix_ReturnsTrue()
        {
            // 46А-46Б → range 46 to 46
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 46А-46Б", "46В"));
        }

        [TestMethod]
        public void MultipleCommaSegments_MatchInSecond_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 10-28,34-42,46А-46Б,15Б-41", "38"));
        }

        [TestMethod]
        public void MultipleCommaSegments_NoMatch_ReturnsFalse()
        {
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 10-12,46А-46Б", "31В"));
        }

        [TestMethod]
        public void NoColonInInput_FallbackTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА", "31В"));
        }

        [TestMethod]
        public void AllSegmentsUnparseable_FallbackTrue()
        {
            // ББ is "без броја" (no number) — can't parse → fallback
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: ББ", "31В"));
        }

        [TestMethod]
        public void UserNumberWithLetterSuffix_MatchesRange()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 2-48", "31В"));
        }

        [TestMethod]
        public void UserNumberWithLetterSuffix_OutOfRange()
        {
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 2-10", "31В"));
        }

        [TestMethod]
        public void UserNumberIsExactBoundary_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 10-31", "31"));
        }

        [TestMethod]
        public void UserNumberIsExactLowerBoundary_ReturnsTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 31-50", "31В"));
        }

        [TestMethod]
        public void UserNumberNoDigits_FallbackTrue()
        {
            // User street number has no numeric part — can't compare, fallback to notify
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: 10-20", "ББ"));
        }

        [TestMethod]
        public void MixedParseableAndUnparseable_MatchFound()
        {
            // ББ is unparseable but 29-35 matches
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: ББ,29-35", "31В"));
        }

        [TestMethod]
        public void MixedParseableAndUnparseable_NoMatch()
        {
            // ББ is unparseable but 55-57 doesn't match — at least one segment was parsed
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("КЕЛТСКА: ББ,55-57", "31В"));
        }

        [TestMethod]
        public void EmptyAfterColon_FallbackTrue()
        {
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("КЕЛТСКА: ", "31В"));
        }

        [TestMethod]
        public void RealWorldExample_NotInRange()
        {
            // User at 31В, outage at 55-57 — should NOT notify
            Assert.IsFalse(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 55-57", "31В"));
        }

        [TestMethod]
        public void RealWorldExample_InRange()
        {
            // User at 31В, outage at 20-40 — should notify
            Assert.IsTrue(MainService.IsUserStreetNumberInRange("ЏОНА КЕНЕДИЈА: 20-40", "31В"));
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test test/Tests.csproj --filter "FullyQualifiedName~StreetNumberMatchingTests"`
Expected: Compilation error — `MainService.IsUserStreetNumberInRange` does not exist yet.

- [ ] **Step 3: Commit failing tests**

```bash
git add test/StreetNumberMatchingTests.cs
git commit -m "test: add failing tests for street number range matching"
```

---

### Task 5: Implement `IsUserStreetNumberInRange`

**Files:**
- Modify: `src/PowerOutageNotifierService/MainService.cs`

- [ ] **Step 1: Add the static helper method**

In `src/PowerOutageNotifierService/MainService.cs`, add the following method inside the `MainService` class (e.g., just before the `CheckAndNotifyPowerOutageAsync` method):

```csharp
/// <summary>
/// Checks if the user's street number falls within the number ranges
/// listed in the scraped street text (e.g., "ЏОНА КЕНЕДИЈА: 55-57,22А").
/// Returns true if the number is in range, or if the range can't be parsed (safety fallback).
/// </summary>
internal static bool IsUserStreetNumberInRange(string streetWithNumber, string userStreetNumber)
{
    // Extract the segment after the colon
    int colonIndex = streetWithNumber.IndexOf(':');
    if (colonIndex < 0)
    {
        return true; // No colon found — can't parse, fallback to notify
    }

    string numbersPart = streetWithNumber[(colonIndex + 1)..].Trim();
    if (string.IsNullOrEmpty(numbersPart))
    {
        return true; // Nothing after colon — fallback to notify
    }

    // Extract the numeric part of the user's street number
    if (!TryExtractNumber(userStreetNumber, out int userNumber))
    {
        return true; // Can't extract user's number — fallback to notify
    }

    // Split by comma and check each segment
    string[] segments = numbersPart.Split(',');
    bool anySegmentParsed = false;

    foreach (string rawSegment in segments)
    {
        string segment = rawSegment.Trim();
        if (string.IsNullOrEmpty(segment))
        {
            continue;
        }

        // Check if it's a range (contains a dash between numbers)
        int dashIndex = FindRangeDash(segment);
        if (dashIndex > 0)
        {
            // Range: e.g., "55-57" or "15Б-41"
            string startPart = segment[..dashIndex].Trim();
            string endPart = segment[(dashIndex + 1)..].Trim();

            if (TryExtractNumber(startPart, out int rangeStart)
                && TryExtractNumber(endPart, out int rangeEnd))
            {
                anySegmentParsed = true;
                if (userNumber >= rangeStart && userNumber <= rangeEnd)
                {
                    return true;
                }
            }
        }
        else
        {
            // Single number: e.g., "22А"
            if (TryExtractNumber(segment, out int singleNumber))
            {
                anySegmentParsed = true;
                if (userNumber == singleNumber)
                {
                    return true;
                }
            }
        }
    }

    // If no segments could be parsed at all, fallback to notify
    return !anySegmentParsed;
}

/// <summary>
/// Extracts the leading numeric part from a string like "31В" → 31, "55" → 55.
/// Returns false if no leading digits found.
/// </summary>
private static bool TryExtractNumber(string value, out int number)
{
    number = 0;
    int i = 0;
    while (i < value.Length && char.IsDigit(value[i]))
    {
        i++;
    }

    if (i == 0)
    {
        return false;
    }

    return int.TryParse(value[..i], out number);
}

/// <summary>
/// Finds the index of the dash that separates a range (e.g., "15Б-41").
/// Returns -1 if no valid range dash is found.
/// Skips leading characters to avoid treating the start of a string as a dash.
/// </summary>
private static int FindRangeDash(string segment)
{
    // A range dash appears after at least one digit (or digit+letter).
    // Scan past the leading number+suffix to find a dash.
    int i = 0;

    // Skip leading digits
    while (i < segment.Length && char.IsDigit(segment[i]))
    {
        i++;
    }

    // Skip optional letter suffix (e.g., the "Б" in "15Б-41")
    while (i < segment.Length && char.IsLetter(segment[i]))
    {
        i++;
    }

    // Now look for a dash
    if (i < segment.Length && segment[i] == '-')
    {
        return i;
    }

    return -1;
}
```

- [ ] **Step 2: Run the tests to verify they pass**

Run: `dotnet test test/Tests.csproj --filter "FullyQualifiedName~StreetNumberMatchingTests"`
Expected: All tests pass.

- [ ] **Step 3: Also run existing tests to check for regressions**

Run: `dotnet test test/Tests.csproj`
Expected: All tests pass (including LatinToCyrillicConverterTests).

- [ ] **Step 4: Commit**

```bash
git add src/PowerOutageNotifierService/MainService.cs
git commit -m "feat: implement IsUserStreetNumberInRange with range parsing"
```

---

### Task 6: Integrate Number Filtering into `CheckAndNotifyPowerOutageAsync`

**Files:**
- Modify: `src/PowerOutageNotifierService/MainService.cs`

- [ ] **Step 1: Add number check in the notification block**

In `src/PowerOutageNotifierService/MainService.cs`, find the power outage matching block inside `CheckAndNotifyPowerOutageAsync`:

```csharp
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
```

Replace with:

```csharp
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

    // If user has a street number, check if it falls within the affected range
    if (user.StreetNumber != null
        && !IsUserStreetNumberInRange(streetWithNumber, user.StreetNumber))
    {
        continue; // User's number is not in the affected range — skip
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
```

The key addition is the 5-line block:
```csharp
if (user.StreetNumber != null
    && !IsUserStreetNumberInRange(streetWithNumber, user.StreetNumber))
{
    continue;
}
```

- [ ] **Step 2: Build and run all tests**

Run: `dotnet test test/Tests.csproj`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add src/PowerOutageNotifierService/MainService.cs
git commit -m "feat: integrate street number filtering into power outage notifications"
```

---

### Task 7: Final Verification

- [ ] **Step 1: Full build**

Run: `dotnet build src/PowerOutageNotifierService/PowerOutageNotifier.csproj`
Expected: Build succeeded, no warnings.

- [ ] **Step 2: Full test run**

Run: `dotnet test test/Tests.csproj -v normal`
Expected: All tests pass.

- [ ] **Step 3: Commit any remaining changes**

Verify with `git status` that the working tree is clean. If any uncommitted changes remain, commit them.
