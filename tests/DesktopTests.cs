using System;
using System.IO;
using System.Media;
using CodexWatchdog;

internal static class DesktopTests
{
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static int Main()
    {
        try
        {
            byte[] wave = AlarmSound.CreateWave(65);
            using (BinaryReader reader = new BinaryReader(new MemoryStream(wave)))
            {
                Require(new string(reader.ReadChars(4)) == "RIFF", "Wave must be a RIFF container");
                Require(reader.ReadInt32() == wave.Length - 8, "Wave container length must match the payload");
                Require(new string(reader.ReadChars(8)) == "WAVEfmt ", "Wave must contain the PCM format header");
                reader.ReadInt32();
                Require(reader.ReadInt16() == 1 && reader.ReadInt16() == 1, "Alarm must be mono PCM");
                Require(reader.ReadInt32() == 22050, "Alarm sample rate must match the generated samples");
                reader.BaseStream.Position = 40;
                Require(reader.ReadInt32() == wave.Length - 44, "PCM data length must match actual samples");
                int nonzero = 0, zero = 0;
                while (reader.BaseStream.Position < reader.BaseStream.Length) { if (reader.ReadInt16() == 0) zero++; else nonzero++; }
                Require(nonzero > 20000 && zero > 10000, "Alarm cycle needs real tones and rests, rather than one system beep");
            }
            using (SoundPlayer player = new SoundPlayer(new MemoryStream(wave))) player.Load();
            byte[] quiet = AlarmSound.CreateWave(0);
            for (int i = 44; i < quiet.Length; i++) Require(quiet[i] == 0, "Zero-volume wave must be silent");
            using (AlarmSound alarm = new AlarmSound()) { alarm.Stop(); Require(!alarm.IsPlaying, "Stop must be safe before playback"); }
            Console.WriteLine("PASS: real alarm WAV format, tones/rests, native SoundPlayer load, silence and stop");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    }
}
