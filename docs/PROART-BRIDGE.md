# ProArt bridge fork

This fork preserves G-Helper and adds a JSON adapter for a separate custom desktop interface. Upstream: https://github.com/seerge/g-helper, baseline `dd2dced63fbd6e5abb1a50d88b738f7159f5e71d`. G-Helper and modifications remain GPL-3.0; retain LICENSE when redistributing. This is an unofficial fork, not an ASUS or upstream G-Helper release.

Build the normal Windows application using the upstream instructions. Build the bridge with .NET 10:

```
dotnet run --project bridge-tests/Bridge.Tests.csproj
dotnet publish app/GHelper.csproj -c Release -r win-x64 --self-contained true -p:ProArtBridge=true -p:PublishSingleFile=true -o dist/bridge
```

Commands:

```
ProArtGHelper.exe --protocol-version
ProArtGHelper.exe snapshot
ProArtGHelper.exe set battery.charge_limit 80
```

The bridge uses upstream AsusACPI transport and constants. It does not start the upstream UI, configuration, automation, updater, hardware listeners, or service-stopping logic. Snapshot only queries named interfaces. No arbitrary device IDs are accepted. The bridge constructor skips AppConfig-dependent model overrides; tuning ranges are deliberately capped (CPU 5–100 W, CPU target 75–97 C, GPU boost 5–25 W and target 75–87 C). Firmware may reject values even inside these bounds. An unreadable or out-of-range current value hides the control; support is not inferred from the laptop name.

The JSON version-1 snapshot contains `backend`, `controls`, `metrics`, and `diagnostics`. Control IDs and mode labels are normalized, including the different Vivo/ProArt mode numbering. A successful `set` requires a fresh valid current reading, firmware success, and matching readback. A failed write/readback attempts restoration and reports restoration failures. Mutations are serialized with a local Windows mutex and refused while standalone `GHelper.exe` is running. No elevation, BIOS flash, undervolting driver installation, GPU power-off, or reboot is initiated by this bridge.

Custom fan curves, GPU/MUX switching, hotkeys, display refresh, RGB, undervolting, peripherals and update discovery remain in the normal Windows G-Helper app. They are not exposed by this initial bridge. Linux uses separate kernel adapters; the Windows executable does not run natively on Linux.

The bridge contract tests use a fake hardware transport. CI compiles on Linux and Windows and checks the packaged protocol on Windows without accessing hardware. These checks do not establish ASUS firmware acceptance. On actual hardware, verify snapshot, change, readback and restoration for each exposed control. Exit other hardware managers first.
