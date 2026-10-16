using System.Diagnostics;
using System.Text.Json;
using GHelper.ProArt;

namespace GHelper;

/// <summary>One command per process. Does not run G-Helper's UI, automation, services or updater.</summary>
internal static class ProArtBridge
{
    private sealed class Hardware(AsusACPI acpi) : IHardware
    {
        public int Read(uint device) => acpi.DeviceGet(device);
        public int Write(uint device, int value) => acpi.DeviceSet(device, value, null);
    }

    private static List<Setting> Settings(AsusACPI acpi)
    {
        var settings = new List<Setting>();
        if (acpi.IsSupported(AsusACPI.PerformanceMode))
            settings.Add(new("asus.performance", "Power", "ASUS performance mode", AsusACPI.PerformanceMode,
                0, 2, Choices: ["balanced", "performance", "quiet"], Values: [0, 1, 2]));
        else if (acpi.IsSupported(AsusACPI.VivoBookMode))
            settings.Add(new("asus.performance", "Power", "ASUS performance mode", AsusACPI.VivoBookMode,
                0, 2, Choices: ["balanced", "performance", "quiet"], Values: [0, 2, 1]));
        settings.AddRange([
            new("battery.charge_limit", "Battery", "Battery charge limit", AsusACPI.BatteryLimit, 40, 100, "%"),
            new("asus.cpu.sustained", "CPU", "CPU sustained power", AsusACPI.PPT_APUA3, 5, 100, "W"),
            new("asus.cpu.slow", "CPU", "CPU slow boost power", AsusACPI.PPT_APUA0, 5, 100, "W"),
            new("asus.cpu.fast", "CPU", "CPU fast boost power", AsusACPI.PPT_APUC1, 5, 100, "W"),
            new("asus.cpu.temperature", "CPU", "CPU temperature limit", AsusACPI.PPT_TEMP9E, 75, 97, "°C"),
            new("asus.gpu.boost", "GPU", "GPU Dynamic Boost", AsusACPI.PPT_GPUC0, 5, 25, "W"),
            new("asus.gpu.temperature", "GPU", "GPU temperature limit", AsusACPI.PPT_GPUC2, 75, 87, "°C"),
            new("asus.display.overdrive", "Display", "Panel overdrive", AsusACPI.ScreenOverdrive, 0, 1,
                Choices: ["off", "on"], Values: [0, 1]),
            new("asus.boot_sound", "System", "Boot sound", AsusACPI.BootSound, 0, 1,
                Choices: ["off", "on"], Values: [0, 1])
        ]);
        return settings;
    }

    public static int Main(string[] args)
    {
        // Self-test is safe on non-ASUS Windows runners and never opens the driver.
        if (args.SequenceEqual(new[] { "--protocol-version" }))
        {
            Console.WriteLine(JsonSerializer.Serialize(new { protocol = Protocol.Version }));
            return 0;
        }
        AsusACPI? acpi = null;
        try
        {
            if (!(args.SequenceEqual(new[] { "snapshot" }) || args.Length == 3 && args[0] == "set"))
                throw new ArgumentException("Usage: ProArtGHelper snapshot | set <control> <value>");
            using var gate = new Mutex(false, @"Local\ProArtGHelperBridge");
            bool acquired;
            try { acquired = gate.WaitOne(TimeSpan.FromSeconds(2)); }
            catch (AbandonedMutexException) { acquired = true; }
            if (!acquired) throw new InvalidOperationException("Another ASUS operation is in progress");
            try
            {
                acpi = new AsusACPI(false);
                if (!acpi.IsConnected()) throw new InvalidOperationException("ASUS System Control Interface driver unavailable");
                var hardware = new Hardware(acpi);
                var settings = Settings(acpi);
                if (args[0] == "set")
                {
                    if (Process.GetProcessesByName("GHelper").Length > 0)
                        throw new InvalidOperationException("Quit standalone G-Helper before changing controls in ProArt Power");
                    var setting = settings.SingleOrDefault(s => s.Id == args[1])
                        ?? throw new ArgumentException("Unknown or unavailable control");
                    Protocol.Apply(hardware, setting, args[2]);
                }
                var metrics = new List<Metric>();
                foreach (var (id, name, device) in new[] {
                    ("cpu.temperature", "CPU temperature", (uint)AsusACPI.Temp_CPU),
                    ("gpu.temperature", "GPU temperature", (uint)AsusACPI.Temp_GPU) })
                {
                    int value = hardware.Read(device);
                    if (value is > 0 and < 125) metrics.Add(new(id, name, value, "°C"));
                }
                foreach (var (id, name, device) in new[] {
                    ("fan.cpu", "CPU fan", AsusACPI.CPU_Fan), ("fan.gpu", "GPU fan", AsusACPI.GPU_Fan),
                    ("fan.mid", "System fan", AsusACPI.Mid_Fan) })
                {
                    int value = hardware.Read(device);
                    if (value is >= 0 and <= 120) metrics.Add(new(id, name, value * 100, "RPM"));
                }
                var controls = settings.Select(s => s.Read(hardware.Read(s.Device))).OfType<GHelper.ProArt.Control>().ToArray();
                Console.WriteLine(JsonSerializer.Serialize(new Snapshot(Protocol.Version, "G-Helper ASUS ACPI", controls,
                    metrics.ToArray(), ["Hardware acceptance pending on this laptop; only readable controls are offered."])));
            }
            finally { gate.ReleaseMutex(); }
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
        finally { acpi?.Close(); }
    }
}
