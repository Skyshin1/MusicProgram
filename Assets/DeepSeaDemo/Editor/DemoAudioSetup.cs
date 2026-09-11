#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeepSeaDemo.Editor
{
    internal static class DemoAudioSetup
    {
        const string Folder = "Assets/DeepSeaDemo/Audio/Placeholders";
        [MenuItem("Tools/Deep Sea Demo/Audio/Create Missing Placeholder Sounds")]
        public static void Generate()
        {
            Directory.CreateDirectory(Folder);
            foreach (DemoSound sound in Enum.GetValues(typeof(DemoSound)))
            {
                string path = Folder + "/" + sound + ".wav";
                if (!File.Exists(path)) WriteWave(path, sound);
            }
            AssetDatabase.Refresh();
            foreach (DemoSound sound in Enum.GetValues(typeof(DemoSound)))
            {
                var importer = AssetImporter.GetAtPath(Folder + "/" + sound + ".wav") as AudioImporter;
                var settings = importer.defaultSampleSettings;
                if (settings.compressionFormat != AudioCompressionFormat.PCM)
                {
                    settings.compressionFormat = AudioCompressionFormat.PCM;
                    settings.loadType = AudioClipLoadType.DecompressOnLoad;
                    importer.defaultSampleSettings = settings; importer.forceToMono = true; importer.SaveAndReimport();
                }
            }
            const string bankPath = "Assets/DeepSeaDemo/Resources/DeepSeaAudio.asset";
            var bank = AssetDatabase.LoadAssetAtPath<DemoAudioBank>(bankPath);
            if (bank == null)
            {
                bank = ScriptableObject.CreateInstance<DemoAudioBank>();
                var sounds = (DemoSound[])Enum.GetValues(typeof(DemoSound));
                bank.cues = new DemoAudioBank.Cue[sounds.Length];
                for (int i = 0; i < sounds.Length; i++) bank.cues[i] = new DemoAudioBank.Cue { sound = sounds[i] };
                AssetDatabase.CreateAsset(bank,bankPath);
                foreach (var cue in bank.cues)
                {
                    cue.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder + "/" + cue.sound + ".wav");
                    cue.volume = cue.sound == DemoSound.FishSwim ? .07f : cue.sound == DemoSound.RepairLoop ? .16f : .5f;
                    cue.maxDistance = cue.sound == DemoSound.HatchOpen ? 28 : cue.sound == DemoSound.FishSwim ? 7 : 18;
                }
                EditorUtility.SetDirty(bank); AssetDatabase.SaveAssetIfDirty(bank);
            }
            // Existing banks and recordings are intentionally never overwritten.
            File.WriteAllText("Logs/DeepSeaDemo/Audio-Setup.txt", "Audio bank: " + bankPath + "\nGenerated mono 44.1 kHz PCM placeholders; existing files and bank assignments preserved.\n");
        }
        static void WriteWave(string path, DemoSound sound)
        {
            const int rate = 44100;
            float seconds = sound == DemoSound.HatchOpen ? 2.5f : sound == DemoSound.RepairLoop || sound == DemoSound.FishSwim ? 2f : sound == DemoSound.RepairComplete ? .8f : sound == DemoSound.FishFlee || sound == DemoSound.EnemyChase ? .7f : .35f;
            int count = (int)(rate * seconds);
            var data = new float[count]; double phase = 0, lowNoise = 0;
            var random = new System.Random(7301 + (int)sound);
            double peak = 0;
            for (int i = 0; i < count; i++)
            {
                double t = (double)i / rate, u = t / seconds;
                lowNoise += .065 * ((random.NextDouble() * 2 - 1) - lowNoise);
                double env = Math.Pow(Math.Sin(Math.PI * u), .8);
                double hz = 240, value;
                switch (sound)
                {
                    case DemoSound.RepairSuccess: hz = u < .5 ? 660 : 880; break;
                    case DemoSound.RepairComplete: hz = u < .33 ? 440 : u < .66 ? 660 : 880; break;
                    case DemoSound.RepairFailure: hz = 190 - 95 * u; break;
                    case DemoSound.QteStart: hz = 600; break;
                    case DemoSound.RepairLoop: hz = 95 + 8 * Math.Sin(t * Math.PI * 8); break;
                    case DemoSound.FishSwim: hz = 70; break;
                    case DemoSound.FishFlee: hz = 230 - 140 * u; break;
                    case DemoSound.EnemyChase: hz = 120 - 60 * u; break;
                    case DemoSound.EnemyBite: hz = 90 - 45 * u; break;
                    case DemoSound.HatchOpen: hz = 65 + 30 * Math.Sin(u * Math.PI); break;
                }
                phase += 2 * Math.PI * hz / rate;
                if (sound == DemoSound.FishFlee || sound == DemoSound.FishSwim || sound == DemoSound.EnemyChase)
                    value = lowNoise * 2.5 + .12 * Math.Sin(phase);
                else if (sound == DemoSound.HatchOpen || sound == DemoSound.RepairLoop)
                    value = lowNoise + .2 * Math.Sin(phase) + .09 * Math.Sin(phase * 2.03);
                else if (sound == DemoSound.EnemyBite)
                    value = (lowNoise * 3 + .4 * Math.Sin(phase)) * Math.Exp(-u * 4);
                else value = .4 * Math.Sin(phase) + .08 * Math.Sin(phase * 2);
                data[i] = (float)(value * env); peak = Math.Max(peak,Math.Abs(data[i]));
            }
            using var writer = new BinaryWriter(File.Create(path));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + count * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(rate); writer.Write(rate * 2);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(count * 2);
            foreach (float value in data) writer.Write((short)(value / Math.Max(.001,peak) * .65 * short.MaxValue));
        }
    }
}
#endif
