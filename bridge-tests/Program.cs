using GHelper.ProArt;
var setting = new Setting("test", "CPU", "Power", 1, 5, 100);
void Expect(bool ok) { if (!ok) throw new Exception("Assertion failed"); }
void Reject(Action fn) { try { fn(); } catch (Exception) { return; } throw new Exception("Expected rejection"); }
var fake = new Fake();
Reject(() => Protocol.Apply(fake, setting, "101")); Expect(fake.Writes == 0);
Reject(() => Protocol.Apply(fake, setting, "5; rm -rf")); Expect(fake.Writes == 0);
fake.Value = -1; Reject(() => Protocol.Apply(fake, setting, "20")); Expect(fake.Writes == 0);
fake.Value = 10; Protocol.Apply(fake, setting, "20"); Expect(fake.Value == 20);
fake.Mismatch = true; Reject(() => Protocol.Apply(fake, setting, "30")); Expect(fake.Value == 20);
var vivo = new Setting("mode", "Power", "Mode", 2, 0, 2, Choices: ["balanced", "performance", "quiet"], Values: [0, 2, 1]);
Expect(vivo.Encode("performance") == 2 && vivo.Decode(1) == "quiet" && vivo.Read(-1) is null);
Console.WriteLine("Bridge contract: validation, unavailable hardware, write/readback/restore and Vivo mapping passed");
class Fake : IHardware {
 public int Value = 10; public int Writes; public bool Mismatch;
 public int Read(uint device) => Value;
 public int Write(uint device, int value) { Writes++; Value = Mismatch && value == 30 ? 31 : value; return 1; }
}
