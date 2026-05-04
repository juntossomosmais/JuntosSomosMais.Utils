# Releasing Libraries

Each package is versioned independently. The git tag is the version — MinVer reads it at build time. Don't edit any version field in `.csproj`.

| Package | Tag prefix |
|---|---|
| `JuntosSomosMais.Utils.GlobalExceptionHandler` | `globalexceptionhandler/v` |
| `JuntosSomosMais.Utils.HealthChecks` | `healthchecks/v` |
| `JuntosSomosMais.Utils.Instrumentation` | `instrumentation/v` |

## Release

```bash
git checkout main && git pull --ff-only
git tag healthchecks/v0.2.0
git push origin healthchecks/v0.2.0
```

Watch the `Publish package` workflow in the Actions tab. To release several packages from the same commit, push several tags. There is no "release all" tag.

## Version rules

[SemVer 2.0](https://semver.org/). Lowercase only. Tag `<prefix>v1.2.3` → NuGet `1.2.3`; tag `<prefix>v1.2.3-rc.1` → pre-release.

## Bad release

NuGet doesn't allow unpublishing — only delisting. Delist the broken version, fix on `main`, tag a new patch. Never re-tag the same version.

## Adding a new package

1. Add the project under `src/` (inherits MinVer from `src/Directory.Build.props`).
2. Set a unique `<MinVerTagPrefix>` in its `.csproj`.
3. Add the tag pattern to `on.push.tags` and a `case` branch in `.github/workflows/publish-package.yml`.
4. Add the row to the table above.
