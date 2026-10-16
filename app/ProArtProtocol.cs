using System.Text.Json.Serialization;

namespace GHelper.ProArt;

// This contract deliberately accepts named controls only, never arbitrary ACPI IDs.
public record ChoiceSpec(string kind, string[] values);
public record RangeSpec(string kind, int min, int max, int step, string unit);
public record Spec(object value_type, string permission, bool restart_required, string backend);
public record Control(string id, string category, string name, string current, Spec spec);
public record Metric(string id, string name, double value, string unit);
public record Snapshot(int protocol, string backend, Control[] controls, Metric[] metrics, string[] diagnostics);
public record Setting(string Id, string Category, string Name, uint Device, int Min, int Max,
    string Unit = "", string[]? Choices = null, int[]? Values = null)
{
    public string? Decode(int raw) => Choices is null
        ? raw >= Min && raw <= Max ? raw.ToString(System.Globalization.CultureInfo.InvariantCulture) : null
        : Array.IndexOf(Values!, raw) is var index && index >= 0 ? Choices[index] : null;

    public int Encode(string value)
    {
        if (Choices is not null)
        {
            int index = Array.IndexOf(Choices, value);
            if (index >= 0) return Values![index];
        }
        else if (int.TryParse(value, out int number) && number >= Min && number <= Max)
            return number;
        throw new ArgumentException($"Invalid value for {Id}");
    }
    public Control? Read(int raw)
    {
        var value = Decode(raw);
        return value is null ? null : new(Id, Category, Name, value,
            new(Choices is null ? new RangeSpec("range", Min, Max, 1, Unit)
                : new ChoiceSpec("choices", Choices), "user", false, "G-Helper ASUS ACPI"));
    }
}

public interface IHardware
{
    int Read(uint device);
    int Write(uint device, int value);
}

public static class Protocol
{
    public const int Version = 1;
    public static void Apply(IHardware hardware, Setting setting, string value)
    {
        int target = setting.Encode(value); // Validate before touching hardware.
        int previous = hardware.Read(setting.Device);
        if (setting.Decode(previous) is null) throw new InvalidOperationException("Control unavailable");
        if (target == previous) return;
        try
        {
            if (hardware.Write(setting.Device, target) != 1)
                throw new InvalidOperationException("Firmware rejected the change");
            if (hardware.Read(setting.Device) != target)
                throw new InvalidOperationException("Firmware readback did not match");
        }
        catch (Exception original)
        {
            if (hardware.Write(setting.Device, previous) != 1 || hardware.Read(setting.Device) != previous)
                throw new InvalidOperationException(original.Message + "; restore failed", original);
            throw;
        }
    }
}
