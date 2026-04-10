# Authentication Credential Manager — Implementation Plan

## Problem Statement

The **Navigate to URL** node currently has no authentication support. Users who automate
login-protected pages must hard-code credentials directly in node parameters or handle them
outside the tool. This plan introduces a first-class **Credential Manager** system that:

1. Lets users store named credentials securely in an AES-256 / master-password-encrypted file
   database that is app-wide and reusable across all flows.
2. Exposes a new **Authentication** section in the Navigate to URL node property panel with a
   text field (showing the selected credential name) and a **…** button that opens the modal
   Credential Manager window.
3. Supports all common web authentication types as a starting point, with Windows auth deferred
   to a later phase.

---

## Proposed Approach

### Layering

| Layer | What it contains |
|---|---|
| `WpfAutomation.App` — new `Credentials/` folder | All credential UI, view models, and the manager window |
| `WpfAutomation.App` — new `Services/Credentials/` | `ICredentialStore`, `CredentialStore` (AES-256 encrypted JSON file) |
| `WpfAutomation.App` — existing Navigate to URL files | New `CredentialRef` property on `NavigateToUrlActionParameters` |
| `WpfAutomation.Core` — existing Playwright execution bridge | Runtime resolution: reads credential, performs auth steps before navigation |

### Security design

- The credential database file lives at `%APPDATA%\WpfAutomation\credentials.db`.
- AES-256-CBC encryption; random IV per file write; PBKDF2-SHA256 key derivation (100 000 iterations) from the master password.
- The unlocked key is held in a `SecureString`-backed session object and released when the app
  closes. The user is challenged for the master password **once, immediately before a flow run
  begins** (first time per session), so time-sensitive steps are not delayed.
- Passwords and secrets in the in-memory credential model are held as `SecureString` and only
  converted to `string` at the exact moment Playwright needs them.

---

## Credential Types — Web Authentication

Each type has a unique `WebAuthKind` discriminator and stores only the fields it needs.

| `WebAuthKind` | Stored fields |
|---|---|
| `UsernamePassword` | `Username`, `Password` |
| `UsernameEmailOtp` | `Username`, `Password`, `ImapHost`, `ImapPort`, `ImapUsername`, `ImapPassword`, `MailboxFolder`, `SubjectContains` + runtime fallback: if IMAP fetch fails or is not configured, a WPF popup textbox appears during the run so the user can paste the code manually |
| `UsernameSmsOtp` | `Username`, `Password`, `PhoneHint` (informational only — code entered via WPF popup at run time) |
| `Totp` | `Username`, `Password`, `TotpSecret` (Base32) |
| `OAuthSso` | `ProviderName`, `Username`, `Password` (provider login page credentials) |
| `HttpBasicAuth` | `Username`, `Password` |
| `ApiKeyBearer` | `TokenName` (display label), `Token` (the bearer/API key value) |
| `CertificateMtls` | `CertificatePath`, `CertificatePassword`, `PrivateKeyPath` |
| `Custom` | `Label`, `Notes` (free-text placeholder for unsupported types) |

---

## Phases

### Phase 1 — Credential Domain Model & Encrypted Storage

- [x] Create `WpfAutomation.App/Credentials/Models/CredentialEntry.cs`
  — abstract record `CredentialEntry` with `Id` (Guid), `Name` (string), `AuthType` (enum
  `HostAuthKind { Web, Windows }`), fields common to all entries.
- [x] Create `WebAuthKind` enum (`UsernamePassword`, `UsernameEmailOtp`, `UsernameSmsOtp`,
  `Totp`, `OAuthSso`, `HttpBasicAuth`, `ApiKeyBearer`, `CertificateMtls`, `Custom`).
- [x] Create `WebCredentialEntry` derived record with `WebAuthKind` discriminator and a
  `Dictionary<string,string> Fields` property for flexible per-type key/value storage.
  Sensitive fields are declared via a static `SensitiveFieldNames` per subtype.
- [x] Create `ICredentialStore` interface in `Services/Credentials/`:
  - `Task<IReadOnlyList<CredentialEntry>> LoadAllAsync()`
  - `Task SaveAsync(CredentialEntry entry)`
  - `Task DeleteAsync(Guid id)`
  - `bool IsUnlocked`
  - `void Unlock(SecureString masterPassword)`
  - `void Lock()`
- [x] Create `CredentialStore : ICredentialStore` — AES-256-CBC, PBKDF2 key derivation,
  serializes to/from JSON, stores at `%APPDATA%\WpfAutomation\credentials.db`.
- [x] Register `ICredentialStore` / `CredentialStore` in the app's service container (check
  existing DI wiring patterns).
- [x] Unit tests for `CredentialStore`: encrypt → decrypt round-trip, wrong password throws,
  add/remove entry persists correctly.

### Phase 2 — Master Password Unlock Flow

