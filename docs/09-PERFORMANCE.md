# RTS-015 performance report

## Workload

The T-015 profile measures a five-minute Release run with 50 active units.
The 100- and 200-unit profiles run for 45 seconds each. Each configuration
uses the release battlefield at the starting camera position and warms up
for five seconds before recording. Both sides start with an 80/20 mix of
infantry squads and tanks. Increased hit points keep the roster stable.
Scripted orders alternate advances and returns every 80 simulation ticks in
place of the orange AI. Orders use normal navigation, while automatic fire,
line of sight, capture, HUD and rendering follow normal gameplay. The benchmark
starts only when given the `--benchmark` argument.

Frame times are intervals between rendered process frames. Simulation times
cover `GameSession.AdvanceOneTick`, including command processing and navigation.
Godot visual updates count toward frame time. The process working set is
sampled once per second and at the start and end. The reported peak includes
managed and native resident memory. The JSON also includes the managed heap
at the end, accepted and rejected move orders, and shots fired.

## Reproduce

After the [pinned toolchain setup](08-SETUP.md), use the matching Godot .NET
export templates with `linux_release.x86_64` installed for the Linux example.

```bash
mkdir -p "$PWD/dist/linux-benchmark"
dotnet build game/IronDominion.Game.csproj --configuration Debug
"$GODOT_BIN" --headless --path game --editor --import
"$GODOT_BIN" --headless --path game --export-release "Linux Benchmark" \
  "$PWD/dist/linux-benchmark/IronDominion.x86_64"
cd dist/linux-benchmark
./IronDominion.x86_64 --resolution 1280x720 --disable-vsync --audio-driver Dummy -- --benchmark 50 300 "$PWD/linux-50.json"
./IronDominion.x86_64 --resolution 1280x720 --disable-vsync --audio-driver Dummy -- --benchmark 100 45 "$PWD/linux-100.json"
./IronDominion.x86_64 --resolution 1280x720 --disable-vsync --audio-driver Dummy -- --benchmark 200 45 "$PWD/linux-200.json"
```

For a graphical Windows profile, extract the Windows x64 package and run this
from that directory in PowerShell. Wait for the window to close and read the
JSON. The 100- and 200-unit profiles use the same arguments as on Linux.

```powershell
Start-Process -FilePath .\IronDominion.exe -Wait -ArgumentList @(
    '--resolution', '1280x720', '--disable-vsync', '--', '--benchmark', '50', '300', 'windows-50.json'
)
```

The Windows workflow runs a shorter headless check. Its JSON leaves rendered
FPS empty because headless execution has no framebuffer to render.

## Graphical Release measurements

The cloud desktop ran Debian 13 x64 with three logical processors visible to
the process on an Intel Xeon Platinum 8573C host. Godot 4.6.3 used X11 and
OpenGL 4.5 Compatibility with Mesa 25.0.7 llvmpipe (LLVM 19.1.7). The
window manager gave the game a 1180 × 812 window after a 1280 × 720 request.
The driver reported that switching VSync mode was unsupported.

| Units | Seconds | Avg. FPS | Frame p50 / p95 / p99 (ms) | Tick p50 / p95 / p99 (ms) | Peak working set (MiB) |
| ---: | ---: | ---: | ---: | ---: | ---: |
| 50 | 300.06 | 12.3 | 78.38 / 119.11 / 177.26 | 0.26 / 0.59 / 1.69 | 446.9 |
| 100 | 45.08 | 10.4 | 92.54 / 123.55 / 184.66 | 0.72 / 1.24 / 2.83 | 374.7 |
| 200 | 45.01 | 8.0 | 121.37 / 155.24 / 179.54 | 2.68 / 3.61 / 7.23 | 397.9 |

| Units | Frames | Ticks | Path requests | Move orders accepted / rejected | Shots | Final managed heap (MiB) |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 50 | 3696 | 6002 | 3800 | 3800 / 0 | 1900 | 59.7 |
| 100 | 471 | 902 | 1100 | 1100 / 0 | 708 | 16.1 |
| 200 | 358 | 901 | 2200 | 2200 / 0 | 1308 | 23.6 |

All three profiles sustained 20 simulation ticks per second. The 50-unit
run averaged 12.3 FPS on software rendering. The 60 FPS target on Windows
hardware remains unmeasured. Peak memory was sampled over different test
lengths, so the three peaks cannot establish memory scaling by themselves.

## Native Windows x64 startup and load check

GitHub Actions validated SHA-256 and the packaged commit on a Windows Server
2025 runner. The packaged executable initialized its bundled .NET runtime,
loaded the Release battlefield and returned exit code 0. Its 50-unit headless
load check processed 160 ticks and 100 path requests over eight seconds with
zero rejected moves and 50 shots. This verifies the distributed runtime and
scene on Windows. Graphical performance was measured separately on Linux.
