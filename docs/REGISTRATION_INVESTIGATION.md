# Registration Form Investigation

Status: **403 confirmed with ALL activation methods — including game's own native code path.** The post-registration `ConnectToFrontDoor` auto-login fails regardless of how the submit button is clicked. Not yet tested without MelonLoader.

## Summary of All Tests

Three different activation methods have been tested. All produce the same result: registration succeeds (email sent), but `ConnectToFrontDoor` returns 403.

| Activation Method | Code Path | Result |
|---|---|---|
| Mod: `Panel.OnAccept()` via reflection | Mod → Panel.OnAccept() → CustomButton.Click() → OnButton_SubmitRegistration | 403 |
| Mod: `SimulatePointerClick` | Mod → OnPointerUp → UnityEvent.Invoke → OnButton_SubmitRegistration | 403 |
| Game native (zero mod involvement) | OldInputHandler.Update() → ActionSystem → Panel.OnAccept() → CustomButton.Click() → OnButton_SubmitRegistration | 403 |

**Conclusion:** The mod's activation method is NOT the cause. The 403 occurs even when the game's own `OldInputHandler.Update()` handles Enter with zero mod involvement in the activation.

## Current Architecture (after native-path changes)

For the RegistrationPanel submit button specifically:
- `AllowNativeEnterOnLogin = true` — all Enter blocks are lifted
- `BlockSubmitForToggle = false` — game's `GetKeyDown(Return)` returns true
- Mod's Enter handling does NOT activate (early return in BaseNavigator)
- Game's `OldInputHandler.Update()` → `ActionSystem` → `Panel.OnAccept()` handles it natively

For all OTHER Login scene elements:
- Enter is still blocked from the game (6-layer blocking as before)
- Mod handles activation via `UIActivator.Activate()` / `SimulatePointerClick()`

## Architecture: How Enter Reaches Registration

MTGA has **two independent input systems** that both detect Enter:

### Old Input System (`UnityEngine.Input`)
- `OldInputHandler.Update()` polls `Input.GetKeyDown(KeyCode.Return)` → fires `Accept` event → `ActionSystem` → `Panel.OnAccept()` → `_mainButton.Click()`
- `StandaloneInputModule` / `CustomStandaloneInputModule` → `SendSubmitEventToSelectedObject` → fires `ISubmitHandler.OnSubmit` on EventSystem selected object
- `KeyboardManager.PublishKeyDown(Return)` → notifies subscribers (Panel implements `IKeyDownSubscriber` but only handles Escape)

### New Input System (`UnityEngine.InputSystem`)
- `NewInputHandler.OnAccept(InputAction.CallbackContext)` fires via InputAction callback → fires `Accept` event → `ActionSystem` → `Panel.OnAccept()` → `_mainButton.Click()`
- `CustomUIInputModule` (extends `InputSystemUIInputModule`) → fires Submit events on EventSystem selected object. BUT `CustomButton` does NOT implement `ISubmitHandler` (only pointer handlers), so this path has no effect on buttons.

### Which system is active?
`ActionSystemFactory.UseNewInput` (feature toggle `"use_new_unity_input"`) determines which handler is created. `CustomInputModule.Start()` uses the same toggle to choose `CustomStandaloneInputModule` or `CustomUIInputModule`. Only ONE of each is active.

## Decompiled Types (all in `llm-docs/decompiled/`)

- `OldInputHandler.cs` — `Core.Code.Input.OldInputHandler` (Core.dll): polls `Input.GetKeyDown` for all actions
- `NewInputHandler.cs` — `Core.Code.Input.NewInputHandler` (Core.dll): uses InputAction callbacks, `Update()` is empty
- `ActionSystem.cs` — `Core.Code.Input.ActionSystem` (Core.dll): subscribes to `IInputHandler.Accept` → calls `IAcceptActionHandler.OnAccept()`. No additional state management — just dispatches events to focused handlers.
- `ActionSystemFactory.cs` — `Core.Code.Input.ActionSystemFactory` (Core.dll): creates OldInputHandler or NewInputHandler based on feature toggle
- `CustomInputModule.cs` — `Wotc.Mtga.CustomInput.CustomInputModule` (Core.dll): adds CustomStandaloneInputModule or CustomUIInputModule
- `CustomUIInputModule.cs` — `Wotc.Mtga.CustomInput.CustomUIInputModule` (Core.dll): extends InputSystemUIInputModule, no Submit override
- `Panel.cs` — `Wotc.Mtga.Login.Panel` (Core.dll): implements IAcceptActionHandler, OnAccept() clicks _mainButton
- `RegistrationPanel.cs` — `Wotc.Mtga.Login.RegistrationPanel` (Core.dll): OnButton_SubmitRegistration() → DoRegistration()
- `LoginScene.cs` — `Wotc.Mtga.Login.LoginScene` (Core.dll): ConnectToFrontDoor() connects via JWT + client version
- `CustomButton.cs` — `CustomButton` (Core.dll): implements IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler (NOT ISubmitHandler). `Click()` calls OnPointerDown + _onClick.Invoke() directly. `OnPointerUp()` checks `_mouseOver` before invoking _onClick.