- [x] Create `CredentialUnlockWindow.xaml` — simple modal with a `PasswordBox`, OK and Cancel
  buttons, and a retry error label.
- [x] Create `CredentialUnlockViewModel` — validates that the password is non-empty; on OK
  calls `ICredentialStore.Unlock()`; closes the window on success and keeps it open with an
  error message on wrong password.
- [x] Expose `IMasterPasswordService` (application-level: `bool EnsureUnlockedBeforeRun()`)
  implemented by `MasterPasswordService`. When the store is already unlocked for the session,
  it returns `true` immediately; otherwise it shows `CredentialUnlockWindow`.
- [x] Wire `MasterPasswordService.EnsureUnlockedBeforeRun()` into the flow-run entry point
  (locate in `FlowRuntimeExecutor` or `AutomationOrchestrator`) so it fires before the first
  node executes.
- [x] Unit tests: already-unlocked → no prompt shown; wrong password → stays open; correct
  password → unlocks and returns.

### Phase 3 — Credential Manager Window (CRUD UI)

- [x] Create `CredentialManagerWindow.xaml` — modal WPF Window with:
  - Left panel: list of saved credentials (`ListBox`), a `[+ New]` button, and `[Delete]` button.
  - Right panel: detail/edit form that changes dynamically based on the selected credential's
    `HostAuthKind` and `WebAuthKind`.
  - Top of right panel: "Auth platform" radio buttons — **Web** / **Windows** (Windows disabled
    / labelled "coming soon" for now).
  - "Auth type" ComboBox (populated with all 9 `WebAuthKind` values when Web is selected).
  - Dynamic field area — shows exactly the fields relevant to the selected auth type (see table
    in Proposed Approach). Password/secret fields use `PasswordBox`.
  - IMAP sub-section for `UsernameEmailOtp` — collapsible, labelled "Email inbox (optional —
    if not set, you will be prompted for the code at runtime)".
  - `[Save]` / `[Cancel]` buttons; `[Save]` validates required fields before closing.
- [x] Create `CredentialManagerViewModel` — MVVM, `ObservableCollection<CredentialEntryViewModel>`,
  selected entry, add/delete/save commands wired to `ICredentialStore`.
- [x] Create `CredentialEntryViewModel` — exposes all editable fields as bindable properties;
  converts to/from `CredentialEntry` model.
- [x] Ensure the **…** button in the node inspector opens `CredentialManagerWindow` and on
  close returns the selected or newly-created credential's `Id`/`Name` back to the node
  inspector (see Phase 4 wiring).

### Phase 4 — Navigate to URL Node — Authentication Section

- [x] Add `CredentialRef` property to `NavigateToUrlActionParameters`:
  ```csharp
  public sealed record NavigateToUrlActionParameters(
      string Url = "https://example.com",
      int TimeoutMs = 30000,
      bool WaitUntilNetworkIdle = true,
      string? CredentialId = null,    // Guid as string, null = no auth
      string? CredentialName = null   // display-only denormalized copy
  ) : ActionParameters;
  ```
- [x] Update `NavigateToUrlInspectorViewModel` to:
  - Expose `CredentialName` (string, display-only) and `OpenCredentialManagerCommand` (ICommand).
  - `OpenCredentialManagerCommand` invokes `CredentialManagerWindow`; on close, if user picked
    a credential, writes back `CredentialId` + `CredentialName` into the parameters and commits.
  - Keep the existing `JsonActionInspectorViewModelBase` field list for all other properties;
    the credential section is added as dedicated bound properties (not a generic `InspectorFieldViewModel`) because it needs special button behaviour.
- [x] Update `NavigateToUrlInspectorView.xaml` to add an **Authentication** `GroupBox` section:
  - Label: "Authentication"
  - Read-only `TextBox` bound to `CredentialName` (shows "None" when null)
  - `Button` labelled `…` next to it, command bound to `OpenCredentialManagerCommand`
  - Hint text: "Select a credential to use when logging in to this page."
- [x] Update `FlowActionParameterResolver` to use the updated default record (no breaking
  change — `CredentialId` and `CredentialName` default to `null`).

### Phase 5 — Runtime Execution (Playwright Bridge)

- [x] In `PlaywrightFlowExecutionBridge` (or the execution mapper), intercept the
  `navigate-to-url` action step:
  1. If `CredentialId` is null → proceed as today (plain navigation).
  2. If `CredentialId` is set → call `ICredentialStore.GetByIdAsync(id)` to retrieve the
     decrypted credential.
  3. Delegate to a new `WebAuthExecutor` class (in `WpfAutomation.Core` or the App's
     execution layer, whichever owns Playwright calls) that switches on `WebAuthKind` and
     performs the appropriate login sequence.
