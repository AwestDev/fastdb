# Saved connections & encrypted password storage

> Branch: `feature/saved-connection-credentials` (from `feature/nav-shell` @ `c761d3b`).
> **Status (verified vs not):**
> - Commit 1 (vault): the 4 new vault unit tests passed on the owner's machine. Two older profile-store tests failed only because of the new flag name; fixed in `ae1981c`, **re-run pending**.
> - The WPF project has **not been built yet** by anyone (the owner's first run stopped at the test step).
> - Commit 2 (password field UX, below): written, **NOT compiled or run**.
>
> **This document supersedes Master Context §2 rule 4 and decision D4** ("no passwords on disk"): a SQL password may now be stored, but only encrypted, only per profile, and never shown again.

## Behaviour (commit 2)
All connection data is entered in **one form** (Connections screen). There is **no password popup** any more.

| Situation | Password field |
|---|---|
| New profile / SQL auth | Editable `PasswordBox`, hidden by default, eye button toggles show/hide |
| After Save/Connect | Password stored encrypted, typed text cleared from UI/memory, eye toggle gone |
| Saved profile selected | Locked mask `••••••••••••` (fixed length, reveals nothing) + "Change password" button; no show button |
| "Change password" | Field becomes editable again (empty), eye toggle available, "Cancel" link; Save replaces the stored password (empty + Save keeps the old one) |
| Connect / switch profile (shell selector) | Reads the stored password from the vault; **no prompt** |
| Profile has no usable stored password | Message in the form; switching from the shell selector jumps to Connections |
| Windows auth | No password field at all |

Rules: server/auth/user changed on a profile -> stored password deleted (message shown). Stored password rejected by the server (`LoginFailed`) -> deleted + message, form unlocked. Profile deleted -> entry deleted. Unreadable entry (other Windows account / damaged file) -> treated as "not saved".
The removed "Remember password" checkbox: saving a SQL profile *with a typed password* now stores it (that is the purpose of the form). Saving without a password stores nothing. The Connect button works on a saved profile (as before), so: type password -> Save -> Connect.

## Security
- Storage: `DpapiCredentialVault` = Windows DPAPI, **CurrentUser** scope + app entropy, Base64 in `%AppData%\FastDbExplorer\credentials.json`. Other Windows users (and other PCs) cannot decrypt it. `connections.json` only contains the boolean `RememberPassword` (meaning "an encrypted password exists").
- The UI has **no way to read or show** a stored password; the vault value is read only inside `ConnectProfileAsync` to open the connection.
- Honest limits: DPAPI does not protect against malware running as the same Windows user; the .NET `string` holding the password during connect cannot be wiped (byte buffers in the vault are zeroed). Do not use on shared Windows accounts.
- Accessibility: password field and toggle have `AutomationProperties.Name`; the toggle is a normal focusable button (Tab, Space/Enter), its name/tooltip switch between show/hide; focus stays on the toggle; focus moves to the field when "Change password" is pressed.

## Files
Commit 1: `Application/Abstractions/CredentialAbstractions.cs`, `Infrastructure/DpapiCredentialVault.cs` (+ package `System.Security.Cryptography.ProtectedData 10.*`), `Domain/Models.cs` (`RememberPassword`), tests.
Commit 2: `Wpf/ViewModels/ConnectionViewModel.cs` (password state + flows, popup removed), `Wpf/Views/ConnectionsView.xaml` + `.xaml.cs` (field, toggle, mask, PasswordBox sync), `Wpf/MainWindow.xaml.cs` (popup wiring removed, jump to Connections), `Wpf/Localization/CredentialStrings.cs`, this doc, `CLAUDE.md`.
`Views/SqlPasswordWindow.*` is now unused (not deleted yet; safe to remove after you confirm the build).

## Manual test checklist
1. Build + `dotnet test`.
2. New SQL profile: password is dots by default; eye shows it; eye again hides it; Tab reaches the eye; Space/Enter toggles; Narrator reads "Show/Hide password".
3. Save: field turns into the locked mask, no eye, "Change password" visible, typed text gone.
4. Connect: **no popup**. Disconnect, then switch via the shell profile selector: **no prompt**.
5. Close and reopen the app, connect: **no prompt**.
6. `credentials.json` has Base64 only; `connections.json` has no password value.
7. Change password: press the button, field is empty and editable; type a new one, Save, Connect works. Cancel restores the mask. Empty + Save keeps the old password.
8. Wrong password: login error + "cleared" message, field editable again, nothing stored.
9. Change server or user of a saved profile + Save: message that the password was cleared.
10. Delete profile: entry removed from `credentials.json`.
11. Profile without password, switched from the selector: lands on Connections with the message (no popup).
12. Other Windows user: sign in with another Windows account, copy `credentials.json`: profile behaves as "no stored password"; nothing is shown or crashes.
13. Windows-auth profiles unchanged. Check light/dark and RTL of the new row (eye glyphs `E7B3`/`ED1A`).

## Known risks / not verified
- Commit 2 is uncompiled (XAML + view model + code-behind). Likely trouble: glyph availability, `AutomationProperties` setters in the style, PasswordBox sync.
- NuGet warnings NU1902/NU1903 on `System.Security.Cryptography.Pkcs 9.0.11` appeared at restore. Probably transitive from `Microsoft.Data.SqlClient` (not from this change), not yet confirmed.
- If the vault cannot be written, saving is refused with an error (nothing half-saved).

## Rollback
Revert commit 2 to return to the "remember checkbox + popup" behaviour, or delete the branch. `credentials.json` can be deleted any time.