## Enter Blocking on Login Scene

### For RegistrationPanel submit button (`AllowNativeEnterOnLogin = true`)
- `BlockSubmitForToggle = false` → `GetKeyDown(Return)` returns true to all callers
- `NewInputHandler.OnAccept()` → allowed through
- `KeyboardManager.PublishKeyDown/Up(Return)` → allowed through
- Mod's BaseNavigator detects Enter but returns without activating
- **Game handles activation entirely through its own native code path**

### For all other Login elements (`AllowNativeEnterOnLogin = false`)
- `Input.GetKeyDown(Return)` → blocked (patched, sets `EnterPressedWhileBlocked`)
- `SendSubmitEventToSelectedObject()` → blocked (`BlockSubmitForToggle = true`)
- `KeyboardManager.PublishKeyDown(Return)` → blocked (scene-based check)
- `KeyboardManager.PublishKeyUp(Return)` → blocked (scene-based check)
- `NewInputHandler.OnAccept()` → blocked (scene-based check)
- **Only activation:** mod handles via `UIActivator.Activate()`

## Diagnostic Results

### Test 1: Mod calls Panel.OnAccept() via reflection
```
Activating: MainButton_Register (ID:-2846, Label:Bestätigen)
Invoking RegistrationPanel.OnAccept() via Panel base
>>> OnButton_SubmitRegistration CALLED <<<
Stack: Panel.OnAccept() → CustomButton.Click() → UnityEvent.Invoke → OnButton_SubmitRegistration
(~10 seconds later) Accessible Arena shutting down
```
- Exactly ONE call. Registration succeeds (email sent), 403 on auto-login.

### Test 2: Mod calls SimulatePointerClick
```
Activating: MainButton_Register (ID:-2846, Label:Bestätigen)
Simulating pointer events on: MainButton_Register
>>> OnButton_SubmitRegistration CALLED <<<
Stack: UIActivator.SimulatePointerClick → CustomButton.OnPointerUp → UnityEvent.Invoke → OnButton_SubmitRegistration
(~4.5 seconds later) Accessible Arena shutting down
```
- Exactly ONE call. Same 403 result.

### Test 3: Game's own native code path (zero mod activation)
```
BlockSubmitForToggle changed: True -> False  (user navigated to submit button)
>>> OnButton_SubmitRegistration CALLED <<<
Stack: OldInputHandler.Update() → ActionSystem.<.ctor>b__7_1() → Panel.OnAccept() → CustomButton.Click() → UnityEvent.Invoke → OnButton_SubmitRegistration
(~23 seconds later) Accessible Arena shutting down
```
- Exactly ONE call. **Game's own `OldInputHandler.Update()` fired Enter.** Zero mod code in the stack trace. Same 403 result.

## What's Been Ruled Out

1. **Double-submit** — Diagnostic proves exactly ONE `OnButton_SubmitRegistration` call in all tests
2. **Mod's activation code path** — Three different activation methods tested (reflection, pointer simulation, game native) — all produce same 403
3. **Mod involvement in activation** — Test 3 has zero mod code in the stack trace. The game's own `OldInputHandler.Update()` detected Enter and called `Panel.OnAccept()` through `ActionSystem`. Same 403.
4. **Dropdown 500ms timer** — All dropdown `onValueChanged` properly suppressed/restored/fired. Timer expired 68+ seconds before submit
5. **Dropdown onValueChanged suppression** — Log shows every "Suppressed" has matching "Restored" and "Fired"
6. **`ShouldBlockEnterFromGame`** — Cleared when dropdown closed, false at submit time
7. **Leaked KeyUp event** — Both KeyDown and KeyUp blocked on Login scene (scene-based, not button-state-based)

## Post-Registration 403: What We Know

### ConnectToFrontDoor (decompiled)
```csharp
public void ConnectToFrontDoor(AccountInformation accountInfo)
{
    FDCConnectionParams parameters = new FDCConnectionParams
    {
        Host = _currentEnvironment.fdHost,
        Port = _currentEnvironment.fdPort,
        SessionTicket = accountInfo.Credentials.Jwt,
        ClientVersion = Global.VersionInfo.ContentVersion.ToString(),
        IsDebugAccount = (accountInfo.HasRole_Debugging() || Debug.isDebugBuild),
        AcceptsPolicy = () => RegistrationPanel.PolicyAcceptedThisSession
                           || UpdatePoliciesPanel.PolicyAcceptedThisSession
    };
    _frontDoorConnection.Connect(parameters);
    LoadNextPanelBasedOnLoginState();
}
```

