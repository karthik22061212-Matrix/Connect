# 00 - Prerequisites & Setup: Connect

**Azure subscription is now active.** Sprint 0 through Sprint 7.4 ran on the local dev stack (see `04-sprint-plan.md`). Sprint 7.5 (Azure Migration) can now begin.

---

## Step 1: Create an Azure Account — ✅ DONE
1. Account created with `karthik22061212@gmail.com`.
2. Subscription confirmed **active** in Azure Portal.
3. **Subscription ID:** `787de81a-4d56-4048-b30a-2414c153e3e1`
4. **Tenant:** Default Directory (`karthik22061212gmail.onmicrosoft.com`), Tenant ID `25c36799-0594-4ac4-9794-82914cbda4fb`
5. Note: this tenant had "Security Defaults" enabled by default, which blocked `az login` until disabled via Microsoft Entra ID → Properties → Manage Security Defaults. Also required personal-account two-step verification setup via https://account.microsoft.com/security before login would succeed.

## Step 2: Create a Firebase Project (for push notifications) — ✅ DONE
Completed during Sprint 6 (push notification implementation).

## Step 3: Create a Git Repository — ✅ DONE
Repo: `https://github.com/karthik22061212-Matrix/Connect.git`, local path `C:\Projects\Connect`.

## Step 4: Install Local Tooling — ✅ DONE
- [x] **Azure CLI** — installed via WinGet (`winget install --id Microsoft.AzureCLI -e`), version 2.89.1, authenticated via `az login --use-device-code`
- [x] **.NET 8 SDK**
- [x] **Flutter SDK**
- [x] **Git**

## Step 5: Provision Azure Resources (Sprint 7.5 — now active)
With the subscription confirmed active and Azure CLI authenticated, resource provisioning proceeds via Azure CLI script, to be generated and reviewed before execution. Will provision:
- Resource Group
- Azure App Service (Linux, F1 Free tier)
- Azure SQL Database (Free tier)
- Azure Static Web App (Free tier)
- Azure B1s VM (for coturn TURN server)

---

## Readiness Checklist (Sprint 0 — coding) — ✅ ALL COMPLETE
- [x] Firebase project created
- [x] Git repository created
- [x] Local tooling installed (.NET 8 SDK, Flutter SDK, Git, Azure CLI)
- [x] Planning docs (`00`–`08`) committed to repo
- [x] Local DB confirmed: **SQL Server LocalDB** (Windows dev environment)

## Readiness Checklist (Sprint 7.5 — Azure migration) — ✅ ALL COMPLETE
- [x] Azure account created, subscription active (`787de81a-4d56-4048-b30a-2414c153e3e1`)
- [x] Azure CLI installed and authenticated (`az account show` confirms active subscription)

**Sprint 7.5 can begin.**