# Game Type Index

Maps commonly-referenced game types to their full namespace and DLL location.
Used by `tools/decompile.ps1` and `tools/decompile-all.ps1`.

**DLL abbreviations:**
- `Core` = `Core.dll`
- `Asm` = `Assembly-CSharp.dll`
- `Gre` = `Wizards.MDN.GreProtobuf.dll`
- `Shared` = `SharedClientCore.dll`

**DLL location:** `C:\Program Files\Wizards of the Coast\MTGA\MTGA_Data\Managed\`

## Store Types

| Short Name | Full Namespace | DLL |
|---|---|---|
| ContentController_StoreCarousel | ContentController_StoreCarousel | Core |
| StoreItemBase | StoreItemBase | Core |
| StoreConfirmationModal | StoreConfirmationModal | Core |
| StoreItemDisplay | StoreItemDisplay | Core |
| StoreDisplayPreconDeck | Core.Meta.MainNavigation.Store.StoreDisplayPreconDeck | Core |
| StoreDisplayCardViewBundle | StoreDisplayCardViewBundle | Core |
| StoreSetFilterToggles | Core.Meta.MainNavigation.Store.StoreSetFilterToggles | Core |
| StoreSetFilterModel | Core.Meta.MainNavigation.Store.Data.StoreSetFilterModel | Core |
| CardDataForTile | Wizards.MDN.Store.CardDataForTile | Gre |

## Card Data & Display Types

| Short Name | Full Namespace | DLL |
|---|---|---|
| CardData | GreClient.CardData.CardData | Gre |
| CardPrintingData | Wotc.Mtga.Cards.Database.CardPrintingData | Shared |
| CardDatabase | Wotc.Mtga.Cards.Database.CardDatabase | Shared |
| MtgCardInstance | (runtime only, not easily decompiled) | Core |
| DuelScene_CDC | DuelScene_CDC | Core |
| MetaCardView | MetaCardView | Core |
| PagesMetaCardView | PagesMetaCardView | Core |
| BoosterMetaCardView | BoosterMetaCardView | Core |
| DraftPackCardView | DraftPackCardView | Asm |
| CardView | CardView | Asm |
| DuelCardView | DuelCardView | Core |
| RewardDisplayCard | RewardDisplayCard | Core |
| CardRolloverZoomHandler | CardRolloverZoomHandler | Core |
| StaticColumnMetaCardHolder | StaticColumnMetaCardHolder | Core |
| StaticColumnMetaCardView | StaticColumnMetaCardView | Core |
| CDCViewMetadata | Wotc.Mtga.CardParts.CDCViewMetadata | Core |
| CardHolderType | Wotc.Mtga.CardParts.CardHolderType | Core |

## Card Holders & Browsers

| Short Name | Full Namespace | DLL |
|---|---|---|
| CardPoolHolder | CardPoolHolder | Core |
| ScrollCardPoolHolder | ScrollCardPoolHolder | Core |
| CardBrowserCardHolder | CardBrowserCardHolder | Core |
| ListMetaCardHolder | ListMetaCardHolder | Core |
| UniversalBattlefieldStack | UniversalBattlefieldStack | Core |

## UI Components

| Short Name | Full Namespace | DLL |
|---|---|---|
| CustomButton | CustomButton | Core |
| CustomButtonWithTooltip | CustomButtonWithTooltip | Core |
| SystemMessageButtonView | SystemMessageButtonView | Asm |
| cTMP_Dropdown | cTMP_Dropdown | Asm |
| TooltipTrigger | TooltipTrigger | Asm |
| MainButton | MainButton | Asm |
| Spinner_OptionSelector | Spinner_OptionSelector | Core |

## Navigation & Controllers

| Short Name | Full Namespace | DLL |
|---|---|---|
| NavContentController | NavContentController | Core |
| NavBarController | NavBarController | Core |
| HomePageContentController | HomePageContentController | Core |
| EventPageContentController | EventPageContentController | Core |
| PacketSelectContentController | PacketSelectContentController | Core |
| LearnToPlayControllerV2 | LearnToPlayControllerV2 | Core |
| DeckManagerController | DeckManagerController | Core |
| PlayBladeController | PlayBladeController | Core |
| DeckSelectBlade | DeckSelectBlade | Core |
| SettingsMenu | Wotc.Mtga.Wrapper.SettingsMenu | Core |
| GameManager | GameManager | Core |
| MatchTimer | MatchTimer | Core |
| WrapperController | WrapperController | Core |

## Play Blade & Events

| Short Name | Full Namespace | DLL |
|---|---|---|
| BladeContentView | Wizards.Mtga.PlayBlade.BladeContentView | Core |
| EventBladeContentView | Wizards.Mtga.PlayBlade.EventBladeContentView | Core |
| LastPlayedBladeContentView | LastPlayedBladeContentView | Core |

## Deck Builder

| Short Name | Full Namespace | DLL |
|---|---|---|
| DeckBuilderModelProvider | Core.Code.Decks.DeckBuilderModelProvider | Core |
| DeckBuilderActionsHandler | Core.Code.Decks.DeckBuilderActionsHandler | Core |
| DeckMainTitlePanel | DeckMainTitlePanel | Core |
| DeckCostsDetails | DeckCostsDetails | Core |
| DeckTypesDetails | DeckTypesDetails | Core |

## Social / Friends

| Short Name | Full Namespace | DLL |
|---|---|---|
| SocialUI | SocialUI | Core |
| FriendsWidget | FriendsWidget | Core |
| SocialEntityListHeader | SocialEntityListHeader | Core |
| FriendTile | FriendTile | Asm |
| InviteOutgoingTile | InviteOutgoingTile | Asm |
| InviteIncomingTile | InviteIncomingTile | Asm |
| BlockTile | BlockTile | Asm |

## Challenge / Private Game

| Short Name | Full Namespace | DLL |
|---|---|---|
| UnifiedChallengeDisplay | UnifiedChallengeDisplay | Core |
| ChallengePlayerDisplay | Wizards.Mtga.PrivateGame.ChallengePlayerDisplay | Core |
| UnifiedChallengeBladeWidget | UnifiedChallengeBladeWidget | Core |

## Duel Scene

| Short Name | Full Namespace | DLL |
|---|---|---|
| UXEventQueue | Wotc.Mtga.DuelScene.UXEvents.UXEventQueue | Core |
| UXEvent | Wotc.Mtga.DuelScene.UXEvents.UXEvent | Core |
| ButtonPhaseLadder | ButtonPhaseLadder | Core |
| ManaColorSelector | ManaColorSelector | Core |

## Mastery / Rewards

| Short Name | Full Namespace | DLL |
|---|---|---|
| ProgressionTrackLevel | Core.MainNavigation.RewardTrack.ProgressionTrackLevel | Core |
| ClientTrackLevelInfo | Core.MainNavigation.RewardTrack.ClientTrackLevelInfo | Core |
| RewardDisplayData | RewardDisplayData | Core |
| ProgressionTracksContentController | ProgressionTracksContentController | Core |
| ContentController_PrizeWall | ContentController_PrizeWall | Core |

## Codex / Learn to Play

| Short Name | Full Namespace | DLL |
|---|---|---|
| TableOfContentsSection | Core.MainNavigation.LearnToPlay.TableOfContentsSection | Core |
| LearnMoreSection | Core.MainNavigation.LearnToPlay.LearnMoreSection | Core |

## Mailbox

| Short Name | Full Namespace | DLL |
|---|---|---|
| ContentControllerPlayerInbox | Wotc.Mtga.Wrapper.Mailbox.ContentControllerPlayerInbox | Core |

## System / Popups

| Short Name | Full Namespace | DLL |
|---|---|---|
| PopupManager | Core.Meta.MainNavigation.PopUps.PopupManager | Core |
| SystemMessageManager | SystemMessageManager | Core |
| SystemMessageView | SystemMessageView | Core |

## Localization

| Short Name | Full Namespace | DLL |
|---|---|---|
| LocalizedString | Wotc.Mtga.Loc.LocalizedString | Core |
| Languages | Wotc.Mtga.Loc.Languages | Core |
| MTGALocalizedString | MTGALocalizedString | Core |

## GRE Protocol Enums

| Short Name | Full Namespace | DLL |
|---|---|---|
| EventContext | Wizards.MDN.EventContext | Gre |
| StopType | (check Gre) | Gre |
| SettingStatus | (check Gre) | Gre |

## Provider / Utility Types

| Short Name | Full Namespace | DLL |
|---|---|---|
| Pantry | Wizards.Mtga.Pantry | Core |
| ICardRolloverZoom | Wotc.Mtga.ICardRolloverZoom | Core |
| AbilityHangerBaseConfigProvider | Wotc.Mtga.Hangers.AbilityHangers.AbilityHangerBaseConfigProvider | Core |
| AbilityHangerBase | AbilityHangerBase | Core |

## Critical Field/Property Notes

Some types have members that are fields (not properties) - reflection with `GetProperty()` will fail:

- **MtgCardInstance**: `AttachedToId` (uint field), `IsTapped` (bool field), `HasSummoningSickness` (bool field)
- **EventContext**: `PlayerEvent` (IPlayerEvent field)
- **TooltipTrigger**: `TooltipData` (public field), but `TooltipData.Text` IS a property
- **StoreSetFilterModel**: `SetSymbol` (string field), `Availability` (field)
- **cTMP_Dropdown**: extends `Selectable`, NOT `TMP_Dropdown` - use type name reflection
