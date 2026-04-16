# Credential Security Note

This file documents what credential-related data is encrypted and what is not.

## Encrypted At Rest

- Credential secret values in the credential database file, including:
  - Passwords
  - IMAP passwords
  - TOTP secrets
  - API/bearer tokens
  - Certificate passwords
- The credential database content payload as a whole is encrypted using AES-256-CBC.

## Not Encrypted At Rest

- Credential names
- Credential type metadata (for example WebAuthKind)
- Any non-secret display metadata stored outside the encrypted credential database

## Master Password Behavior

- A master password is required to unlock the credential store.
- The master password is requested once per app session before a run that needs credentials.
- If the wrong password is provided, unlock fails and credentials remain inaccessible.

## Forgotten Master Password

- There is no recovery path for encrypted credentials without the correct master password.
- If the master password is forgotten, the practical reset path is:
  - Close the app.
  - Delete the credential database file.
  - Recreate credentials with a new master password.
- Deleting the database permanently removes all saved credentials.

## Logging And Exports

- Secret values must never be logged.
- Flows should reference credential id/name only, never raw secret values.
