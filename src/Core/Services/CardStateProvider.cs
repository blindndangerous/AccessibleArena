using UnityEngine;
using MelonLoader;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AccessibleArena.Core.Models;

namespace AccessibleArena.Core.Services
{
    /// <summary>
    /// Provides card state information: attachments, combat state, targeting, and categorization.
    /// Extracted from CardModelProvider for separation of concerns.
    /// Uses CardModelProvider for low-level model/CDC access.
    /// </summary>
    public static class CardStateProvider
    {
        #region Cache Fields

        // Attachment cache
        private static FieldInfo _attachedToIdField;
        private static bool _attachedToIdFieldSearched;

        // Combat state cache
        private static PropertyInfo _instancePropCached;
        private static bool _instancePropSearched;
        private static PropertyInfo _isAttackingProp;
        private static bool _isAttackingPropSearched;
        private static PropertyInfo _isBlockingProp;
        private static bool _isBlockingPropSearched;
        private static FieldInfo _isTappedField;
        private static bool _isTappedFieldSearched;
        private static FieldInfo _hasSummoningSicknessField;
        private static bool _hasSummoningSicknessFieldSearched;
        private static FieldInfo _blockingIdsField;
        private static bool _blockingIdsFieldSearched;
        private static FieldInfo _blockedByIdsField;
        private static bool _blockedByIdsFieldSearched;

        // Zone type cache
        private static PropertyInfo _zoneTypePropCached;
        private static bool _zoneTypePropSearched;

        // Targeting cache
        private static FieldInfo _targetIdsField;
        private static bool _targetIdsFieldSearched;
        private static FieldInfo _targetedByIdsField;
        private static bool _targetedByIdsFieldSearched;

        #endregion

        /// <summary>
        /// Clears all cached reflection data. Call on scene change.
        /// </summary>
        public static void ClearCache()
        {
            // Attachment cache
            _attachedToIdField = null;
            _attachedToIdFieldSearched = false;
            // Combat state cache
            _instancePropCached = null;
            _instancePropSearched = false;
            _isAttackingProp = null;
            _isAttackingPropSearched = false;
            _isBlockingProp = null;
            _isBlockingPropSearched = false;
            _isTappedField = null;
            _isTappedFieldSearched = false;
            _hasSummoningSicknessField = null;
            _hasSummoningSicknessFieldSearched = false;
            _blockingIdsField = null;
            _blockingIdsFieldSearched = false;
            _blockedByIdsField = null;
            _blockedByIdsFieldSearched = false;
            // Zone type cache
            _zoneTypePropCached = null;
            _zoneTypePropSearched = false;
            // Targeting cache
            _targetIdsField = null;
            _targetIdsFieldSearched = false;
            _targetedByIdsField = null;
            _targetedByIdsFieldSearched = false;
        }

        #region Attachments

