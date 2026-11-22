# .NET 10.0 compatibility for Space Engineers

Provides some of the performance benefits of the newer .NET runtime without having to recompile the game.

This repository ships **two** plugins:
- [`ClientPlugin/`](ClientPlugin/) — for the Space Engineers **game client**, loaded by [Pulsar](https://github.com/SpaceGT/Pulsar).
- [`ServerPlugin/`](ServerPlugin/) — for the Space Engineers **Dedicated Server**, loaded by [Magnetar](https://github.com/CometWorks/magnetar).

Common patches, rewriters, and tools used by both plugin assemblies can be found in [`Shared/`](Shared/).

UI / audio / render-only patches are skipped on the server.

## Prerequisites

### Client

- [Space Engineers](https://store.steampowered.com/app/244850/Space_Engineers/)
- [.NET 10.0 Runtime or SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Pulsar](https://github.com/SpaceGT/Pulsar)

### Server

- Space Engineers Dedicated Server
- [.NET 10.0 Runtime or SDK](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- [Magnetar](https://github.com/CometWorks/magnetar)

## How to use

- **Client**: run the `Interim.exe` binary of Pulsar.
- **Server**: run the `Interim.exe` binary of Magnetar.

## Development

Local build paths (`Bin64`, `DS64`, `Pulsar`, `Magnetar`) are empty in `Directory.Build.props`.
To override them, copy its first `PropertyGroup` into `Directory.Build.props.user` (git-ignored)
in the repo root, wrapped in a top-level `<Project>` element, and fill in your paths.

`Bin64` and `DS64` are auto-detected from Steam if left empty.

### Tests

`tests/string-hash-collision` checks the `MyStringHash` collision fix against the game's
`VRage.dll` without starting the game. On .NET 10 vanilla `MyStringHash.GetOrCompute` hangs
forever when a new string's hash belongs to another string (SE-1606). `vanilla` expects that hang
and `patched` expects the fix's `ArgumentException` instead:

```
dotnet run -c Release --project tests/string-hash-collision -- vanilla
dotnet run -c Release --project tests/string-hash-collision -- patched
```

## Credits

- `SpaceGT` for his contribution in fixing a lot of issues with the .NET 10 port, especially mod support. 

## Bug reports

Please start a support thread on the [Pulsar Discord](https://discord.gg/z8ZczP2YZY)

## License

[AGPL v3 only](LICENSE) (`AGPL-3.0-only`).
Your own plugins can stay closed when they use the interfaces we document for
third-party use. See the [plugin exception](EXCEPTION.md) for details.
See the [licensing guide](LICENSING.md) for scope and source requirements.

[Alternative licensing](LICENSING.md#alternative-licensing) is available case by case.
