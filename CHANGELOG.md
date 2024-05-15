Changelog
=========
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog][keep-a-changelog], and this project adheres to [Semantic Versioning v2.0.0][semver]. See [the README file][docs.readme] for more details on how it is versioned.

## [1.4.0] - 2025-05-15
### Added
- `Commands.step`: new parameter `condition` that gets transformed into `if` on the corresponding action step.

## [1.3.0] - 2024-05-14
### Added
- `Commands.needs`: support `needs` entry of the job definition.

## [1.2.0] - 2024-03-29
### Changed
- `Actions.createRelease`: update the action to v2, allow overriding the version
- `Actions.uploadArtifacts`: update the action to v4, allow overriding the version

## [1.1.0] - 2024-02-23
### Added
- Better support for execution from `.fsx` files.

  This was technically supported in 1.0.0 already, but from this version, we now support a direct list of arguments from `fsi.CommandLineArgs` as an argument for the `EntryPoint.Process`.

  Thanks to @kant2002 for the contribution!

## [1.0.0] - 2024-02-17
### Added
The initial release of this package. Main features:
- low-level features to set up GitHub action workflows and jobs;
- a set of actions to work with .NET projects, including build, test, and release actions.

[docs.readme]: README.md
[keep-a-changelog]: https://keepachangelog.com/en/1.0.0/
[semver]: https://semver.org/spec/v2.0.0.html

[1.0.0]: https://github.com/ForNeVeR/Generaptor/releases/tag/v1.0.0
[1.1.0]: https://github.com/ForNeVeR/Generaptor/compare/v1.0.0...v1.1.0
[1.2.0]: https://github.com/ForNeVeR/Generaptor/compare/v1.1.0...v1.2.0
[1.3.0]: https://github.com/ForNeVeR/Generaptor/compare/v1.2.0...v1.3.0
[1.4.0]: https://github.com/ForNeVeR/Generaptor/compare/v1.3.0...v1.4.0
[Unreleased]: https://github.com/ForNeVeR/Generaptor/compare/v1.4.0...HEAD
