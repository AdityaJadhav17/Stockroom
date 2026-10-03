# Browser E2E tests

Use Playwright for .NET with xUnit. Start with Chromium and the purchase, rejection, and insufficient-stock workflows. Use accessible names and form labels as selectors. Capture traces or screenshots after a failure, and keep generated output outside Git.

The developer must add cases, start the application, and provide an isolated dataset before running E2E tests. Configure the base URL and credentials outside tracked files.

After building the E2E project, install Chromium with the generated script:

```powershell
pwsh tests/e2e/bin/Release/net10.0/playwright.ps1 install chromium
```

On Linux CI, use `install --with-deps chromium`. Browser installation downloads executables; the initial package audit covers NuGet dependencies, not those browser binaries. Revisit the browser version and release advisories when enabling E2E CI.

Run cases after configuring the host:

```powershell
dotnet test tests/e2e/MakerspaceLedger.E2ETests.csproj --configuration Release
```

Current status: dependency scaffold only; no browser cases or installed project browsers.