        /// <summary>
        /// Gets the AttachedToId from a card's Model.Instance via reflection.
        /// This is the InstanceId of the card this card is attached to (0 if not attached).
        /// Used by the game's UniversalBattlefieldStack to track attachment relationships.
        /// </summary>
        public static uint GetAttachedToId(object model)
        {
            var instance = GetModelInstance(model);
            if (instance == null) return 0;
            try
            {
                // Cache the FieldInfo - AttachedToId is a field on MtgCardInstance, not a property
                if (!_attachedToIdFieldSearched)
                {
                    _attachedToIdFieldSearched = true;
                    _attachedToIdField = instance.GetType().GetField("AttachedToId",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_attachedToIdField != null)
                {
                    var val = _attachedToIdField.GetValue(instance);
                    if (val is uint id) return id;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Gets the InstanceId from a card's model.
        /// </summary>
        private static uint GetModelInstanceId(object model)
        {
            if (model == null) return 0;
            try
            {
                var prop = model.GetType().GetProperty("InstanceId");
                if (prop != null)
                {
                    var val = prop.GetValue(model);
                    if (val is uint id) return id;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Gets the GrpId from a card's model.
        /// </summary>
        private static uint GetModelGrpId(object model)
        {
            if (model == null) return 0;
            try
            {
                var prop = model.GetType().GetProperty("GrpId");
                if (prop != null)
                {
                    var val = prop.GetValue(model);
                    if (val is uint id) return id;
                }
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// Collects all DuelScene_CDC card models from the battlefield.
        /// Returns list of (model, instanceId, grpId) for scanning attachment relationships.
        /// </summary>
        private static List<(object model, uint instanceId, uint grpId)> GetAllBattlefieldCardModels()
        {
            var results = new List<(object model, uint instanceId, uint grpId)>();

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!go.name.Contains("BattlefieldCardHolder")) continue;

                foreach (var child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    var cdc = CardModelProvider.GetDuelSceneCDC(child.gameObject);
                    if (cdc == null) continue;

                    var model = CardModelProvider.GetCardModel(cdc);
                    if (model == null) continue;

                    uint instanceId = GetModelInstanceId(model);
                    if (instanceId == 0) continue;

                    uint grpId = GetModelGrpId(model);
                    results.Add((model, instanceId, grpId));
                }
                break; // Only one battlefield holder needed
            }

            return results;
        }

        /// <summary>
        /// Gets the list of cards attached to this card (enchantments, equipment, etc.).
        /// Scans all battlefield cards and checks whose Instance.AttachedToId matches this card's InstanceId.
        /// </summary>
        public static List<(uint instanceId, uint grpId, string name)> GetAttachments(GameObject card)
        {
            var attachments = new List<(uint instanceId, uint grpId, string name)>();
            if (card == null) return attachments;

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(card);
            if (cdcComponent == null) return attachments;

            var model = CardModelProvider.GetCardModel(cdcComponent);
            if (model == null) return attachments;

            uint myInstanceId = GetModelInstanceId(model);
            if (myInstanceId == 0) return attachments;

            try
            {
                var allCards = GetAllBattlefieldCardModels();
                foreach (var (cardModel, instanceId, grpId) in allCards)
                {
                    if (instanceId == myInstanceId) continue; // Skip self

                    uint attachedToId = GetAttachedToId(cardModel);
                    if (attachedToId == myInstanceId)
                    {
                        string name = grpId > 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                        attachments.Add((instanceId, grpId, name));
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider",
                            $"Found attachment: {name ?? "unknown"} (InstanceId={instanceId}) attached to InstanceId={myInstanceId}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider", $"Error getting attachments: {ex.Message}");
            }

            return attachments;
        }

        /// <summary>
        /// Gets the card that this card is attached to (for auras/equipment).
        /// Uses Model.Instance.AttachedToId to find the parent card on the battlefield.
        /// </summary>
        public static (uint instanceId, uint grpId, string name)? GetAttachedTo(GameObject card)
        {
            if (card == null) return null;

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(card);
            if (cdcComponent == null) return null;

            var model = CardModelProvider.GetCardModel(cdcComponent);
            if (model == null) return null;

            try
            {
                uint attachedToId = GetAttachedToId(model);
                if (attachedToId == 0) return null;

                // Find the parent card on the battlefield by its InstanceId
                var allCards = GetAllBattlefieldCardModels();
                foreach (var (cardModel, instanceId, grpId) in allCards)
                {
                    if (instanceId == attachedToId)
                    {
                        string name = grpId > 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider",
                            $"Card is attached to: {name ?? "unknown"} (InstanceId={instanceId}, GrpId={grpId})");
                        return (instanceId, grpId, name);
                    }
                }

                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider",
                    $"AttachedToId={attachedToId} but parent card not found on battlefield");
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider", $"Error getting attached-to info: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets formatted attachment text for announcing a card.
        /// Returns text describing what's attached to this card AND what this card is attached to.
        /// </summary>
        public static string GetAttachmentText(GameObject card)
        {
            var result = new List<string>();

            // Check what's attached TO this card (enchantments, equipment on a creature)
            var attachments = GetAttachments(card);
            if (attachments.Count > 0)
            {
                var names = attachments
                    .Select(a => a.name ?? "unknown card")
                    .ToList();

                if (names.Count == 1)
                {
                    result.Add(Models.Strings.Card_EnchantedBy(names[0]));
                }
                else
                {
                    result.Add(Models.Strings.Card_EnchantedBy(string.Join(", ", names)));
                }
            }

            // Check if this card IS attached to something (aura/equipment itself)
            var attachedTo = GetAttachedTo(card);
            if (attachedTo.HasValue && !string.IsNullOrEmpty(attachedTo.Value.name))
            {
                result.Add(Models.Strings.Card_AttachedTo(attachedTo.Value.name));
            }

            if (result.Count == 0) return "";
            return ", " + string.Join(", ", result);
        }

        #endregion

        #region Combat State

        /// <summary>
        /// Gets the Instance object from a card model via cached reflection.
        /// This is the MtgCardInstance that holds combat state fields.
        /// </summary>
        private static object GetModelInstance(object model)
        {
            if (model == null) return null;
            try
            {
                if (!_instancePropSearched)
                {
                    _instancePropSearched = true;
                    _instancePropCached = model.GetType().GetProperty("Instance");
                }
                // If cached type doesn't match (different model type), look up directly
                if (_instancePropCached != null && _instancePropCached.DeclaringType.IsAssignableFrom(model.GetType()))
                {
                    return _instancePropCached.GetValue(model);
                }
                // Fallback: direct lookup
                var prop = model.GetType().GetProperty("Instance");
                return prop?.GetValue(model);
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Returns true if the card model's Instance.IsAttacking property is true.
        /// </summary>
        public static bool GetIsAttacking(object model)
        {
            var instance = GetModelInstance(model);
            if (instance == null) return false;
            try
            {
                if (!_isAttackingPropSearched)
                {
                    _isAttackingPropSearched = true;
                    _isAttackingProp = instance.GetType().GetProperty("IsAttacking",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_isAttackingProp != null)
                {
                    var val = _isAttackingProp.GetValue(instance);
                    if (val is bool b) return b;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Returns true if the card model's Instance.IsBlocking property is true.
        /// </summary>
        public static bool GetIsBlocking(object model)
        {
            var instance = GetModelInstance(model);
            if (instance == null) return false;
            try
            {
                if (!_isBlockingPropSearched)
                {
                    _isBlockingPropSearched = true;
                    _isBlockingProp = instance.GetType().GetProperty("IsBlocking",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_isBlockingProp != null)
                {
                    var val = _isBlockingProp.GetValue(instance);
                    if (val is bool b) return b;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Gets the list of attacker InstanceIds that this blocker is blocking.
        /// Reads Instance.BlockingIds field (List of uint).
        /// </summary>
        public static List<uint> GetBlockingIds(object model)
        {
            var result = new List<uint>();
            var instance = GetModelInstance(model);
            if (instance == null) return result;
            try
            {
                if (!_blockingIdsFieldSearched)
                {
                    _blockingIdsFieldSearched = true;
                    _blockingIdsField = instance.GetType().GetField("BlockingIds",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_blockingIdsField != null)
                {
                    var val = _blockingIdsField.GetValue(instance);
                    if (val is IList list)
                    {
                        foreach (var item in list)
                        {
                            if (item is uint id) result.Add(id);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Gets the list of blocker InstanceIds blocking this attacker.
        /// Reads Instance.BlockedByIds field (List of uint).
        /// </summary>
        public static List<uint> GetBlockedByIds(object model)
        {
            var result = new List<uint>();
            var instance = GetModelInstance(model);
            if (instance == null) return result;
            try
            {
                if (!_blockedByIdsFieldSearched)
                {
                    _blockedByIdsFieldSearched = true;
                    _blockedByIdsField = instance.GetType().GetField("BlockedByIds",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_blockedByIdsField != null)
                {
                    var val = _blockedByIdsField.GetValue(instance);
                    if (val is IList list)
                    {
                        foreach (var item in list)
                        {
                            if (item is uint id) result.Add(id);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Resolves an InstanceId to a card name by scanning all battlefield cards.
        /// Returns null if not found.
        /// </summary>
        public static string ResolveInstanceIdToName(uint instanceId)
        {
            if (instanceId == 0) return null;
            try
            {
                var allCards = GetAllBattlefieldCardModels();
                foreach (var (cardModel, id, grpId) in allCards)
                {
                    if (id == instanceId)
                    {
                        return grpId > 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Returns true if the card model's Instance.IsTapped field is true.
        /// Note: IsTapped is a public FIELD on MtgCardInstance, not a property.
        /// </summary>
        public static bool GetIsTapped(object model)
        {
            var instance = GetModelInstance(model);
            if (instance == null) return false;
            try
            {
                if (!_isTappedFieldSearched)
                {
                    _isTappedFieldSearched = true;
                    _isTappedField = instance.GetType().GetField("IsTapped",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_isTappedField != null)
                {
                    var val = _isTappedField.GetValue(instance);
                    if (val is bool b) return b;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Checks if a card GameObject is tapped, using model data.
        /// Chains: GetDuelSceneCDC -> GetCardModel -> GetIsTapped.
        /// </summary>
        public static bool GetIsTappedFromCard(GameObject card)
        {
            if (card == null) return false;
            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return false;
            var model = CardModelProvider.GetCardModel(cdc);
            return GetIsTapped(model);
        }

        /// <summary>
        /// Returns true if the card model's Instance.HasSummoningSickness field is true.
        /// Note: HasSummoningSickness is a public FIELD on MtgCardInstance, not a property.
        /// </summary>
        public static bool GetHasSummoningSickness(object model)
        {
            var instance = GetModelInstance(model);
            if (instance == null) return false;
            try
            {
                if (!_hasSummoningSicknessFieldSearched)
                {
                    _hasSummoningSicknessFieldSearched = true;
                    _hasSummoningSicknessField = instance.GetType().GetField("HasSummoningSickness",
                        BindingFlags.Public | BindingFlags.Instance);
                }
                if (_hasSummoningSicknessField != null)
                {
                    var val = _hasSummoningSicknessField.GetValue(instance);
                    if (val is bool b) return b;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Checks if a card GameObject has summoning sickness, using model data.
        /// Chains: GetDuelSceneCDC -> GetCardModel -> GetHasSummoningSickness.
        /// </summary>
        public static bool GetHasSummoningSicknessFromCard(GameObject card)
        {
            if (card == null) return false;
            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return false;
            var model = CardModelProvider.GetCardModel(cdc);
            return GetHasSummoningSickness(model);
        }

        /// <summary>
        /// Checks if a card GameObject is attacking, using model data.
        /// Chains: GetDuelSceneCDC -> GetCardModel -> GetIsAttacking.
        /// </summary>
        public static bool GetIsAttackingFromCard(GameObject card)
        {
            if (card == null) return false;
            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return false;
            var model = CardModelProvider.GetCardModel(cdc);
            return GetIsAttacking(model);
        }

        /// <summary>
        /// Checks if a card GameObject is blocking, using model data.
        /// Chains: GetDuelSceneCDC -> GetCardModel -> GetIsBlocking.
        /// </summary>
        public static bool GetIsBlockingFromCard(GameObject card)
        {
            if (card == null) return false;
            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return false;
            var model = CardModelProvider.GetCardModel(cdc);
            return GetIsBlocking(model);
        }

        /// <summary>
        /// Formats a CounterType enum name into a human-readable string.
        /// E.g., "P1P1" -> "+1/+1", "M1M1" -> "-1/-1", "Loyalty" -> "Loyalty".
        /// </summary>
        public static string FormatCounterTypeName(string enumName)
        {
            if (string.IsNullOrEmpty(enumName)) return enumName;

            // Match patterns like P1P1, M1M0, P0P1, etc.
            if (enumName.Length == 4 &&
                (enumName[0] == 'P' || enumName[0] == 'M') &&
                char.IsDigit(enumName[1]) &&
                (enumName[2] == 'P' || enumName[2] == 'M') &&
                char.IsDigit(enumName[3]))
            {
                char sign1 = enumName[0] == 'P' ? '+' : '-';
                char sign2 = enumName[2] == 'P' ? '+' : '-';
                return $"{sign1}{enumName[1]}/{sign2}{enumName[3]}";
            }

            return enumName;
        }

        /// <summary>
        /// Gets all counters on a card from its model's Instance.Counters dictionary.
        /// Returns a list of (typeName, count) tuples for counters with count > 0.
        /// Chains: GetDuelSceneCDC -> GetCardModel -> Instance -> Counters.
        /// </summary>
        public static List<(string typeName, int count)> GetCountersFromCard(GameObject card)
        {
            var result = new List<(string, int)>();
            if (card == null) return result;

            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return result;
            var model = CardModelProvider.GetCardModel(cdc);
            if (model == null) return result;

            var instance = GetModelInstance(model);
            if (instance == null) return result;

            try
            {
                var instanceType = instance.GetType();
                // Counters is IReadOnlyDictionary<CounterType, int> - try as property first, then field
                var countersProp = instanceType.GetProperty("Counters", BindingFlags.Public | BindingFlags.Instance);
                object countersObj = null;
                if (countersProp != null)
                {
                    countersObj = countersProp.GetValue(instance);
                }
                else
                {
                    var countersField = instanceType.GetField("Counters", BindingFlags.Public | BindingFlags.Instance);
                    if (countersField != null)
                        countersObj = countersField.GetValue(instance);
                }

                if (countersObj == null) return result;

                // Iterate via IEnumerable (each entry is KeyValuePair<CounterType, int>)
                var enumerable = countersObj as IEnumerable;
                if (enumerable == null) return result;

                foreach (var entry in enumerable)
                {
                    if (entry == null) continue;
                    var entryType = entry.GetType();
                    var keyProp = entryType.GetProperty("Key");
                    var valueProp = entryType.GetProperty("Value");
                    if (keyProp == null || valueProp == null) continue;

                    var key = keyProp.GetValue(entry);
                    var value = valueProp.GetValue(entry);
                    if (key == null || value == null) continue;

                    int count = 0;
                    if (value is int i) count = i;
                    else if (int.TryParse(value.ToString(), out int parsed)) count = parsed;

                    if (count > 0)
                    {
                        result.Add((FormatCounterTypeName(key.ToString()), count));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider", $"Error reading counters: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Gets the ZoneType string from a card's model (e.g., "Hand", "Command", "Graveyard").
        /// This is the game's internal zone, which may differ from the UI holder zone
        /// (e.g., commander cards are in Command zone but visually placed in the hand holder).
        /// </summary>
        public static string GetModelZoneTypeName(object model)
        {
            if (model == null) return null;
            try
            {
                if (!_zoneTypePropSearched)
                {
                    _zoneTypePropSearched = true;
                    _zoneTypePropCached = model.GetType().GetProperty("ZoneType",
                        BindingFlags.Public | BindingFlags.Instance);
                }

                PropertyInfo prop = _zoneTypePropCached;
                if (prop != null && !prop.DeclaringType.IsAssignableFrom(model.GetType()))
                    prop = model.GetType().GetProperty("ZoneType", BindingFlags.Public | BindingFlags.Instance);

                if (prop != null)
                {
                    var val = prop.GetValue(model);
                    return val?.ToString();
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Gets the ZoneType name from a card GameObject.
        /// Chains: GetDuelSceneCDC -> GetCardModel -> GetModelZoneTypeName.
        /// </summary>
        public static string GetCardZoneTypeName(GameObject card)
        {
            if (card == null) return null;
            var cdc = CardModelProvider.GetDuelSceneCDC(card);
            if (cdc == null) return null;
            var model = CardModelProvider.GetCardModel(cdc);
            return GetModelZoneTypeName(model);
        }

        #endregion

        #region Targeting

        /// <summary>
        /// Gets the list of InstanceIds this card is targeting.
        /// Reads Instance.TargetIds field (List of uint).
        /// </summary>
        public static List<uint> GetTargetIds(object model)
        {
            var result = new List<uint>();
            var instance = GetModelInstance(model);
            if (instance == null) return result;
            try
            {
                if (!_targetIdsFieldSearched)
                {
                    _targetIdsFieldSearched = true;
                    _targetIdsField = instance.GetType().GetField("TargetIds",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_targetIdsField != null)
                {
                    var val = _targetIdsField.GetValue(instance);
                    if (val is IList list)
                    {
                        foreach (var item in list)
                        {
                            if (item is uint id) result.Add(id);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Gets the list of InstanceIds of cards targeting this card.
        /// Reads Instance.TargetedByIds field (List of uint) from MtgEntity.
        /// </summary>
        public static List<uint> GetTargetedByIds(object model)
        {
            var result = new List<uint>();
            var instance = GetModelInstance(model);
            if (instance == null) return result;
            try
            {
                if (!_targetedByIdsFieldSearched)
                {
                    _targetedByIdsFieldSearched = true;
                    _targetedByIdsField = instance.GetType().GetField("TargetedByIds",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_targetedByIdsField != null)
                {
                    var val = _targetedByIdsField.GetValue(instance);
                    if (val is IList list)
                    {
                        foreach (var item in list)
                        {
                            if (item is uint id) result.Add(id);
                        }
                    }
                }
            }
            catch { }
            return result;
        }

        /// <summary>
        /// Collects all DuelScene_CDC card models from the stack zone.
        /// Returns list of (model, instanceId, grpId) for name resolution.
        /// </summary>
        private static List<(object model, uint instanceId, uint grpId)> GetAllStackCardModels()
        {
            var results = new List<(object model, uint instanceId, uint grpId)>();

            foreach (var go in GameObject.FindObjectsOfType<GameObject>())
            {
                if (go == null || !go.activeInHierarchy) continue;
                if (!go.name.Contains("StackCardHolder")) continue;

                foreach (var child in go.GetComponentsInChildren<Transform>(true))
                {
                    if (child == null || !child.gameObject.activeInHierarchy) continue;

                    var cdc = CardModelProvider.GetDuelSceneCDC(child.gameObject);
                    if (cdc == null) continue;

                    var model = CardModelProvider.GetCardModel(cdc);
                    if (model == null) continue;

                    uint instanceId = GetModelInstanceId(model);
                    if (instanceId == 0) continue;

                    uint grpId = GetModelGrpId(model);
                    results.Add((model, instanceId, grpId));
                }
                break;
            }

            return results;
        }

        /// <summary>
        /// Resolves an InstanceId to a card name by scanning battlefield and stack cards.
        /// Returns null if not found.
        /// </summary>
        public static string ResolveInstanceIdToNameExtended(uint instanceId)
        {
            if (instanceId == 0) return null;

            // Try battlefield first
            string name = ResolveInstanceIdToName(instanceId);
            if (name != null) return name;

            // Try stack
            try
            {
                var stackCards = GetAllStackCardModels();
                foreach (var (cardModel, id, grpId) in stackCards)
                {
                    if (id == instanceId)
                    {
                        return grpId > 0 ? CardModelProvider.GetNameFromGrpId(grpId) : null;
                    }
                }
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Gets formatted targeting text for announcing a card.
        /// Returns text describing what this card targets and what targets it.
        /// Format: ", targeting X, targeted by Y" (with leading ", " like GetAttachmentText).
        /// </summary>
        public static string GetTargetingText(GameObject card)
        {
            if (card == null) return "";

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(card);
            if (cdcComponent == null) return "";

            var model = CardModelProvider.GetCardModel(cdcComponent);
            if (model == null) return "";

            var result = new List<string>();

            try
            {
                // Check what this card targets
                var targetIds = GetTargetIds(model);
                if (targetIds.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var id in targetIds)
                    {
                        string name = ResolveInstanceIdToNameExtended(id);
                        if (!string.IsNullOrEmpty(name))
                            names.Add(name);
                    }
                    if (names.Count == 1)
                    {
                        result.Add(Models.Strings.Card_Targeting(names[0]));
                    }
                    else if (names.Count == 2)
                    {
                        result.Add(Models.Strings.Card_TargetingTwo(names[0], names[1]));
                    }
                    else if (names.Count > 2)
                    {
                        result.Add(Models.Strings.Card_TargetingMany(string.Join(", ", names)));
                    }
                }

                // Check what targets this card
                var targetedByIds = GetTargetedByIds(model);
                if (targetedByIds.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var id in targetedByIds)
                    {
                        string name = ResolveInstanceIdToNameExtended(id);
                        if (!string.IsNullOrEmpty(name))
                            names.Add(name);
                    }
                    if (names.Count == 1)
                    {
                        result.Add(Models.Strings.Card_TargetedBy(names[0]));
                    }
                    else if (names.Count == 2)
                    {
                        result.Add(Models.Strings.Card_TargetedByTwo(names[0], names[1]));
                    }
                    else if (names.Count > 2)
                    {
                        result.Add(Models.Strings.Card_TargetedByMany(string.Join(", ", names)));
                    }
                }
            }
            catch (Exception ex)
            {
                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider", $"Error getting targeting text: {ex.Message}");
            }

            if (result.Count == 0) return "";
            return ", " + string.Join(", ", result);
        }

        #endregion

        #region Card Categorization

        /// <summary>
        /// Checks if a card on the stack is a triggered or activated ability rather than a spell.
        /// Returns: (isAbility, isTriggered) - isTriggered distinguishes triggered vs activated.
        /// Language-agnostic: checks CardTypes enum values, not localized type line.
        /// </summary>
        public static (bool isAbility, bool isTriggered) IsAbilityOnStack(GameObject cardObj)
        {
            if (cardObj == null) return (false, false);

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(cardObj);
            if (cdcComponent == null) return (false, false);

            var model = CardModelProvider.GetCardModel(cdcComponent);
            if (model == null) return (false, false);

            var modelType = model.GetType();

            // Log model properties once to discover ability-specific fields
            CardModelProvider.LogModelProperties(model);

            // Check CardTypes - spells have Instant, Sorcery, Creature, etc.
            // Abilities on the stack won't have these standard spell types
            var cardTypes = CardModelProvider.GetModelPropertyValue(model, modelType, "CardTypes") as IEnumerable;
            if (cardTypes != null)
            {
                bool hasSpellType = false;
                bool hasAbilityType = false;

                foreach (var ct in cardTypes)
                {
                    if (ct == null) continue;
                    string typeStr = ct.ToString();

                    // Check for standard spell types (language-agnostic enum values)
                    if (typeStr == "Instant" || typeStr == "Sorcery" ||
                        typeStr == "Creature" || typeStr == "Artifact" ||
                        typeStr == "Enchantment" || typeStr == "Planeswalker" ||
                        typeStr == "Land" || typeStr == "Battle" ||
                        typeStr == "Kindred")
                    {
                        hasSpellType = true;
                    }

                    // Check for Ability type
                    if (typeStr == "Ability" || typeStr.Contains("Ability"))
                    {
                        hasAbilityType = true;
                    }
                }

                DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider", $"IsAbilityOnStack: hasSpellType={hasSpellType}, hasAbilityType={hasAbilityType}");

                // If has explicit Ability type or no spell types, it's an ability
                if (hasAbilityType || !hasSpellType)
                {
                    // Try to determine if triggered vs activated
                    // Check for AbilityType, TriggerType, or similar properties
                    var abilityType = CardModelProvider.GetModelPropertyValue(model, modelType, "AbilityType");
                    var triggerType = CardModelProvider.GetModelPropertyValue(model, modelType, "TriggerType");
                    var abilityCategory = CardModelProvider.GetModelPropertyValue(model, modelType, "AbilityCategory");

                    DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider", $"Ability properties: AbilityType={abilityType}, TriggerType={triggerType}, AbilityCategory={abilityCategory}");

                    if (abilityType != null)
                    {
                        string typeVal = abilityType.ToString();
                        bool isTriggered = typeVal.Contains("Trigger") || typeVal.Contains("Triggered");
                        return (true, isTriggered);
                    }

                    if (triggerType != null)
                    {
                        // If TriggerType exists and is not None/null, it's a triggered ability
                        string triggerVal = triggerType.ToString();
                        bool isTriggered = !string.IsNullOrEmpty(triggerVal) && triggerVal != "None";
                        return (true, isTriggered);
                    }

                    if (abilityCategory != null)
                    {
                        string categoryVal = abilityCategory.ToString();
                        bool isTriggered = categoryVal.Contains("Trigger") || categoryVal.Contains("Triggered");
                        return (true, isTriggered);
                    }

                    // Fallback: if no spell types found, assume triggered ability
                    // (most common case for things going on stack automatically)
                    return (true, true);
                }
            }

            return (false, false);
        }

        /// <summary>
        /// Gets card category info (creature, land, opponent) in a single Model lookup.
        /// More efficient than calling IsCreatureCard/IsLandCard/IsOpponentCard separately.
        /// </summary>
        public static (bool isCreature, bool isLand, bool isOpponent) GetCardCategory(GameObject card)
        {
            if (card == null) return (false, false, false);

            bool isCreature = false;
            bool isLand = false;
            bool isOpponent = false;

            var cdcComponent = CardModelProvider.GetDuelSceneCDC(card);
            if (cdcComponent != null)
            {
                var model = CardModelProvider.GetCardModel(cdcComponent);
                if (model != null)
                {
                    try
                    {
                        var modelType = model.GetType();

                        // Check ownership from ControllerNum
                        var controllerProp = modelType.GetProperty("ControllerNum");
                        if (controllerProp != null)
                        {
                            var controller = controllerProp.GetValue(model);
                            isOpponent = controller?.ToString() == "Opponent";
                        }

                        // Check IsBasicLand property
                        var isBasicLandProp = modelType.GetProperty("IsBasicLand");
                        if (isBasicLandProp != null && (bool)isBasicLandProp.GetValue(model))
                            isLand = true;

                        // Check IsLandButNotBasic property
                        if (!isLand)
                        {
                            var isLandNotBasicProp = modelType.GetProperty("IsLandButNotBasic");
                            if (isLandNotBasicProp != null && (bool)isLandNotBasicProp.GetValue(model))
                                isLand = true;
                        }

                        // Check CardTypes for Creature and Land
                        var cardTypesProp = modelType.GetProperty("CardTypes");
                        if (cardTypesProp != null)
                        {
                            var cardTypes = cardTypesProp.GetValue(model) as IEnumerable;
                            if (cardTypes != null)
                            {
                                foreach (var cardType in cardTypes)
                                {
                                    string typeStr = cardType?.ToString() ?? "";
                                    if (typeStr == "Creature" || typeStr.Contains("Creature"))
                                        isCreature = true;
                                    if (typeStr == "Land" || typeStr.Contains("Land"))
                                        isLand = true;
                                }
                            }
                        }

                        return (isCreature, isLand, isOpponent);
                    }
                    catch (Exception ex)
                    {
                        DebugConfig.LogIf(DebugConfig.LogCardInfo, "CardStateProvider", $"Error in GetCardCategory: {ex.Message}");
                    }
                }
            }

            // Fallback for ownership if Model not available
            isOpponent = IsOpponentCardFallback(card);
            return (isCreature, isLand, isOpponent);
        }

        /// <summary>
        /// Checks if a card is a creature based on its CardTypes from the Model.
        /// For single checks only - use GetCardCategory() when checking multiple properties.
        /// </summary>
        public static bool IsCreatureCard(GameObject card)
        {
            return GetCardCategory(card).isCreature;
        }

        /// <summary>
        /// Checks if a card is a land based on its CardTypes or IsBasicLand/IsLandButNotBasic from the Model.
        /// For single checks only - use GetCardCategory() when checking multiple properties.
        /// </summary>
        public static bool IsLandCard(GameObject card)
        {
            return GetCardCategory(card).isLand;
        }

        /// <summary>
        /// Checks if a card belongs to the opponent.
        /// For single checks only - use GetCardCategory() when checking multiple properties.
        /// </summary>
        public static bool IsOpponentCard(GameObject card)
        {
            return GetCardCategory(card).isOpponent;
        }

        /// <summary>
        /// Fallback method to determine opponent ownership via hierarchy/position.
        /// Used when Model is not available.
        /// </summary>
        private static bool IsOpponentCardFallback(GameObject card)
        {
            if (card == null) return false;

            // Check parent hierarchy for ownership indicators
            // Only use "local"/"opponent" markers, not hardcoded player numbers
            // (local player could be player 1 or 2 depending on game state)
            Transform current = card.transform;
            while (current != null)
            {
                string name = current.name.ToLower();
                if (name.Contains("opponent"))
                    return true;
                if (name.Contains("local"))
                    return false;

                current = current.parent;
            }

            // Final fallback: Check screen position (top 60% = opponent)
            Vector3 screenPos = Camera.main?.WorldToScreenPoint(card.transform.position) ?? Vector3.zero;
            if (screenPos != Vector3.zero)
            {
                return screenPos.y > Screen.height * 0.6f;
            }

            return false;
        }

        #endregion
    }
}
