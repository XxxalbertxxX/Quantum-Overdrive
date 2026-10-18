# Quantum Overdrive

Quantum Overdrive is a Windows WPF companion for monitoring PC performance and preparing a system for gaming, great alternative to Nvidia statistics. It is designed to be edited and built in Visual Studio Code with the .NET 9 SDK.

## Features

- Live CPU utilization, physical memory use, and CPU/GPU hardware sensor readings where supported by the hardware and drivers.
- A rolling 60-second CPU/GPU usage chart.
- Selectable 1, 2, or 5 second refresh interval.
- Game library shortcuts and direct executable launch.
- Optional Above normal process priority for games launched by the app.
- Reversible Windows power plan controls and a shortcut to Windows Game Mode settings.
- Dark interface; telemetry is read locally and is not uploaded.

Sensor support varies between PCs. CPU temperature and GPU utilization are collected with LibreHardwareMonitor; some devices require launching the app as administrator, and some hardware/driver combinations do not expose these sensors.

The gaming setup switches to the Windows High performance power plan and opens Game Mode settings for the user to review. Restore Balanced to switch back. The app does not overclock hardware or terminate background processes.

## Run in VS Code

1. Install the .NET 9 SDK and the C# Dev Kit extension.
2. Open this folder in VS Code.
3. Run the **Quantum Overdrive** launch configuration, or use **Terminal → Run Build Task**.

You can also build from PowerShell with `dotnet build .\QuantumOverdrive.csproj` and run with `dotnet run --project .\QuantumOverdrive.csproj`.
