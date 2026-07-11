---
---

# Release Process

CVOYA graph releases are tag-triggered and fully automated by
[`.github/workflows/release.yml`](https://github.com/cvoya-com/graph/blob/main/.github/workflows/release.yml). There is
no manual publish step and no CI stamping of version numbers: the `VERSION`
file at the repository root is the single source of truth for the package
version, and the workflow fails loudly if the pushed tag doesn't match it.

## Cutting a release

1. **Update `VERSION`.**

   ```bash
   ./scripts/create-release.sh -v 1.2.3
   # or, for a pre-release:
   ./scripts/create-release.sh -v 1.2.3-alpha
   ```

   This updates `VERSION` (the semantic package version) and `VERSION.ASSEMBLY`
   (a separate numeric `Major.Minor.YYDDD.HHMM` stamp used for the .NET
   `AssemblyVersion`/`FileVersion`, which cannot carry a semver prerelease
   suffix). Use `--dry-run` to preview without writing, and `--commit` to
   update and commit in one step.

2. **Verify locally before tagging** (optional but recommended):

   ```bash
   dotnet build --configuration Release
   dotnet test --configuration Release
   dotnet pack --configuration Release --no-build -o ./artifacts src/Graph/Cvoya.Graph.csproj
   ```

3. **Commit and tag.** The tag must be `v` followed by the exact `VERSION`
   file content.

   ```bash
   git add VERSION VERSION.ASSEMBLY
   git commit -m "chore: release 1.2.3"
   git tag v1.2.3
   git push origin main
   git push origin v1.2.3
   ```

4. **Pushing the tag triggers the release workflow**, which:
   - Verifies the tag matches `VERSION` exactly (fails loudly on any
     mismatch — this is what prevents the double-date-suffix class of bugs;
     the workflow never writes to `VERSION` itself).
   - Runs the full test suite, including the Neo4j provider tests against a
     Neo4j service container (the same pattern as `ci.yml`).
   - Packs all five packages (`Cvoya.Graph`,
     `Cvoya.Graph.Serialization`, `Cvoya.Graph.Neo4j`,
     `Cvoya.Graph.Analyzers`, `Cvoya.Graph.Serialization.CodeGen`).
   - Generates a build provenance attestation for every package
     (`actions/attest-build-provenance`), verifiable with
     `gh attestation verify <file> --repo cvoya-com/graph`.
   - Publishes to nuget.org using **Trusted Publishing** (OIDC) — no API key
     is stored anywhere, long-lived or otherwise.
   - Creates a GitHub Release for the tag with auto-generated release notes.

### Dry runs

Running `release.yml` manually via `workflow_dispatch` (Actions tab → Release
→ Run workflow, on any branch) is always a dry run: it executes the full
build/test/pack/attest pipeline but always skips the NuGet publish and GitHub
Release steps. Use it to validate the pipeline end-to-end — including on a
throwaway branch — without touching nuget.org or creating a release. Real
publishing only ever happens from an actual `v*` tag push.

## NuGet Trusted Publishing — one-time portal setup

Trusted Publishing exchanges a short-lived GitHub OIDC token for a temporary
(~1 hour) nuget.org API key at publish time, so no NuGet API key is ever
stored as a GitHub secret. This must be configured once on nuget.org by an
owner of the `Cvoya.Graph*` package IDs (or, before the first publish,
the account/org that will own them):

1. Sign in to [nuget.org](https://www.nuget.org), open your username menu, and
   choose **Trusted Publishing**.
2. Add a new trusted publishing policy with:
   - **Repository Owner:** `cvoya-com`
   - **Repository:** `graph`
   - **Workflow File:** `release.yml` (the file name only, not the
     `.github/workflows/` path)
   - **Environment:** leave empty (the workflow does not use a GitHub
     Environment)
3. Repeat step 2 for each package owner scope if the five package IDs aren't
   all covered by a single policy (a policy applies to all packages owned by
   the selected owner).
4. The policy is valid for 7 days pending its first successful publish (this
   is a GitHub anti-resurrection safeguard for the repository identity), after
   which it's tied to the repository permanently.
5. Set the `NUGET_TRUSTED_PUBLISHING_USER` **repository variable** (Settings →
   Secrets and variables → Actions → Variables — not a secret, since it's just
   a username) to the nuget.org account or organization name that owns the
   policy. `release.yml` reads it as the `user` input to `NuGet/login@v1`.

No further workflow changes are needed — `release.yml` already requests
`id-token: write` and uses `NuGet/login@v1` to exchange the OIDC token for the
push.

## Design decisions

- **`VERSION` is authoritative; nothing stamps it in CI.** The old, disabled
  `release.yml` produced the mangled
  `1.0.0-alpha.20251014.0.20251014.0` version because a workflow step
  appended a date suffix to an already-suffixed `VERSION`. The new workflow
  only *reads* `VERSION` and *verifies* the tag against it.
- **No `CHANGELOG.md`.** GitHub Releases' auto-generated notes
  (`generate_release_notes: true`, derived from merged PR titles since the
  previous tag) are sufficient for now. Adopting
  [Keep a Changelog](https://keepachangelog.com/) can be a follow-up if
  hand-curated release notes become worth the maintenance cost.
- **No NuGet API key anywhere**, cleartext or secret-stored — Trusted
  Publishing is the only supported publish mechanism.
