# TODO Security Multitenancy

## Pending

- Move `TenantSecrets:MasterKey` out of appsettings to environment variables or a cloud secret manager.
- Isolate key material per environment (Dev, QA, Prod) and avoid shared keys across long-lived environments.
- Implement key rotation policy with overlap window and active/inactive key states.
- Implement bulk re-encryption process for `EncryptedConnection` when rotating keys.
- Add security audit trail for tenant secret encryption/decryption operations and key changes.
