using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// The user's voice recordings: 16-bit mono WAV files in persistentDataPath/Voice ("jump_1.wav", ...), one per slot
    /// and take, loaded on first use. They live beside the save and follow it: starting a new game deletes them all
    /// (DeleteAll), so a slot that opens again starts empty.
    /// </summary>
    public static class VoiceLibrary
    {
        private const string Folder = "Voice";
        private static readonly string[] FileNames = { "jump", "dodge", "perfect_dodge", "death", "now", "stage_clear", "boss_defeat", "title_call" };

        private static readonly Dictionary<int, AudioClip> Clips = new Dictionary<int, AudioClip>();
        private static readonly HashSet<int> Probed = new HashSet<int>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Clips.Clear();
            Probed.Clear();
        }

        private static int Key(VoiceSlot slot, int take) => (int)slot * 16 + take;

        public static string PathOf(VoiceSlot slot, int take) =>
            Path.Combine(Application.persistentDataPath, Folder, $"{FileNames[(int)slot]}_{take + 1}.wav");

        /// <summary>The recording of the slot / take (0-based), or null when there is none (or the file is unreadable).</summary>
        public static AudioClip Get(VoiceSlot slot, int take)
        {
            int key = Key(slot, take);
            if (Clips.TryGetValue(key, out var clip) && clip != null)
                return clip;
            if (!Probed.Add(key))
                return null;
            string path = PathOf(slot, take);
            try
            {
                if (!File.Exists(path))
                    return null;
                if (!TryReadWav(File.ReadAllBytes(path), out float[] samples, out int rate) || samples.Length == 0)
                {
                    Debug.LogWarning($"[Voice] '{path}' is not a readable 16-bit PCM WAV; ignored.");
                    return null;
                }
                clip = MakeClip(slot, take, samples, rate);
                Clips[key] = clip;
                return clip;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Voice] Could not read '{path}': {e.Message}");
                return null;
            }
        }

        public static bool Has(VoiceSlot slot, int take) => Get(slot, take) != null;

        /// <summary>Writes the recording (mono samples, -1..1) and makes it the clip of the slot / take. False when the file could not be written.</summary>
        public static bool Save(VoiceSlot slot, int take, float[] samples, int rate)
        {
            if (samples == null || samples.Length == 0 || rate <= 0)
                return false;
            string path = PathOf(slot, take);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                string temp = path + ".tmp";
                File.WriteAllBytes(temp, ToWav(samples, rate));
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(temp, path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Voice] Could not write '{path}': {e.Message}");
                return false;
            }
            int key = Key(slot, take);
            DestroyClip(key);
            Clips[key] = MakeClip(slot, take, samples, rate);
            Probed.Add(key);
            return true;
        }

        public static void Delete(VoiceSlot slot, int take)
        {
            int key = Key(slot, take);
            DestroyClip(key);
            Probed.Add(key); // known to be empty now
            try
            {
                string path = PathOf(slot, take);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Voice] Could not delete the recording: {e.Message}");
            }
        }

        /// <summary>Deletes every recording (files and loaded clips). Called when a new game replaces the save. Returns the number of files removed.</summary>
        public static int DeleteAll()
        {
            foreach (var clip in Clips.Values)
                if (clip != null)
                    UnityEngine.Object.Destroy(clip);
            Clips.Clear();
            Probed.Clear();
            int removed = 0;
            try
            {
                string folder = Path.Combine(Application.persistentDataPath, Folder);
                if (!Directory.Exists(folder))
                    return 0;
                foreach (string name in FileNames)
                    foreach (string file in Directory.GetFiles(folder, name + "_*.wav*")) // also a leftover ".wav.tmp"
                    {
                        File.Delete(file);
                        removed++;
                    }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Voice] Could not delete all recordings: {e.Message}");
            }
            return removed;
        }

        private static void DestroyClip(int key)
        {
            if (Clips.TryGetValue(key, out var old) && old != null)
                UnityEngine.Object.Destroy(old);
            Clips.Remove(key);
        }

        private static AudioClip MakeClip(VoiceSlot slot, int take, float[] samples, int rate)
        {
            var clip = AudioClip.Create($"Voice_{slot}_{take + 1}", samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ------------------------------------------------------------------ WAV (16-bit PCM, mono)

        private static byte[] ToWav(float[] samples, int rate)
        {
            int dataBytes = samples.Length * 2;
            var bytes = new byte[44 + dataBytes];
            using (var stream = new MemoryStream(bytes))
            using (var w = new BinaryWriter(stream))
            {
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' });
                w.Write(36 + dataBytes);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E' });
                w.Write(new[] { (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16);            // fmt chunk size
                w.Write((short)1);      // PCM
                w.Write((short)1);      // mono
                w.Write(rate);
                w.Write(rate * 2);      // byte rate
                w.Write((short)2);      // block align
                w.Write((short)16);     // bits per sample
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' });
                w.Write(dataBytes);
                for (int i = 0; i < samples.Length; i++)
                    w.Write((short)Mathf.RoundToInt(Mathf.Clamp(samples[i], -1f, 1f) * 32767f));
            }
            return bytes;
        }

        /// <summary>Reads a 16-bit PCM WAV (any channel count; only the first channel is kept).</summary>
        private static bool TryReadWav(byte[] bytes, out float[] samples, out int rate)
        {
            samples = null;
            rate = 0;
            if (bytes == null || bytes.Length < 44 || bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F'
                || bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
                return false;
            int channels = 0, bits = 0, format = 0;
            int at = 12;
            while (at + 8 <= bytes.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(bytes, at, 4);
                int size = BitConverter.ToInt32(bytes, at + 4);
                int body = at + 8;
                if (size < 0 || body + size > bytes.Length)
                    size = bytes.Length - body;
                if (id == "fmt " && size >= 16)
                {
                    format = BitConverter.ToInt16(bytes, body);
                    channels = BitConverter.ToInt16(bytes, body + 2);
                    rate = BitConverter.ToInt32(bytes, body + 4);
                    bits = BitConverter.ToInt16(bytes, body + 14);
                }
                else if (id == "data")
                {
                    if (format != 1 || bits != 16 || channels < 1 || rate <= 0)
                        return false;
                    int frames = size / (2 * channels);
                    samples = new float[frames];
                    for (int i = 0; i < frames; i++)
                        samples[i] = BitConverter.ToInt16(bytes, body + i * 2 * channels) / 32768f;
                    return true;
                }
                at = body + size + (size & 1); // chunks are word-aligned
            }
            return false;
        }
    }
}
