# Publish BlueBox through GitHub Actions

This deploys the reviewed `main` application to the existing Azure App Service website. A local Visual Studio installation or .NET SDK is not needed. GitHub installs the SDK from `global.json`; Azure still needs its built-in **.NET 10** runtime. Keep the existing **F1 Free** plan and Azure SQL free offer.

**Status:** workflow prepared; Azure authentication, the first live deployment, and the browser walkthrough are not verified. Website: [BlueBox on Azure](https://bluebox-ims-portfolio-hkc4ckdjhtd5a2gk.westus3-01.azurewebsites.net), awaiting launch.

## 1. Merge the workflow PR

Review the changes and merge the PR into `main`. Wait for **Build and test** to finish successfully on that main commit, including the disposable SQL Server job. The deployment workflow is manual and refuses to deploy a branch other than main or a main revision whose latest push CI run has not succeeded. Merging a PR does not publish the website.

## 2. Add the existing Azure app name

1. In Azure Portal, open the existing **Web App → Overview** and copy its **Name**. This is the resource name, not the full default domain. The intended app is `bluebox-ims-portfolio`; use the exact Name shown in your portal.
2. Open the GitHub repository → **Settings → Secrets and variables → Actions → Variables**.
3. Select **New repository variable**.
4. Name it **`AZURE_WEBAPP_NAME`** and use that Azure resource Name as the value.

The workflow deploys to the existing app. It does not create an App Service plan, database, or other Azure resource.

## 3. Add the private publish profile

1. In Azure, open that same Web App and select **Download publish profile** (sometimes **Get publish profile**) on Overview's toolbar.
2. If Azure reports that basic authentication is disabled, open **Settings → Configuration → General settings** and enable **SCM Basic Auth Publishing Credentials** for this app, if permitted by the subscription. Save, then download again. This authentication is required for the publish-profile method. FTP Basic Auth does not need to be enabled.
3. Open the downloaded `.PublishSettings` file in a text editor. Copy the **entire XML contents**.
4. In GitHub → **Settings → Secrets and variables → Actions → Secrets**, select **New repository secret**.
5. Name it **`AZURE_WEBAPP_PUBLISH_PROFILE`**, paste the XML into the secret value, and save.

Keep the downloaded profile private and outside the project folder. Do not upload it to GitHub's Files page, paste it into chat, or put it in the app's configuration. It contains deployment credentials, not a database password. The profile and app-name variable must refer to the same production web app. If subscription policy blocks publish profiles, stop this authentication setup and use an approved federated Azure identity instead; this workflow does not configure that alternative.

The application settings already entered in Azure remain there: `ConnectionStrings__BlueBox`, `Omdb__ApiKey`, `AllowedHosts`, and `ASPNETCORE_ENVIRONMENT`. Do not copy SQL credentials or the OMDb key into the workflow. Confirm the App Service outbound IP firewall rules remain in place. See [Azure setup](azure-portfolio-setup.md) for the complete host/database steps.

## 4. Run deployment

1. Open the repository's **Actions** tab.
2. Select **Deploy BlueBox to Azure**.
3. Select **Run workflow**, choose **main**, and confirm **Run workflow**.
4. Wait for both **build** and **deploy** to succeed. The `azure-portfolio` GitHub environment records deployments to the existing website; it does not create an Azure resource or require a paid hosting tier.
5. Open the website above and verify the BlueBox sign-in page.

GitHub checks the successful CI run for the selected commit, checks out that exact commit, restores locked packages, runs local tests without a SQL connection, and publishes Release output. It packages DLLs, static assets, and IIS `web.config` at the ZIP root. `UseAppHost=false` avoids publishing a Linux executable to the planned Windows host; Azure runs the portable application using its .NET 10 runtime. Build artifacts expire after one day. Concurrent deployments queue instead of canceling an upload midway.

Full SQL Server verification comes from the separate successful CI run on the same commit, not the local test step inside deployment. Those tests create/drop only disposable test databases. No SQL test, migration, or setup command runs against Azure during deployment.

## 5. Use the account-only starter data

The agreed portfolio setup is **one active manager, four active staff accounts, and two warehouses in Fayetteville, AR and Bentonville, AR**. Run the separately provided private starter-data SQL once in the intended Azure database if it has not already been run. Its login hashes and the separate initial-password file stay outside GitHub.

**Do not run `--seed-demo` for this portfolio setup.** That older class/test command also creates catalog items, inventory balances, and movement history. Leave those three tables empty; the owner will add items and record stock through the deployed website. Deployment itself creates no accounts or data and does not rerun setup on future releases.

Sign in using the private starter-login file. Keep the manager password private; publish only intended staff demo access after the [live walkthrough](demo-guide.md). No public registration is provided.

## Troubleshooting

- **Workflow missing:** the deployment YAML must be merged into the default branch before its Run workflow button appears. Refresh Actions after merging.
- **Main/CI gate fails:** choose main and wait for its latest **Build and test** push run to pass. A PR-only pass or an older main commit's pass is insufficient. Then start a new deployment.
- **App-name check fails:** add `AZURE_WEBAPP_NAME` under repository **Variables**, using the Azure resource Name without a URL or spaces.
- **Missing credential or deployment authentication failure:** check the repository **Secret** name, full XML, matching production app, and SCM publishing authentication. If credentials were reset in Azure, replace the secret with a newly downloaded profile. Never paste the XML into an issue or log.
- **Deployment succeeds but Azure still shows its default page:** confirm the app-name variable/profile target the intended web app and inspect its deployment logs. The ZIP must contain application files at its root, not inside a `publish` directory.
- **500.31/500.32 or runtime startup failure:** confirm Azure's .NET 10 runtime/hosting configuration and bitness; this workflow does not change the host's runtime settings.
- **503/temporarily unavailable:** check private Azure filesystem logs, SQL firewall/settings, and free-tier resume/usage limits. Do not rerun schema installation or delete records to troubleshoot connectivity.

After a working launch, update the README's launch status and add fake-data screenshots. For later releases, merge the reviewed PR, wait for its main CI run, then run this same manual workflow again.
