# Street Number Filtering for Power Outage Notifications

**Date:** 2026-04-19
**Status:** Approved

## Problem

Users receive power outage notifications for their entire street, even when the outage only affects a specific range of house numbers. For example, a user at ЏОНА КЕНЕДИЈА 31В gets notified about an outage at ЏОНА КЕНЕДИЈА: 55-57, which doesn't affect them.

## Solution

Add an optional street number field to user data and parse the number ranges from the scraped EPS website data to filter notifications. If the user's number is not in any affected range, skip the notification.

## Design

### 1. Data Model — `UserData.cs`

Add an optional `StreetNumber` property:

```csharp
[Name("Street Number")]
public string? StreetNumber { get; set; }
```

CSV header becomes: `Friendly Name,Chat ID,Municipality Name,Street Name,Street Number`

Backward-compatible: CsvHelper treats missing trailing columns as null. Existing users without a street number continue to receive all notifications for their street.

### 2. Registration Flow — `UserRegistrationState.cs` + `MainService.cs`

Add new enum value `AwaitingStreetNumber` after `AwaitingStreetName`.

After the user enters their street name, the bot asks:
> "Please enter your street number (example: 31В), or type 'skip' to receive notifications for all numbers on this street."

- If the user types "skip": `StreetNumber` stays null, registration completes.
- Otherwise: the entered value is stored after Latin-to-Cyrillic conversion (letter suffixes like В, А, Б are Cyrillic on the website).

### 3. Range Parsing & Matching — new static helper in `MainService.cs`

A pure static method: `IsUserStreetNumberInRange(string streetWithNumber, string userStreetNumber) → bool`

**Input:** `streetWithNumber` is the text already extracted by the current code (e.g., `ЏОНА КЕНЕДИЈА: 55-57`), and `userStreetNumber` is the user's stored number (e.g., `31В`).

**Algorithm:**
1. Extract the segment after the colon (`: `). If no colon found, return `true` (can't parse → notify as fallback).
2. Split by comma to get individual segments: `["55-57", "22А", "10-28"]`.
3. Extract the numeric part of the user's street number by stripping the letter suffix (e.g., `31В` → `31`). If no numeric part can be extracted, return `true` (fallback).
4. For each segment:
   - Strip letter suffixes from both sides of a range.
   - If it's a range (`X-Y`): check if user's number is between X and Y (inclusive).
   - If it's a single number: check if user's number equals it.
   - If the segment can't be parsed as a number at all, skip it.
5. If the user's number matches any segment, return `true`.
6. If no segments matched and at least one segment was successfully parsed, return `false`.
7. If NO segments could be parsed at all, return `true` (fallback — better to notify than miss).

**Edge cases:**
- `ББ` (без броја / no number): not parseable, skipped.
- `22А` → single number 22.
- `15Б-41` → range 15 to 41.
- `46А-46Б` → range 46 to 46 (same number, different letter suffix — matches 46).

### 4. Integration in `CheckAndNotifyPowerOutageAsync`

In the existing match block, after confirming the street name matches:

- If user has no `StreetNumber` (null): notify (current behavior, unchanged).
- If user has a `StreetNumber`: call `IsUserStreetNumberInRange(streetWithNumber, user.StreetNumber)`.
  - `true` → notify.
  - `false` → skip notification.

### 5. `/aboutme` Display

Include street number in the output when present:

```
Friendly Name: ...
Municipality Name: ...
Street Name: ЏОНА КЕНЕДИЈА
Street Number: 31В
```

### 6. Testing

Unit tests for `IsUserStreetNumberInRange` covering:
- Simple range match (number in range)
- Simple range miss (number not in range)
- Single number match/miss
- Range with letter suffixes on endpoints
- User number with letter suffix
- Multiple comma-separated segments
- No colon in input (fallback → true)
- Unparseable segments (fallback → true)
- ББ entries (skipped, don't prevent fallback)
- Null/empty user street number behavior (handled before calling the method)

## Scope

- Power outage notifications only. Water outage pages use a different format (free text) and are not affected by this change.
- No changes to the scraping URLs or schedule.
- No migration needed for existing CSV files — missing column is handled as null.