The FrontDoor connection uses:
- **JWT token** from registration response (`accountInfo.Credentials.Jwt`)
- **Client version** string (`Global.VersionInfo.ContentVersion`)
- **Policy acceptance** lambda checking `RegistrationPanel.PolicyAcceptedThisSession`

### LoginScene.Update() — auto-transition on success
```csharp
private void Update()
{
    if (!_exiting && _accountClient != null && _frontDoorConnection != null)
    {
        bool loggedIn = _accountClient.CurrentLoginState == LoginState.FullyRegisteredLogin;
        bool connected = _frontDoorConnection.Connected;
        bool notQueued = !_frontDoorConnection.IsQueued;
        if (loggedIn && connected && notQueued)
        {
            _exiting = true;
            this.LoggedIn?.Invoke();
        }
    }
}
```
If ConnectToFrontDoor fails (403), `Connected` never becomes true, and the game never transitions out of the Login scene. Eventually the game quits (`OnApplicationQuit` fires).

## Remaining Suspects

### 1. MelonLoader/Harmony presence (most likely)
MelonLoader injects into the Unity process and Harmony modifies game methods at the IL level (~30+ patches). If the FrontDoor server performs any client integrity validation (method hashes, DLL checksums, process integrity), the patched methods would fail those checks.

### 2. Diagnostic Harmony patch on OnButton_SubmitRegistration
The `SubmitRegistrationDiagnostic_Prefix` modifies `OnButton_SubmitRegistration` via Harmony IL patching. Even though it only logs, the method's IL is altered. This could trip integrity checks.

### 3. Game/server bug unrelated to the mod
The 403 might occur without the mod installed. **Not yet tested.**

## Critical Next Step

**Test registration without MelonLoader entirely.** Rename `version.dll` (or `winhttp.dll`) in the MTGA folder to temporarily disable MelonLoader. If the 403 also occurs without the mod, it's a game/server issue.

## Previous Attempts That Failed

### String-based Harmony patch on `Panel.OnAccept` — SILENTLY FAILED
```csharp
[HarmonyPatch("Wotc.Mtga.Login.Panel", "OnAccept")]
[HarmonyPrefix]
public static bool PanelOnAccept_Prefix() { ... }
```
Never fired — no log output. String-based `[HarmonyPatch]` attributes silently fail when the type isn't found at attribute processing time. Replaced with runtime patching via `FindType()` + `harmony.Patch()`.

### ConsumeKey for Login scene elements — NO EFFECT
`ConsumeKey(Return)` only blocks `KeyboardManager.PublishKeyDown` via `IsKeyConsumed()`. Doesn't block `NewInputHandler` callbacks or `InputSystemUIInputModule`.

### BlockNextEnterKeyUp pattern (craft fix) — INSUFFICIENT
Blocks Enter KeyUp from `KeyboardManager.PublishKeyUp`. Correct pattern but doesn't address the new Input System which uses InputAction callbacks, not KeyUp/KeyDown. Also redundant on Login scene where `ShouldBlockKey` permanently blocks.

## CRITICAL: Do NOT Globally Intercept Input.GetKeyDown(Return)

**Reverted in March 2026 after breaking all duel Enter handling.**

Commit `ff141d0` attempted to fix the registration phantom-submit by globally blocking
`Input.GetKeyDown(Return)` whenever any navigator was active (via `EventSystemPatch.GetKeyDown_Postfix`).

### Why it broke duels
- `Input.GetKeyDown` is a Unity API that **our own mod calls** (e.g., DuelNavigator's Enter guard)
- The Harmony postfix intercepts ALL callers, including our own code
- DuelNavigator's Enter guard sees `false` → Enter falls through to BaseNavigator → opens settings menu
- The `EnterPressedWhileBlocked` secondary channel was only checked by BaseNavigator

### The rule
**Never patch `Input.GetKeyDown` with a global scope.** Scope: `BlockSubmitForToggle == true` only.

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
- If server rejects username: `_validDisplayName` stays `false`, button permanently disabled

### Panel.OnAccept (base class)
```csharp
public virtual void OnAccept()
{
    current.SetSelectedGameObject(_mainButton.gameObject);
    if (_mainButton.Interactable)
    {
        _mainButton.Click();
        EnableButton(enabled: false);
    }
}
```
Always clicks `_mainButton` regardless of current focus. `EnableButton(false)` immediately disables the button, preventing any subsequent OnAccept call (e.g., from a delayed KeyUp) from triggering a second click.

### OnButton_SubmitRegistration
```csharp
public void OnButton_SubmitRegistration()
{
    // Clear all feedback, disable button, send analytics
    EnableButton(enabled: false);
    DoRegistration();
    AudioManager.PlayAudio(WwiseEvents.sfx_ui_accept, base.gameObject);
}
```
Also calls `EnableButton(false)` — button is disabled by both Panel.OnAccept AND OnButton_SubmitRegistration.
