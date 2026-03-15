# Registration Form Investigation

Status: **In Progress** — button stays disabled, root cause not yet confirmed.

## Problem

The registration form's "Bestätigen" (Confirm) button is never interactable when activated via our mod. The game's `_checkFields()` runs every frame in `Update()` and controls the button's enabled state via `CustomButton.Interactable`. Our `SimulatePointerClick` cannot trigger a non-interactable CustomButton.

## How Registration Validation Works (decompiled)

Source: `llm-docs/decompiled/RegistrationPanel.cs`

### _checkFields() — runs every frame

Enables button ONLY when ALL conditions pass:
1. Password >= 8 chars, no rule violations
2. Display name 3-23 chars AND `_validDisplayName == true`
3. Email not empty
4. Email matches email confirmation
5. Password matches password confirmation
6. All 3 required toggles on (terms, codeOfConduct, privacyPolicy)
7. `_submitting == false`

### _validDisplayName flow

- Defaults to `false`
- `_displayName_select` (onSelect callback): resets to `false` every time the display name field is focused
- `_displayName_endEdit` (onEndEdit callback): starts `Coroutine_ValidateUsername` (async server call)
- `Coroutine_ValidateUsername`: sets `_validDisplayName = false` at start, then on server success sets `true`
- If server rejects username: `_validDisplayName` stays `false`, input field stays disabled (`InputField.enabled = false`)

### Panel.EnableButton / OnAccept

- `EnableButton(bool)` sets `_mainButton.Interactable` on the CustomButton
- `Panel.OnAccept()` checks `_mainButton.Interactable` before calling `_mainButton.Click()`
- `CustomButton.Click()` checks `_interactable` internally — safe path
- `CustomButton.OnPointerUp()` also checks `_interactable` AND `_mouseOver` AND `!PointerIsHeldDown()` — our pointer simulation path

## Confirmed Findings

### Button is NOT interactable (confirmed via log)

Added interactable check in `UIActivator.Activate()`. Log consistently shows:
```
CustomButton 'MainButton_Register' is NOT interactable - click blocked
```
Even after all fields are filled and all toggles checked. 23+ seconds after display name endEdit fires.

### Diagnostic logging added

Next build will log the exact failing condition when button click is blocked:
- `_validDisplayName` value
- `_submitting` value
- All input field texts and enabled states
- Toggle states

### Our onEndEdit fix works mechanically

Log confirms `onEndEdit` fires correctly on each field during Tab:
```
Tab: firing onEndEdit on old field Input Field - Displayname with text: 'klickernst'
Tab: firing onEndEdit on old field Input Field - Email 1 with text: 'klickernst@fs.es47.de'
```

### Previous SimulatePointerClick was silently failing

Before the interactable check, `SimulatePointerClick` returned success ("Aktiviert") even when the CustomButton blocked the click internally. The user thought registration was being attempted but it wasn't.

## Top Theories for Why _validDisplayName Stays False

### Theory 1: Duplicate onEndEdit causes double coroutine

When Tab triggers the race condition path:
1. Unity's `OnDeselect` fires `onEndEdit` on the old field (automatic)
2. Our code fires `onEndEdit` AGAIN on the old field (explicit)

This means `_displayName_endEdit` runs TWICE, starting TWO `Coroutine_ValidateUsername` coroutines. Both set `_validDisplayName = false` at their start. If one coroutine's server call fails (rate limit? duplicate request?), the sequence could be:
- Coroutine 1 succeeds → `_validDisplayName = true`
- Coroutine 2 fails → `_validDisplayName` stays `true` (error handler doesn't touch it)

This should be OK, but needs verification. The duplicate could cause server-side issues.

### Theory 2: onEndEdit fires but _displayName_endEdit is not called

`_displayName_endEdit` is registered on `displayName_inputField.InputField.onEndEdit`. Our code gets the TMP_InputField via `expectedField.GetComponent<TMPro.TMP_InputField>()`. If `UIWidget_InputField_Registration` wraps the TMP_InputField in a way where the `onEndEdit` event object is different from what we invoke, the listeners might not fire.

To verify: check if `displayName_inputField.InputField.gameObject` is the same as the focused `expectedField` GameObject.

### Theory 3: Coroutine_ValidateUsername never completes

The coroutine yields on `_loginScene._accountClient.ValidateUsername(username)`. If this promise never resolves (network issue, server not responding, or some state issue), `_validDisplayName` stays `false` forever. The coroutine also sets `InputField.enabled = false` — if it never completes, the input field stays disabled.

### Theory 4: _displayName_select resets after validation

If something causes the display name field to be re-selected after the coroutine completes, `_displayName_select` resets `_validDisplayName = false`. This could happen if:
- Our Tab suppression code triggers `onSelect` on the display name field
- Some UI event re-focuses the display name field
- EventSystem focus changes hit the display name field

### Theory 5: _displayName_endEdit reads wrong text

`_displayName_endEdit` ignores the `arg0` parameter and reads `displayName_inputField.InputField.text` directly. If the InputField's text property is empty or wrong (e.g., cleared by deactivation), the length check `< 3` would fail, and no coroutine would start.

## Changes Made (EXPERIMENTAL)

### UIActivator.cs — CustomButton interactable check

Added check before `SimulatePointerClick` for CustomButtons. If `Interactable == false`, returns `ActivationResult(false, "Deaktiviert")` instead of silently failing. Also added `DiagnoseRegistrationState()` method that logs all validation state via reflection when the registration button click is blocked.

**Risk**: Low. Only adds a check before existing code, returns early with failure. Does not change activation behavior for interactable buttons.

### UIFocusTracker.cs — Tab onEndEdit handling

Modified Tab race condition handler to:
1. Explicitly fire `onEndEdit` on the OLD field with correct text
2. Suppress `onEndEdit` on the NEW field by temporarily swapping out listeners
3. Deselect the new field without triggering its onEndEdit

**Risk**: Medium. Changes how onEndEdit fires during Tab navigation. Could cause duplicate onEndEdit calls (Unity fires one automatically, we fire another). Could affect any screen with input fields and Tab navigation.

### llm-docs/type-index.md — Documentation

Added Login/Registration types section and critical field/property notes.

## Next Steps

1. **Check diagnostic log** — run the game, fill in registration form, click Bestätigen, check which condition fails
2. **If _validDisplayName is false**: investigate whether `_displayName_endEdit` actually runs (add logging in UIFocusTracker where we invoke onEndEdit)
3. **If fields have wrong text**: investigate whether Tab handling corrupts field text
4. **If InputField.enabled is false**: the validation coroutine started but never completed (server call issue)
5. **Consider preventing duplicate onEndEdit**: check if Unity already fired onEndEdit before our explicit call, skip our call if so
6. **Consider using Panel.OnAccept()**: instead of SimulatePointerClick, invoke OnAccept() on the RegistrationPanel directly — this is the game's intended keyboard activation path and uses `Click()` which doesn't have the `_mouseOver`/`PointerIsHeldDown` issues

## Key Decompiled Types

- `RegistrationPanel` — `llm-docs/decompiled/RegistrationPanel.cs`
- `Panel` (base class) — `llm-docs/decompiled/Panel.cs`
- `CustomButton` — `llm-docs/decompiled/CustomButton.cs`
