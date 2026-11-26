## Summary

<!-- What changes, and why. Link the related issue for anything beyond a small fix. -->

## Testing

GitHub Actions does not compile C# or run Unity tests. Run the EditMode suite locally before
opening this pull request. See [CONTRIBUTING.md](https://github.com/simpletools-studio/unity-supabase/blob/main/CONTRIBUTING.md#running-the-tests).

- Unity version:
- EditMode result (passed / total):
- Other checks (platform builds, live acceptance, manual steps):

## Checklist

- [ ] `python .github/scripts/validate_package.py` passes.
- [ ] New behavior has a test.
- [ ] New files under `Packages/` are committed with their `.meta` files.
- [ ] User-facing changes have a `CHANGELOG.md` entry.
- [ ] No credentials, `SupabaseSettings.asset`, or generated files are included.
