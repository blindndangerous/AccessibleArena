# Card Filter + Text Search Combination Bug

## Summary

When combining set filters (Advanced Filters) with a text search in the deck builder collection, the game returns 0 results even though matching cards exist. This is a **game-side bug** — the mod correctly reads whatever cards the game provides, but the game's card pool filtering logic fails when both filters are active simultaneously.

## Reproduction Steps

1. Open deck builder, navigate to collection
2. Enable crafting toggle (to see unowned cards too)
3. Search for "brand" — finds 4 cards including "Brandende Welle"
4. Open Advanced Filters, enable Avatar set filter(s)
5. Confirm filters — search field still shows "brand"
6. Result: **0 cards** displayed (expected: Brandende Welle)
7. Clear the search text — Avatar cards now appear (8+ cards)
8. Re-search "brand" — **0 cards** again

## Investigation Timeline

### Session 1: Initial Discovery (12:54 - 12:56)

```
12:54:51  Deck builder opened, collection visible
12:55:07  Craft ON, search "brand" → 4 cards found:
          - Brandende Welle (Brandende_Welle)
          - Brandungsmaschine
          - Brandende Flutwelle
          - Goblin-Brandbombe
12:55:18  Opens Advanced Filters popup (114 elements, 4 rows)
12:55:26  Avatar Extra set toggle activated (ON)
12:55:28  Avatar base set toggle activated (ON)
12:55:32  OK pressed to apply filters → 0 collection cards
12:55:47  Search "brand" re-submitted
          Log: "Search rescan pool: 0 -> 0"
12:56:05  Search "well" submitted
          Log: "Search rescan pool: 0 -> 0"
12:56:18  Search cleared (empty)
          Log: "Search rescan pool: 0 -> 8"
          → 8 Avatar cards now visible (all creatures):
            Gran Gran, Tigerrobbe, Koh der Gesichterdieb,
            Suki der Kyoshi-Krieger, Zuko der Prinz,
            etc.
```

**Key observation:** Set filter alone works (8 cards). Text search alone works (4 cards including Brandende Welle). Combined: 0 cards.

### Session 2: ExpansionCode Diagnostic (13:20 - 13:22)

Added debug logging to `CardModelProvider.ExtractCardInfo()` to verify Brandende Welle's set code matches other Avatar cards.

#### Debug Logging Code

Added to `src/Core/Services/CardModelProvider.cs` at line ~1858 (within `ExtractCardInfo`):

```csharp
// Expansion/Set - try direct ExpansionCode, then Printing.ExpansionCode
string rawExpCode = null;
var expCode = GetModelPropertyValue(dataObj, objType, "ExpansionCode");
if (expCode is string expStr && !string.IsNullOrEmpty(expStr))
{
    rawExpCode = expStr;
    info.SetName = UITextExtractor.MapSetCodeToName(expStr);
}
else if (printing != null)
{
    var printExpProp = printing.GetType().GetProperty("ExpansionCode");
    if (printExpProp != null)
    {
        var printExpVal = printExpProp.GetValue(printing)?.ToString();
        if (!string.IsNullOrEmpty(printExpVal))
        {
            rawExpCode = printExpVal;
            info.SetName = UITextExtractor.MapSetCodeToName(printExpVal);
        }
    }
}
if (rawExpCode != null)
    MelonLogger.Msg($"[CardModelProvider] {info.Name}: ExpansionCode='{rawExpCode}' -> SetName='{info.SetName}'");
```

#### Raw ExpansionCode Output

With Avatar filter active and search cleared, scrolling through pages:

```
Brandende Welle:           ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Gran Gran:                 ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Tigerrobbe:                ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Koh der Gesichterdieb:     ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Suki der Kyoshi-Krieger:   ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Zuko der Prinz:            ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Aang der Avatar:           ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Katara:                    ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Sokka:                     ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Toph Beifong:              ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Iroh:                      ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Appa:                      ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Momo:                      ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Feuerlord Ozai:            ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Prinzessin Azula:          ExpansionCode='TLA' -> SetName='Magic: The Gathering | Avatar: The Last Airbender™'
Gedankenwirbel:            ExpansionCode='PZA' -> SetName='PZA' (no localization found)
```

**Set codes discovered:**
- `TLA` = "Magic: The Gathering | Avatar: The Last Airbender™" (main set)
- `TLE` = "Avatar: The Last Airbender - Extra" (extra/bonus set)
- `PZA` = Unknown set (no localization key `General/Sets/PZA` exists)

### Conclusion

**Brandende Welle has ExpansionCode='TLA'** — identical to all other Avatar cards (Gran Gran, Tigerrobbe, etc.) that DO appear when the Avatar filter is active without text search.

This **disproves** the hypothesis that the card has a different set code. The bug is in the game's internal filter combination logic: when both a set filter AND text search are active simultaneously, the card pool returns empty even though cards matching both criteria exist.

## Game Types Investigated

### SetMetadataProvider (Core.Shared.Code.CardFilters)

Manages set filter data. Key fields:
- `_setcodesByFilter` — maps `CardFilterType` to set code string
- `_bonusSheets` — HashSet of bonus sheet set codes
- `SetCodeAliasesReverseMap` — maps alias codes to primary codes
- `_flavorForCollation` — maps `CollationMapping` to flavor strings (empty at runtime for most sets)

### ClientSetMetadata (Core.Code.Collations)

Per-set metadata:
- `SetCode` (string), `Collations` (list), `Availability` (enum)
- `CardFilterType` (enum), `ReleaseDate`, `IsBonusSheet`, `IsPublished`, `IsMajorCardset`
- `SetCodeAliases` (list of strings) — for sets with multiple codes

## External Search Results

Searched for this bug in official Arena sources (March 2026):
- **MTG Arena Known Issues page** — 403/inaccessible
- **Patch notes 2026.57.0, 2026.57.20** — no mention of set filter + search bugs
- **Reddit/community reports** — no matching reports found
- **Conclusion:** Bug does not appear to be officially known or documented

## Impact on Mod

The mod correctly handles this situation:
- `Search rescan pool: 0 -> 0` accurately reports the empty card pool
- The mod reads whatever the game provides via `CardPoolHolder` pages
- No workaround possible from the mod side — the game returns an empty card pool before the mod sees it

## Workaround for Users

- Use set filters OR text search, but not both simultaneously
- To find a specific card in a set: apply set filter, then scroll through pages manually
- To search for a card by name: clear all set filters first, then search
