using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using MidiPlayerTK;
using MEC;
using TMPro;
using UnityEngine.UI;


namespace TerrorRadius;

[HarmonyPatch]
internal class HideAndSeekPatches
{
    public static List<string> midis = new List<string>();
    public static List<SoundObject> musics = new List<SoundObject>();

    public static Dictionary<string, string[]> midiLayers = new Dictionary<string, string[]>();
    public static Dictionary<string, SoundObject[]> musicLayers = new Dictionary<string, SoundObject[]>();
    public static Dictionary<string, byte[]> CustomMidiData = new Dictionary<string, byte[]>();

    [HarmonyPatch(typeof(BaseGameManager), "BeginSpoopMode")]
    [HarmonyPostfix]
    internal static void SpoopModePostfix()
    {
        int seed = Singleton<CoreGameManager>.Instance.Seed() + Singleton<BaseGameManager>.Instance.CurrentLevel;
        System.Random random = new System.Random(seed);

        int totalChoices = midis.Count + musics.Count + midiLayers.Count + musicLayers.Count;
        if (totalChoices == 0) return;

        int choice = random.Next(0, totalChoices);
        GameObject managerObj = Singleton<BaseGameManager>.Instance.gameObject;

        if (choice < midiLayers.Count)
        {
            var set = midiLayers.ElementAt(choice).Value;
            managerObj.AddComponent<ProximityMidiManager>().Setup(set);
            return;
        }
        choice -= midiLayers.Count;

        if (choice < musicLayers.Count)
        {
            var set = musicLayers.ElementAt(choice).Value;
            managerObj.AddComponent<ProximityWavManager>().Setup(set);
            return;
        }
        choice -= musicLayers.Count;

        if (choice < midis.Count)
        {
            Singleton<MusicManager>.Instance.PlayMidi(midis[choice], true);
            return;
        }
        choice -= midis.Count;

        if (choice < musics.Count)
        {
            var audioMan = managerObj.AddComponent<AudioManager>();
            audioMan.audioDevice = managerObj.AddComponent<AudioSource>();
            audioMan.audioDevice.spatialBlend = 0f;
            audioMan.maintainLoop = true;
            audioMan.QueueAudio(musics[choice], true);
            audioMan.SetLoop(true);
        }
    }

    [HarmonyPatch(typeof(MidiFilePlayer), "MPTK_Play", new Type[0])]
    [HarmonyPrefix]
    private static bool MidiPlayPrefix(MidiFilePlayer __instance)
    {
        if (CustomMidiData.TryGetValue(__instance.MPTK_MidiName, out byte[] data))
        {
            if (MidiPlayerGlobal.MPTK_SoundFontLoaded && !__instance.MPTK_IsPlaying)
            {
                __instance.MPTK_InitSynth(16);
                __instance.MPTK_StartSequencerMidi();
                Routine.RunCoroutine(__instance.ThreadCorePlay(data, 0f, 0f).CancelWith(__instance.gameObject), Segment.RealtimeUpdate);
            }
            return false;
        }
        return true;
    }
}

public class ProximityWavManager : MonoBehaviour
{
    private AudioSource[] sources = new AudioSource[4];
    private Baldi baldi;
    private PlayerManager player;

    public void Setup(SoundObject[] layers)
    {
        double startTime = AudioSettings.dspTime + 0.2;
        for (int i = 0; i < 4; i++)
        {
            sources[i] = gameObject.AddComponent<AudioSource>();
            sources[i].clip = layers[i]?.soundClip;
            sources[i].loop = true;
            sources[i].spatialBlend = 0f;
            sources[i].volume = (i == 0) ? 1f : 0f;
            if (sources[i].clip != null) sources[i].PlayScheduled(startTime);
        }
    }

