using System.Collections;
using System.IO;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Networking;

namespace TerrorRadius;

[BepInPlugin("denyscrasav4ik.thedumbfactory.terroradius", "Terror Radius", "1.0.0")]
[BepInProcess("BALDI.exe")]
public class Plugin : BaseUnityPlugin
{
    public static Plugin instance { get; private set; }
    public static Sprite DangerSprite;

    private void Awake()
    {
        Harmony harmony = new Harmony("denyscrasav4ik.thedumbfactory.terroradius");
        instance = this;
        harmony.PatchAll();
        StartCoroutine(LoadingAssets());
    }

    public IEnumerator LoadingAssets()
    {
        string modPath = Path.Combine(Application.streamingAssetsPath, "Modded", "denyscrasav4ik.thedumbfactory.terroradius");
        string midiDir = Path.Combine(modPath, "MIDI");
        string wavDir = Path.Combine(modPath, "WAV");

        if (Directory.Exists(midiDir))
        {
            foreach (string file in Directory.GetFiles(midiDir, "*.mid"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                byte[] data = File.ReadAllBytes(file);
                HideAndSeekPatches.CustomMidiData[fileName] = data;

                string[] parts = fileName.Split(new char[] { '_' });
                if (parts.Length >= 2 && int.TryParse(parts.Last(), out int index) && index >= 1 && index <= 4)
                {
                    string setBaseName = string.Join("_", parts.Take(parts.Length - 1));
                    if (!HideAndSeekPatches.midiLayers.ContainsKey(setBaseName))
                        HideAndSeekPatches.midiLayers[setBaseName] = new string[4];

                    HideAndSeekPatches.midiLayers[setBaseName][index - 1] = fileName;
                }
                else
                {
                    HideAndSeekPatches.midis.Add(fileName);
                }
            }
        }

        if (Directory.Exists(wavDir))
        {
            foreach (string file in Directory.GetFiles(wavDir, "*.wav"))
            {
                string fileName = Path.GetFileNameWithoutExtension(file);
                string url = "file://" + file.Replace("\\", "/");

                using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
                {
                    yield return www.SendWebRequest();
                    if (www.result == UnityWebRequest.Result.Success)
                    {
                        AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                        SoundObject sound = ScriptableObject.CreateInstance<SoundObject>();
                        sound.name = fileName;
                        sound.soundClip = clip;
                        sound.soundType = SoundType.Music;

                        string[] parts = fileName.Split(new char[] { '_' });
                        if (parts.Length >= 2 && int.TryParse(parts.Last(), out int index) && index >= 1 && index <= 4)
                        {
                            string setBaseName = string.Join("_", parts.Take(parts.Length - 1));
                            if (!HideAndSeekPatches.musicLayers.ContainsKey(setBaseName))
                                HideAndSeekPatches.musicLayers[setBaseName] = new SoundObject[4];

                            HideAndSeekPatches.musicLayers[setBaseName][index - 1] = sound;
                        }
                        else
                        {
                            HideAndSeekPatches.musics.Add(sound);
                        }
                    }
                }
            }
            StartCoroutine(LoadDangerMeter());
        }
    }

    private IEnumerator LoadDangerMeter()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "Modded", "denyscrasav4ik.thedumbfactory.terroradius", "Textures", "DangerMeter.png");

        if (File.Exists(path))
        {
            byte[] fileData = File.ReadAllBytes(path);
            Texture2D tex = new Texture2D(2, 2);

            if (tex.LoadImage(fileData))
            {
                tex.filterMode = FilterMode.Point;

                DangerSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }
        yield break;
    }
}