- [x] **`WebAuthExecutor`** — one strategy class per kind (or a strategy interface
  `IWebAuthStrategy`):
  - `UsernamePassword` — fill selector for username, fill selector for password, click submit.
    Selectors are stored in the credential or resolved via the existing selector logic.
  - `UsernameEmailOtp` / `UsernameSmsOtp` — fill username+password, then either:
    a. Poll IMAP (if credentials stored) for a code matching `SubjectContains` within timeout, OR
    b. Show a WPF dispatcher-invoked popup textbox (`OtpInputWindow`) that the user types into;
       the entered code is then filled into the OTP field on the page.
  - `Totp` — fill username+password, compute TOTP from the stored Base32 secret using RFC 6238,
    fill the code.
  - `OAuthSso` — navigate to the provider login, fill username+password.
  - `HttpBasicAuth` — set Playwright's `HttpCredentials` on the browser context before
    navigation.
  - `ApiKeyBearer` — set an `Authorization: Bearer <token>` header via Playwright's route
    interception or browser context's `SetExtraHTTPHeadersAsync`.
  - `CertificateMtls` — set client certificate on the browser context (Playwright supports this
    via `BrowserNewContextOptions.ClientCertificates`).
  - `Custom` — log a warning; no automated action (credential is informational only).
- [x] Create `OtpInputWindow.xaml` — minimal modal: label saying "Enter the code sent to
  [phone/email]", `TextBox`, `[OK]` button. Invoked from the `WebAuthExecutor` strategies that
  need manual OTP entry, via the `IUiDispatcherService`.
- [x] Integration tests for `UsernamePassword` flow (using a local test HTML page or mock
  Playwright server); stub tests for other types that verify strategy dispatch.

### Phase 6 — Polish, Validation & Tests

- [x] Validate `NavigateToUrlActionParameters` — if `CredentialId` is set but the credential
  is no longer in the store (was deleted), show a warning in the node inspector below the
  credential field: "Credential not found. Please re-select."
- [x] Add `[Clear credential]` affordance next to the `…` button (sets `CredentialId` = null).
- [x] Node canvas visual indicator: if a node has a credential assigned, show a small lock icon
  on the node card (extend `FlowActionNodeControl` or its style).
- [x] Persist `CredentialId` / `CredentialName` through the flow save/load cycle; verify
  `FlowPersistenceModels` / `FlowPersistenceService` round-trip (likely works automatically as
  it serializes `ActionParameters`).
- [x] Test: save a flow that references a credential → reload → `CredentialId` is preserved.
- [x] Accessibility: ensure `CredentialManagerWindow` fields have `AutomationProperties.Name`
  set; `PasswordBox` fields do not expose their content to UI Automation.
- [x] Document the master password warning in a `CredentialSecurityNote.md` in the Credentials
  folder: what is encrypted, what is NOT (credential names/types), how to reset if password is
  forgotten.

---

## Acceptance Criteria

1. **Credential Manager opens** — clicking `…` in the Navigate to URL node inspector opens a
   modal Credential Manager window.
2. **All 9 web auth types** are selectable in the manager; each shows only its relevant fields.
3. **Create / edit / delete** a credential in the manager; changes persist after app restart.
4. **AES-256 encryption** — `credentials.db` is unreadable without the master password; wrong
   password shows an error and does not unlock the store.
5. **Master password prompt** fires once per session, immediately before the run begins.
6. **Node inspector Authentication section** shows the selected credential's name or "None".
7. **UsernamePassword** flow executes end-to-end via Playwright after navigation.
8. **Email OTP fallback** — if IMAP is not configured, a WPF popup appears at runtime for
   manual code entry and the code is sent to the page.
9. **CredentialId round-trips** through flow save/load without loss.
10. **No credential value appears in plaintext** in any log, screen capture, or flow JSON; only
    credential name and ID are written.

---

## Open Questions / Assumptions

| # | Item |
|---|---|
| OQ-1 | **Where to store per-auth-type field selectors** (e.g. "which CSS selector is the username input?"). Current plan stores them in the credential; an alternative is a separate "login profile" attached to the site URL. Flag for discussion before Phase 5. |
| OQ-2 | **Windows authentication** is explicitly deferred. When added in a future phase it will share `ICredentialStore` and `CredentialManagerWindow` infrastructure but have its own `HostAuthKind.Windows` branch. |
| OQ-3 | **IMAP polling timeout and retry** strategy for `UsernameEmailOtp` is left for Phase 5 detailed design. |
| OQ-4 | Master password **reset / migration** — if forgotten, the database cannot be recovered. A deliberate "reset and lose all credentials" option should be added to the UI in a future phase. |
| A-1  | DI is wired in `App.xaml.cs` / `MainViewModel` constructor; `ICredentialStore` will be registered there following the same pattern as existing services. |
| A-2  | `FlowRuntimeExecutor` is the correct interception point for pre-navigation auth; verify this matches `PlaywrightFlowExecutionBridge` before Phase 5 begins. |
