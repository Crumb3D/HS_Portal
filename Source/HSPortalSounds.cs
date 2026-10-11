using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public static class HSPortalSounds
{
    public const string Blue = "hsportalGunBlue";
    public const string Orange = "hsportalGunOrange";

    static AudioClip blueClip;
    static AudioClip orangeClip;
    static bool loading;

    public static IEnumerator LoadAll()
    {
        if (loading) yield break;
        loading = true;
        yield return Load("portalgun_shoot_blue1", c => blueClip = c);
        yield return Load("portalgun_shoot_orange1", c => orangeClip = c);
        loading = false;
    }

    public static void PlayShot(Entity player, bool orange)
    {
        if (player == null) return;
        var name = orange ? Orange : Blue;
        var clip = orange ? orangeClip : blueClip;
        try
        {
            if (HSPortalNet.IsRemoteClient)
            {
                var heard = Audio.Manager.Play(player, name);
                Audio.Manager.BroadcastPlay(player, name);
                if (heard == null) PlayClip(player, clip);
                return;
            }
            Audio.Manager.BroadcastPlay(player, name);
        }
        catch
        {
            PlayClip(player, clip);
        }
    }

    static void PlayClip(Entity player, AudioClip clip)
    {
        if (clip == null || player == null || GameManager.IsDedicatedServer) return;
        try
        {
            var go = player.gameObject;
            if (go == null) return;
            var src = go.GetComponent<AudioSource>();
            if (src == null) src = go.AddComponent<AudioSource>();
            src.PlayOneShot(clip, 1f);
        }
        catch (Exception e)
        {
            HSPortalDebug.Warn("Shot clip failed: " + e.Message);
        }
    }

    static IEnumerator Load(string baseName, Action<AudioClip> set)
    {
        var dir = Path.Combine(HSPortalMod.ModPath ?? ".", "Sounds");
        var path = Path.Combine(dir, baseName + ".wav");
        if (!File.Exists(path))
            path = Path.Combine(HSPortalMod.ModPath ?? ".", "assets", "sounds", baseName + ".wav");
        if (!File.Exists(path)) yield break;
        var req = UnityWebRequestMultimedia.GetAudioClip(new Uri(path), AudioType.WAV);
        yield return req.SendWebRequest();
        try
        {
            if (req.result != UnityWebRequest.Result.Success)
            {
                HSPortalDebug.Warn("Could not load " + path + ": " + req.error);
                yield break;
            }
            var clip = DownloadHandlerAudioClip.GetContent(req);
            if (clip == null) yield break;
            clip.name = "HSPortal_" + baseName;
            set(clip);
            HSPortalDebug.Info("Loaded " + Path.GetFileName(path) + " (" + clip.length.ToString("0.0") + "s)");
        }
        finally
        {
            req.Dispose();
        }
    }
}