    private void Update()
    {
        if (baldi == null) baldi = FindObjectOfType<Baldi>();
        if (player == null) player = Singleton<CoreGameManager>.Instance.GetPlayer(0);
        if (!baldi || !player) return;

        float dist = Vector3.Distance(baldi.transform.position, player.transform.position);
        float intensity = 1f - Mathf.Clamp01((dist - 40f) / 210f);

        if (HudManagerPatch.dangerText != null)
        {
            if (intensity < 0.2f) HudManagerPatch.dangerText.text = "SAFE";
            else if (intensity < 0.5f) HudManagerPatch.dangerText.text = "CAUTION";
            else if (intensity < 0.8f) HudManagerPatch.dangerText.text = "WARNING";
            else HudManagerPatch.dangerText.text = "RUN";

            Color finalColor;
            if (intensity < 0.33f)
                finalColor = Color.Lerp(Color.green, Color.yellow, intensity / 0.33f);
            else if (intensity < 0.66f)
                finalColor = Color.Lerp(Color.yellow, Color.red, (intensity - 0.33f) / 0.33f);
            else
                finalColor = Color.Lerp(Color.red, new Color(0.4f, 0f, 0f), (intensity - 0.66f) / 0.34f);

            HudManagerPatch.dangerText.color = finalColor;
        }

        sources[0].volume = Mathf.Clamp01(1f - intensity * 3f);
        sources[1].volume = Mathf.Clamp01(1f - Mathf.Abs(intensity - 0.33f) * 3f);
        sources[2].volume = Mathf.Clamp01(1f - Mathf.Abs(intensity - 0.66f) * 3f);
        sources[3].volume = Mathf.Clamp01(intensity * 3f - 2f);
    }
}

public class ProximityMidiManager : MonoBehaviour
{
    private string[] layers;
    private int currentIdx = -1;
    private Baldi baldi;
    private PlayerManager player;
    private MidiFilePlayer mfp;

    public void Setup(string[] layerNames) => layers = layerNames;

    private void Update()
    {
        if (baldi == null) baldi = FindObjectOfType<Baldi>();
        if (player == null) player = Singleton<CoreGameManager>.Instance.GetPlayer(0);
        if (mfp == null) mfp = Singleton<MusicManager>.Instance.GetComponentInChildren<MidiFilePlayer>();
        if (!baldi || !player || !mfp) return;

        float dist = Vector3.Distance(baldi.transform.position, player.transform.position);
        int targetIdx = dist < 70 ? 3 : dist < 120 ? 2 : dist < 200 ? 1 : 0;

        if (HudManagerPatch.dangerText != null)
        {
            float visualIntensity = 1f - Mathf.Clamp01((dist - 40f) / 160f);

            Color finalColor;
            if (visualIntensity < 0.33f)
                finalColor = Color.Lerp(Color.green, Color.yellow, visualIntensity / 0.33f);
            else if (visualIntensity < 0.66f)
                finalColor = Color.Lerp(Color.yellow, Color.red, (visualIntensity - 0.33f) / 0.33f);
            else
                finalColor = Color.Lerp(Color.red, new Color(0.4f, 0f, 0f), (visualIntensity - 0.66f) / 0.34f);

            HudManagerPatch.dangerText.color = finalColor;

            string[] labels = { "SAFE", "CAUTION", "WARNING", "RUN" };
            HudManagerPatch.dangerText.text = labels[targetIdx];
        }

        if (targetIdx != currentIdx && layers[targetIdx] != null)
        {
            long currentTick = mfp.MPTK_TickCurrent;
            currentIdx = targetIdx;
            Singleton<MusicManager>.Instance.PlayMidi(layers[targetIdx], true);
            mfp.MPTK_TickCurrent = currentTick;
        }
    }
}

[HarmonyPatch(typeof(HudManager), "Awake")]
internal class HudManagerPatch
{
    public static TMP_Text dangerText;
    public static Image dangerBackground;

    private static void Postfix(HudManager __instance)
    {
        GameObject dangerContainer = new GameObject("DangerDisplay");
        dangerContainer.transform.SetParent(__instance.transform, false);

        dangerBackground = dangerContainer.AddComponent<Image>();

        if (Plugin.DangerSprite != null)
        {
            dangerBackground.sprite = Plugin.DangerSprite;
            dangerBackground.rectTransform.sizeDelta = new Vector2(Plugin.DangerSprite.rect.width * 0.5f, Plugin.DangerSprite.rect.height * 0.5f);
        }
        else
        {
            dangerBackground.color = new Color(0, 0, 0, 0.5f);
            dangerBackground.rectTransform.sizeDelta = new Vector2(100, 25);
        }
        dangerBackground.rectTransform.anchoredPosition = new Vector2(0, 140);

        GameObject textObj = new GameObject("DangerText");
        textObj.transform.SetParent(dangerContainer.transform, false);
        dangerText = textObj.AddComponent<TextMeshProUGUI>();
        dangerText.fontSize = 18;
        dangerText.alignment = TextAlignmentOptions.Center;
        dangerText.text = "SAFE";
        dangerText.color = Color.green;

        dangerText.rectTransform.sizeDelta = dangerBackground.rectTransform.sizeDelta;
    }
}
