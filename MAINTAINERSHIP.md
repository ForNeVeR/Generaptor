Maintainer Guide
================

Publish a New Version
---------------------
1. Update the `LICENSE.md` file, if required.
2. Prepare a corresponding entry in the `CHANGELOG.md` file (usually by renaming the "Unreleased" section).
3. Set `<Version>` in the `Directory.Build.props` file.
4. Merge the aforementioned changes via a pull request.
5. Check if the NuGet key is still valid (see the **Rotate NuGet Publishing Key** section if it isn't).
6. Push a tag in form of `v<VERSION>`, e.g. `v0.0.1`. GitHub Actions will do the rest (push a NuGet package).

Rotate NuGet Publishing Key
---------------------------
CI relies on NuGet API key being added to the secrets. From time to time, this key requires maintenance: it will become obsolete and will have to be updated.

To update the key:

1. Sign in onto nuget.org.
2. Go to the [API keys][nuget.api-keys] section.
3. Update the existing or create a new key named `generaptor.github` with a permission to **Push only new package versions** and only allowed to publish the following packages:
   - **Generaptor**,
   - **Generaptor.Library**.
4. Paste the generated key to the `NUGET_TOKEN` variable on the [action secrets][github.secrets] section of GitHub settings.

[github.secrets]: https://github.com/ForNeVeR/Generaptor/settings/secrets/actions
[nuget.api-keys]: https://www.nuget.org/account/apikeys
