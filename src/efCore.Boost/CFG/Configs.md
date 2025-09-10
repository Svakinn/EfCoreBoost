# Configuration Guide

Unit of Work (UOW) classes handle database connection resolution internally. All you need to do is assign a unique name to each database connection and define its settings within the `DBConnections` section in `appsettings.json`.

The Unit of Work constructor will automatically look up the connection string and connection type based on the given name.

**Example usage:**

```csharp
_uow = new UOWTxt(config, "Svak");
```

This refers to a configuration entry like:

```json
"DBConnections": {
  "Svak": {
    "ConnectionString": "data source=localhost;initial catalog=Svak2;integrated security=True;TrustServerCertificate=True",
    "UseAzure": false,
    "UseManagedIdentity": "False",
    "AzureTenantId": "",
    "AzureClientId": "",
    "AzureClientSecret": ""
  }
}
```

This document outlines how database connection strings are structured, accessed, and overridden securely in the application. It focuses exclusively on the two supported connection strategies: flat `ConnectionStrings` and structured `DBConnections`, with an emphasis on secure environment variable overrides.

---

## 🔧 Supported Configuration Models

### 1. `ConnectionStrings`

This is a simple key-value dictionary of raw connection strings.

**Example:**

```json
"ConnectionStrings": {
  "MyConnName": "Server=localhost;Database=Svak2;Encrypt=false;Integrated Security=True"
}
```

**Access in code:**

```csharp
var connStr = ConnectionStringCfg.Get(configuration, "MyConnName");
```

**Environment variable override:**

```bash
ConnectionStrings__MyConnName="Server=prod-db;Database=MyApp;User Id=svc;Password=secret;"
```

---

### 2. `DBConnections`

This is a richer structure that supports advanced metadata (e.g., Azure authentication details).

**Example:**

```json
"DBConnections": {
  "MyDatabase": {
    "ConnectionString": "Server=localhost;Database=Svak2;Integrated Security=True",
    "UseAzure": false,
    "UseManagedIdentity": "False",
    "AzureTenantId": "",
    "AzureClientId": "",
    "AzureClientSecret": ""
  }
}
```

**Azure connection:** There are multiple ways to connect to Azure SQL:

- Standard connection string (no Azure token) – set `UseAzure: false`.
- Azure AD Application Authentication – set `UseAzure: true` and provide:
  - `AzureClientId`
  - `AzureTenantId`
  - `AzureClientSecret`
- Managed Identity (MSI) – set both `UseAzure: true` and `UseManagedIdentity: true`. Only `AzureClientId` is required (if using user-assigned identity).

⚠️ Note: Managed Identity only works when running in an Azure-hosted environment (e.g., App Service, Function App, VM, etc.)

**Access in code:**

```csharp
var dbInfo = DbConnectionCFG.Get(configuration, "MyDatabase");
var connStr = dbInfo?.ConnectionString;
```

- Note: The UOW object picks this up automatically; you don’t need to do it manually when using the UOW pattern.

**Environment variable override:**

```bash
DBConnections__MyDatabase__ConnectionString="Server=prod-db;Database=MyApp;User Id=svc;Password=secret;"
```

---

## 💻 Setting Environment Variables

### 🔹 Windows (Dev Machine)

#### Option A — Temporary for session

```powershell
$env:DBConnections__MyDatabase__ConnectionString="..."
```

#### Option B — Persistent (recommended)

```powershell
[Environment]::SetEnvironmentVariable("DBConnections__MyDatabase__ConnectionString", "...", "User")
```

To override a flat connection string:

```powershell
[Environment]::SetEnvironmentVariable("ConnectionStrings__MyConnName", "...", "User")
```

---

### 🔹 Windows Server (IIS / App Pool User)

- ⚠️ If your connection string includes credentials, avoid using virtual identities (like the default IIS App Pool identity).
- Instead, create a dedicated local or domain user account and assign it to your IIS Application Pool.
- Then set the environment variable under that specific user’s context.

Example:

```powershell
[Environment]::SetEnvironmentVariable("DBConnections__MyDatabase__ConnectionString", "...", "User")
```

Or for system-wide (not recommended unless unavoidable):

```powershell
[Environment]::SetEnvironmentVariable("DBConnections__MyDatabase__ConnectionString", "...", "Machine")
```

---

### 🔹 Linux / Bash

#### Temporary session override:

```bash
export DBConnections__MyDatabase__ConnectionString="..."
```

#### Persistent override:

Add to one of the following:

- `~/.bashrc`
- `~/.bash_profile`
- `/etc/environment` (system-wide)

Note: On Linux, web services typically run under managed process supervisors (like `systemd`), where environment variables must be defined per service.

---

### 🔹 Docker

```dockerfile
ENV DBConnections__MyDatabase__ConnectionString="..."
```

Or in `docker-compose.yml`:

```yaml
environment:
  - DBConnections__MyDatabase__ConnectionString=...
  - ConnectionStrings__MyConnName=...
```

---

### 🔹 Kubernetes (K8s)

```yaml
env:
  - name: DBConnections__MyDatabase__ConnectionString
    valueFrom:
      secretKeyRef:
        name: my-secret
        key: mydatabase-conn-string
```

---

## ✅ Best Practices

- Do **not** commit secrets into version-controlled config files.
- Use environment variables at deployment time.
- Set variables per user on Windows (especially with IIS); per process/service on Linux.
- Support for Azure Key Vault is not implemented at this moment, as it is only practical for clean Azure deployments. This can be extended later if needed.
- Support for Windows Credential Store is not implemented either. If you assign a real user to your App Pool, local environment variables provide sufficient security.
- Avoid using system-wide environment variables for sensitive data on production servers.

---

This document serves as the canonical reference for managing connection string configuration securely and flexibly.

