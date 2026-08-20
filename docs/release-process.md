# Release process

DPIFlow uses the root `VERSION` file as the single release version source.

1. Develop on a feature branch and open a pull request.
2. CI must build `DPIFlow.sln` on `windows-latest` and verify `DPIFlow.exe` exists.
3. Merge only after CI succeeds.
4. On `main`, the Release workflow reads `VERSION`.
5. If that release does not exist yet, the workflow builds again and publishes:
   - `DPIFlow.exe`
   - `DPIFlow.exe.sha256`
   - `DPIFlow-Browser-Companion.zip`
6. If the release already exists, the workflow exits without publishing a duplicate.

Before a future release, update `VERSION` in the release-bound pull request.
