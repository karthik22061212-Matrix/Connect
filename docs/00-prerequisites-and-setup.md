# 00 - Prerequisites & Setup: Connect

**Azure subscription is deferred** — Sprint 0 through Sprint 7 run on a local dev stack (see `04-sprint-plan.md`). Azure is only needed starting Sprint 7.5. Complete Steps 2–4 now; Step 1 can wait.

---

## Step 1: Create an Azure Account (can wait until Sprint 7.5)
1. Go to https://azure.microsoft.com/free/
2. Sign up with a Microsoft account (or create one)
3. You'll need a credit card for verification (not charged unless you exceed free-tier limits)
4. This gives you a 12-month free trial with $200 credit + always-free services (App Service F1, SQL DB, Static Web Apps, etc. — matches everything in `02-tech-stack.md`)
5. Note down your **Subscription ID** once created (Azure Portal → Subscriptions)

## Step 2: Create a Firebase Project (for push notifications)
1. Go to https://console.firebase.google.com/
2. Create a new project (name it "Connect" or similar)
3. Enable **Cloud Messaging (FCM)** for the project
4. Keep this project open — you'll need config keys when wiring up push notifications in Sprint 6

## Step 3: Create a Git Repository
1. Create a new repo on GitHub (or Azure DevOps if you prefer tighter Azure integration)
2. Suggested structure:
   ```
   connect/
     backend/        (ASP.NET Core Web API)
     frontend/        (Flutter app)
     docs/            (these MD files go here)
     infra/           (Azure CLI/Bicep provisioning scripts)
   ```
3. Push these 5 planning docs into `docs/` as the first commit

## Step 4: Install Local Tooling
- [ ] **Azure CLI** — https://learn.microsoft.com/cli/azure/install-azure-cli
- [ ] **.NET 8 SDK** — https://dotnet.microsoft.com/download
- [ ] **Flutter SDK** — https://docs.flutter.dev/get-started/install
- [ ] **Git**

## Step 5: Provision Azure Resources (Sprint 7.5, deferred)
Once your Azure subscription is active (Steps 1–4 done at that point), resource provisioning is scripted via Azure CLI (recommended — easiest, repeatable, and agent-friendly for BMAD/Antigravity to execute). This becomes **Sprint 7.5**, not Sprint 0, and will provision:
- Resource Group
- Azure App Service (Linux, F1 Free tier)
- Azure SQL Database (Free tier)
- Azure Static Web App (Free tier)
- Azure B1s VM (for coturn TURN server)

*(The actual CLI script will be generated once you confirm the subscription is active — no need to wait for it now.)*

---

## Readiness Checklist (to start Sprint 0 — coding — now)
- [ ] Firebase project created
- [ ] Git repository created ✅
- [ ] Local tooling installed (.NET 8 SDK, Flutter SDK, Git — Azure CLI can wait)
- [ ] Planning docs (`00`–`05`) committed to repo
- [ ] Local DB confirmed: **SQL Server LocalDB** (Windows dev environment)

## Readiness Checklist (to start Sprint 7.5 — Azure migration — later)
- [ ] Azure account created, subscription active
- [ ] Azure CLI installed

Once the first checklist is complete, Sprint 0 can begin right away.
