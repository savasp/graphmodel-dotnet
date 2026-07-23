# CVOYA Graph release checklist

The release process is tag-authoritative and is orchestrated by `scripts/release.sh`. The script
requires a clean checkout at the refreshed `origin/main`, runs preflight checks, pushes the tag,
watches `.github/workflows/release.yml`, and verifies every published package on nuget.org.

See [docs/release-process.md](../docs/release-process.md) for the complete design and recovery
procedures.

## Before the release

- [ ] Confirm every release-bounded issue is closed or explicitly moved.
- [ ] Confirm `main` contains the final API, provider certification, documentation, and release
      notes.
- [ ] Confirm the migration guide states the pre-v1 recreate/reimport boundary.
- [ ] Confirm package READMEs and the root package table cover every packable project under `src/`.
- [ ] Confirm the Latest source alias is documented as a movable alias, not an immutable URL:
      `https://github.com/cvoya-com/graph/releases/latest/download/cvoya-graph-source.zip`.
- [ ] Confirm the repository has no uncommitted changes and fetch `origin/main`.

## Local validation

The hosted release workflow reruns the complete build/provider/package pipeline against the tagged
commit. Before cutting the tag, run the relevant final local checks:

```bash
dotnet build --configuration Debug

./scripts/run-tests.sh \
  --configuration Debug \
  --lane fast \
  --disable-diff-engine

./scripts/run-tests.sh \
  --configuration Debug \
  --lane all \
  --neo4j \
  --age \
  --disable-diff-engine

dotnet msbuild eng/PackageValidation.proj -target:Validate
./scripts/build-docs.sh Debug
```

- [ ] The fast lane passes.
- [ ] Neo4j and AGE lanes run serially and pass.
- [ ] Package validation derives and verifies the complete packable set.
- [ ] Documentation builds.
- [ ] Compiling examples build against the final API.
- [ ] Source/XML documentation has no warnings introduced by the release changes.

## Preview the tag

```bash
./scripts/release.sh 1.0.0 --plan
```

For a prerelease:

```bash
./scripts/release.sh 1.0.0 --pre rc --plan
```

- [ ] The computed semantic version and tag are correct.
- [ ] The target commit is the intended `origin/main`.
- [ ] Existing tags are not moved or reused.
- [ ] Stable versus prerelease/Latest behavior is intentional.

## Cut the release

Stable:

```bash
./scripts/release.sh 1.0.0
```

Prerelease:

```bash
./scripts/release.sh 1.0.0 --pre rc
```

Add `--latest` only when that prerelease should become GitHub's Latest release.

The workflow:

1. restores, builds, and runs the complete provider/library suite;
2. derives and packs every packable source project;
3. verifies package inventory and assembly version metadata;
4. publishes NuGet packages through Trusted Publishing;
5. creates and attests `cvoya-graph-source.zip` from the tagged commit;
6. creates the GitHub Release; and
7. publishes documentation from the same immutable tagged artifact.

## Verify publication

- [ ] `scripts/release.sh` reports that every package ID from the release assets resolves
      anonymously on nuget.org at the exact version.
- [ ] The GitHub Release is attached to the intended tag/commit.
- [ ] `cvoya-graph-source.zip` downloads and has the fixed asset name.
- [ ] A specific tagged asset URL remains immutable.
- [ ] If the release should be Latest, the stable alias resolves to this release:

  ```text
  https://github.com/cvoya-com/graph/releases/latest/download/cvoya-graph-source.zip
  ```

- [ ] Published documentation corresponds to the tag, not a later `main` commit.
- [ ] Build provenance attestations exist for packages, symbol packages, and source archive.

## Recovery rules

- Do not recreate or move a published tag.
- A failed/cancelled workflow may be rerun for the same tag when no immutable publication conflict
  exists.
- If a version was partially published and cannot be retried safely, diagnose it and choose a new
  version.
- Manual workflow dispatch is a dry-run package pipeline; it does not publish NuGet packages or
  create a release.
- A documentation deployment can be retried without republishing packages.

## Post-release

- [ ] Confirm the GitHub Release is marked Latest only when intended.
- [ ] Confirm the root/source download wording still distinguishes the movable Latest alias from
      immutable tagged assets and NuGet packages.
- [ ] Confirm the public docs and examples show the identity-free v1 model.
- [ ] Announce any deliberate provider capability differences, especially AGE shortest path being
      unsupported in v1.0.
- [ ] File follow-up issues rather than expanding the completed release scope.
