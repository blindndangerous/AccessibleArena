using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AccessibleArena.Core.Models;
using static AccessibleArena.Core.Utils.ReflectionUtils;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides extended card information: keyword descriptions and linked face data.
    /// Extracted from CardModelProvider to keep that class focused on core model access.
    /// </summary>
    public static class ExtendedCardInfoProvider
    {
        // Cache for linked face reflection
        private static PropertyInfo _linkedFaceTypeProp;
        private static bool _linkedFaceTypePropSearched;
        private static PropertyInfo _linkedFaceGrpIdsProp;
        private static bool _linkedFaceGrpIdsPropSearched;

        // Cache for ability hanger provider (keyword descriptions)
        private static object _abilityHangerProvider;
        private static MethodInfo _getHangerConfigsForCardMethod;
        private static MethodInfo _hangerProviderCleanupMethod;

        // Cache for duel-scene CardDataProvider
        private static object _duelCardDataProvider;
        private static MethodInfo _duelGetCardPrintingMethod;
        private static bool _duelCardDataProviderSearched;

        /// <summary>
        /// Clears extended info caches.
        /// </summary>
        public static void ClearCache()
        {
            _linkedFaceTypeProp = null;
            _linkedFaceTypePropSearched = false;
            _linkedFaceGrpIdsProp = null;
            _linkedFaceGrpIdsPropSearched = false;
            _abilityHangerProvider = null;
            _getHangerConfigsForCardMethod = null;
            _hangerProviderCleanupMethod = null;
            _duelCardDataProvider = null;
            _duelGetCardPrintingMethod = null;
            _duelCardDataProviderSearched = false;
        }

        /// <summary>
        /// Gets keyword ability descriptions for the focused card.
        /// Uses the game's AbilityHangerBaseConfigProvider (from AbilityHangerBase MonoBehaviour)
        /// to get the same keyword tooltips shown when hovering cards in-game.
        /// Returns Header + Details pairs like "Fliegend: Diese Kreatur kann nur..."
        /// </summary>
        public static List<string> GetKeywordDescriptions(GameObject card)
        {
            var result = new List<string>();
            if (card == null) return result;

            try
            {
                // We need a CDC to call the hanger provider
                var cdc = CardModelProvider.GetDuelSceneCDC(card);
                if (cdc == null)
                {
                    // Non-duel context: extract individual ability texts from card model
                    return GetAbilityTextsFromCardModel(card);
                }

                // Ensure hanger provider is cached (retry if not found previously)
                if (_abilityHangerProvider == null)
                {
                    FindAbilityHangerProvider();
                }

                if (_abilityHangerProvider == null || _getHangerConfigsForCardMethod == null)
                    return result;

                var cdcType = cdc.GetType();

                // Get Model (ICardDataAdapter) from CDC
                var modelProp = cdcType.GetProperty("Model", PublicInstance);
                if (modelProp == null)
                {
                    // Try base type
                    modelProp = cdcType.BaseType?.GetProperty("Model", PublicInstance);
                }
                if (modelProp == null) return result;
                var model = modelProp.GetValue(cdc);
                if (model == null) return result;

                // Get HolderType (CardHolderType enum) from CDC
                var holderTypeProp = cdcType.GetProperty("HolderType", PublicInstance)
                    ?? cdcType.BaseType?.GetProperty("HolderType", PublicInstance);
                object holderType = null;
                if (holderTypeProp != null)
                    holderType = holderTypeProp.GetValue(cdc);

                // Create CDCViewMetadata struct
                object metadata = CreateCDCViewMetadata(cdc);

                if (holderType == null || metadata == null)
                {
                    MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Missing holderType={holderType != null} metadata={metadata != null}");
                    return result;
                }

                // Call GetHangerConfigsForCard(model, holderType, metadata)
                var configs = _getHangerConfigsForCardMethod.Invoke(
                    _abilityHangerProvider, new object[] { model, holderType, metadata });

                if (configs is IEnumerable configEnum)
                {
                    var seen = new HashSet<string>();
                    foreach (var config in configEnum)
                    {
                        if (config == null) continue;
                        var configType = config.GetType();

                        // HangerConfig is a struct with public readonly fields
                        var headerField = configType.GetField("Header");
                        var detailsField = configType.GetField("Details");

                        string header = headerField?.GetValue(config)?.ToString() ?? "";
                        string details = detailsField?.GetValue(config)?.ToString() ?? "";

                        if (string.IsNullOrEmpty(header) && string.IsNullOrEmpty(details)) continue;

                        // Format: "Header: Details"
                        string text;
                        if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(details))
                            text = $"{header}: {details}";
                        else if (!string.IsNullOrEmpty(header))
                            text = header;
                        else
                            text = details;

                        // Parse mana symbols
                        text = CardModelProvider.ParseManaSymbolsInText(text);

                        MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Hanger: '{text}'");

                        if (seen.Add(text))
                            result.Add(text);
                    }
                }

                // Cleanup provider internal state
                _hangerProviderCleanupMethod?.Invoke(_abilityHangerProvider, null);

                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] GetKeywordDescriptions: {result.Count} entries");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Error getting keyword descriptions: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Fallback for non-duel contexts: extracts individual ability texts from the card model.
        /// Returns each ability as a separate navigable item for the extended info menu.
        /// </summary>
        private static List<string> GetAbilityTextsFromCardModel(GameObject card)
        {
            var result = new List<string>();
            if (card == null) return result;

            try
            {
                // Get the card model (same path as ExtractCardInfoFromModel)
                object model = null;

                var metaCardView = CardModelProvider.GetMetaCardView(card);
                if (metaCardView == null)
                {
                    var parent = card.transform.parent;
                    int maxLevels = 5;
                    while (metaCardView == null && parent != null && maxLevels-- > 0)
                    {
                        metaCardView = CardModelProvider.GetMetaCardView(parent.gameObject);
                        parent = parent.parent;
                    }
                }

                if (metaCardView != null)
                    model = CardModelProvider.GetMetaCardModel(metaCardView);

                if (model == null)
                {
                    MelonLogger.Msg("[ExtendedCardInfoProvider] [ExtInfo] No model found for non-duel card");
                    return result;
                }

                var objType = model.GetType();

                // Get GrpId and TitleId for ability text lookup
                uint cardGrpId = 0;
                var grpIdObj = CardModelProvider.GetModelPropertyValue(model, objType, "GrpId");
                if (grpIdObj is uint gid) cardGrpId = gid;

                uint cardTitleId = 0;
                var titleIdObj = CardModelProvider.GetModelPropertyValue(model, objType, "TitleId");
                if (titleIdObj is uint tid) cardTitleId = tid;

                // Get all ability IDs for context
                var abilityIdsVal = CardModelProvider.GetModelPropertyValue(model, objType, "AbilityIds");
                uint[] abilityIds = null;
                if (abilityIdsVal is IEnumerable<uint> aidEnum)
                    abilityIds = aidEnum.ToArray();
                else if (abilityIdsVal is uint[] aidArray)
                    abilityIds = aidArray;

                // Extract abilities
                var abilities = CardModelProvider.GetModelPropertyValue(model, objType, "Abilities");
                if (abilities is IEnumerable abilityEnum)
                {
                    var seen = new HashSet<string>();
                    foreach (var ability in abilityEnum)
                    {
                        if (ability == null) continue;
                        var abilityType = ability.GetType();

                        uint abilityId = 0;
                        var idProp = abilityType.GetProperty("Id", PublicInstance);
                        if (idProp != null)
                        {
                            var idVal = idProp.GetValue(ability);
                            if (idVal is uint aid) abilityId = aid;
                        }

                        var textValue = CardTextProvider.GetAbilityText(ability, abilityType, cardGrpId, abilityId, abilityIds, cardTitleId);
                        if (!string.IsNullOrEmpty(textValue))
                        {
                            textValue = CardModelProvider.ParseManaSymbolsInText(textValue);
                            if (seen.Add(textValue))
                            {
                                result.Add(textValue);
                                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Ability: '{textValue}'");
                            }
                        }
                    }
                }

                // Also try CardPrintingData path if model didn't have abilities
                if (result.Count == 0 && cardGrpId > 0)
                {
                    var cardData = CardModelProvider.GetCardDataFromGrpId(cardGrpId) ?? GetCardDataFromGrpIdDuelScene(cardGrpId);
                    if (cardData != null)
                    {
                        var cardType = cardData.GetType();
                        var abilitiesProp = cardType.GetProperty("Abilities") ?? cardType.GetProperty("IntrinsicAbilities");
                        if (abilitiesProp != null)
                        {
                            var abilitiesFromData = abilitiesProp.GetValue(cardData) as IEnumerable;
                            if (abilitiesFromData != null)
                            {
                                var seen = new HashSet<string>();
                                foreach (var ability in abilitiesFromData)
                                {
                                    if (ability == null) continue;
                                    var aType = ability.GetType();
                                    var idProp = aType.GetProperty("Id");
                                    if (idProp != null)
                                    {
                                        var abilityId = (uint)idProp.GetValue(ability);
                                        var abilityText = CardTextProvider.GetAbilityTextFromProvider(cardGrpId, abilityId, null, 0);
                                        if (!string.IsNullOrEmpty(abilityText))
                                        {
                                            abilityText = CardModelProvider.ParseManaSymbolsInText(abilityText);
                                            if (seen.Add(abilityText))
                                            {
                                                result.Add(abilityText);
                                                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Ability (data): '{abilityText}'");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] GetAbilityTextsFromCardModel: {result.Count} entries");
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] [ExtInfo] Error extracting ability texts: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Finds and caches the AbilityHangerBaseConfigProvider from an AbilityHangerBase MonoBehaviour.
        /// Uses Resources.FindObjectsOfTypeAll to include inactive GameObjects.
        /// This provider generates the keyword tooltip data (Header + Details) shown when hovering cards.
        /// </summary>
        private static void FindAbilityHangerProvider()
        {
            // Find the AbilityHangerBase type in loaded assemblies
            Type ahbType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    ahbType = asm.GetType("AbilityHangerBase");
                    if (ahbType != null) break;
                }
                catch { }
            }

            UnityEngine.Object[] instances = null;

            if (ahbType != null)
            {
                // FindObjectsOfTypeAll includes inactive GameObjects
                instances = Resources.FindObjectsOfTypeAll(ahbType);
                MelonLogger.Msg($"[ExtendedCardInfoProvider] Found {instances.Length} AbilityHangerBase instances (including inactive)");
            }
            else
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] AbilityHangerBase type not found in assemblies, searching all MonoBehaviours...");
                instances = Resources.FindObjectsOfTypeAll(typeof(MonoBehaviour));
            }

            foreach (var obj in instances)
            {
                if (obj == null) continue;
                var type = obj.GetType();

                // If we searched all MonoBehaviours, filter to hanger types
                if (ahbType == null && !type.Name.Contains("AbilityHanger"))
                    continue;

                // Look for _abilityHangerProvider field (protected in AbilityHangerBase)
                var field = type.GetField("_abilityHangerProvider",
                    PrivateInstance | BindingFlags.FlattenHierarchy);
                if (field == null) continue;

                var provider = field.GetValue(obj);
                if (provider == null)
                {
                    MelonLogger.Msg($"[ExtendedCardInfoProvider] Found {type.Name} but _abilityHangerProvider is null (not yet initialized)");
                    continue;
                }

                var providerType = provider.GetType();

                // Find GetHangerConfigsForCard method
                var getConfigsMethod = providerType.GetMethod("GetHangerConfigsForCard");
                if (getConfigsMethod == null) continue;

                // Find Cleanup method
                var cleanupMethod = providerType.GetMethod("Cleanup");

                _abilityHangerProvider = provider;
                _getHangerConfigsForCardMethod = getConfigsMethod;
                _hangerProviderCleanupMethod = cleanupMethod;

                MelonLogger.Msg($"[ExtendedCardInfoProvider] Found AbilityHangerProvider: {providerType.Name} from {type.Name}");

                // Log method signature for debugging
                var ps = getConfigsMethod.GetParameters();
                MelonLogger.Msg($"[ExtendedCardInfoProvider] GetHangerConfigsForCard({string.Join(", ", ps.Select(p => $"{p.ParameterType.Name} {p.Name}"))})");
                return;
            }

            MelonLogger.Msg($"[ExtendedCardInfoProvider] AbilityHangerProvider not found (will retry on next I key press)");
        }

        /// <summary>
        /// Creates a CDCViewMetadata struct via reflection.
        /// Tries the BASE_CDC constructor first, falls back to the bool constructor.
        /// </summary>
        private static object CreateCDCViewMetadata(Component cdc)
        {
            try
            {
                // Find CDCViewMetadata type
                Type metadataType = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        metadataType = asm.GetType("Wotc.Mtga.CardParts.CDCViewMetadata");
                        if (metadataType != null) break;
                    }
                    catch { }
                }
                if (metadataType == null) return null;

                // Try constructor that takes BASE_CDC (or any base type of cdc)
                var cdcType = cdc.GetType();
                while (cdcType != null && cdcType != typeof(object))
                {
                    var ctor = metadataType.GetConstructor(new[] { cdcType });
                    if (ctor != null)
                        return ctor.Invoke(new object[] { cdc });
                    cdcType = cdcType.BaseType;
                }

                // Fallback: use bool constructor with safe defaults
                var boolCtor = metadataType.GetConstructor(new[] {
                    typeof(bool), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
                if (boolCtor != null)
                    return boolCtor.Invoke(new object[] { false, false, false, false, false });
            }
            catch (Exception ex)
            {
                MelonLogger.Msg($"[ExtendedCardInfoProvider] Error creating CDCViewMetadata: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Gets linked face information for double-faced, split, adventure, and room cards.
        /// Returns a label (e.g. "Other face") and full CardInfo for the linked face, or null if none.
        /// </summary>
        public static (string label, CardInfo faceInfo)? GetLinkedFaceInfo(GameObject card)
        {
            if (card == null) return null;

            try
            {
                // Get model
                object model = null;
                var cdc = CardModelProvider.GetDuelSceneCDC(card);
                if (cdc != null)
                    model = CardModelProvider.GetCardModel(cdc);
                if (model == null)
                {
                    var metaView = CardModelProvider.GetMetaCardView(card);
                    if (metaView != null)
                        model = CardModelProvider.GetMetaCardModel(metaView);
                }
                if (model == null) return null;

                var objType = model.GetType();

                // Get LinkedFaceType (enum stored as int)
                if (!_linkedFaceTypePropSearched)
                {
                    _linkedFaceTypePropSearched = true;
                    _linkedFaceTypeProp = objType.GetProperty("LinkedFaceType", PublicInstance);
                }
                if (_linkedFaceTypeProp == null) return null;

                var linkedFaceVal = _linkedFaceTypeProp.GetValue(model);
                if (linkedFaceVal == null) return null;

                int linkedFaceInt = (int)Convert.ChangeType(linkedFaceVal, typeof(int));
                if (linkedFaceInt == 0) return null; // None

                // Get LinkedFaceGrpIds
                if (!_linkedFaceGrpIdsPropSearched)
                {
                    _linkedFaceGrpIdsPropSearched = true;
                    _linkedFaceGrpIdsProp = objType.GetProperty("LinkedFaceGrpIds", PublicInstance);
                }
                if (_linkedFaceGrpIdsProp == null) return null;

                var grpIdsVal = _linkedFaceGrpIdsProp.GetValue(model);
                if (grpIdsVal == null) return null;

                // Extract first GrpId from the list
                uint faceGrpId = 0;
                if (grpIdsVal is IEnumerable grpIdsEnum)
                {
                    foreach (var item in grpIdsEnum)
                    {
                        if (item is uint uid && uid > 0) { faceGrpId = uid; break; }
                        if (item is int iid && iid > 0) { faceGrpId = (uint)iid; break; }
                    }
                }

                if (faceGrpId == 0) return null;

                // Look up card data for the linked face GrpId
                var faceData = GetCardDataFromGrpIdDuelScene(faceGrpId);
                if (faceData == null) return null;

                var faceInfo = CardModelProvider.ExtractCardInfoFromObject(faceData);
                if (!faceInfo.IsValid) return null;

                // Map linked face type to label
                string label = GetLinkedFaceLabel(linkedFaceInt);

                return (label, faceInfo);
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "ExtendedCardInfoProvider",
                    $"Error getting linked face info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Maps LinkedFace enum int to a user-facing label.
        /// Values based on decompiled LinkedFace enum.
        /// </summary>
        private static string GetLinkedFaceLabel(int linkedFaceType)
        {
            switch (linkedFaceType)
            {
                case 1:  // DfcFront
                case 2:  // DfcBack
                    return Strings.LinkedFaceOtherFace;
                case 5:  // SplitCard
                case 6:  // SplitHalf
                    return Strings.LinkedFaceOtherHalf;
                case 7:  // AdventureParent
                case 8:  // AdventureChild
                    return Strings.LinkedFaceAdventure;
                case 9:  // MdfcFront
                case 10: // MdfcBack
                    return Strings.LinkedFaceOtherFace;
                case 15: // RoomCard
                case 16: // RoomHalf
                    return Strings.LinkedFaceOtherRoom;
                default:
                    return Strings.LinkedFaceOtherFace;
            }
        }

        /// <summary>
        /// Gets CardData from a GrpId using the duel-scene CardDatabase.
        /// Separate from CardModelProvider.GetCardDataFromGrpId() which requires _cachedDeckHolder (menu-only).
        /// </summary>
        internal static object GetCardDataFromGrpIdDuelScene(uint grpId)
        {
            if (grpId == 0) return null;

            // First try the menu-scene path (works if _cachedDeckHolder is available)
            var menuResult = CardModelProvider.GetCardDataFromGrpId(grpId);
            if (menuResult != null) return menuResult;

            // Try duel-scene path via GameManager.CardDatabase.CardDataProvider
            if (!_duelCardDataProviderSearched)
            {
                _duelCardDataProviderSearched = true;
                FindDuelCardDataProvider();
            }

            if (_duelCardDataProvider == null || _duelGetCardPrintingMethod == null)
                return null;

            try
            {
                var result = _duelGetCardPrintingMethod.Invoke(_duelCardDataProvider, new object[] { grpId, null });
                return result;
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "ExtendedCardInfoProvider",
                    $"Error getting card data for GrpId {grpId} in duel scene: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Finds and caches the CardDataProvider from GameManager.CardDatabase for duel scene lookups.
        /// </summary>
        private static void FindDuelCardDataProvider()
        {
            foreach (var mb in GameObject.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var type = mb.GetType();
                if (type.Name != "GameManager") continue;

                var cardDbProp = type.GetProperty("CardDatabase");
                if (cardDbProp == null) break;

                var cardDb = cardDbProp.GetValue(mb);
                if (cardDb == null) break;

                var cardDbType = cardDb.GetType();
                var cdpProp = cardDbType.GetProperty("CardDataProvider");
                if (cdpProp == null) break;

                var cdp = cdpProp.GetValue(cardDb);
                if (cdp == null) break;

                var cdpType = cdp.GetType();

                // Try GetCardPrintingById(uint id, string skinCode)
                var method = cdpType.GetMethod("GetCardPrintingById", new[] { typeof(uint), typeof(string) });
                if (method == null)
                    method = cdpType.GetMethod("GetCardRecordById", new[] { typeof(uint), typeof(string) });

                if (method != null)
                {
                    _duelCardDataProvider = cdp;
                    _duelGetCardPrintingMethod = method;
                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "ExtendedCardInfoProvider",
                        $"Found duel CardDataProvider: {cdpType.Name}.{method.Name}");
                }
                break;
            }
        }
    }
}
