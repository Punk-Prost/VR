using System;
using System.Linq;
using UnityEngine;

public class LegacyCharacterAnimator : MonoBehaviour
{
    public Animation animationSource;
    public float crossFade = 0.15f;

    string idleClip;
    string walkClip;
    string runClip;
    string jumpClip;
    string currentClip;

    void Awake()
    {
        if (animationSource == null)
            animationSource = GetComponentInChildren<Animation>(true);
        ConfigureClips();
    }

    void Start()
    {
        if (!string.IsNullOrEmpty(idleClip))
            Play(idleClip, true, 1f);
    }

    public void ConfigureClips()
    {
        if (animationSource == null) return;
        var names = animationSource.Cast<AnimationState>().Select(s => s.name).ToArray();
        idleClip = Pick(names, new[] { "idle" }, new[] { "death", "sit", "drive" });
        walkClip = Pick(names, new[] { "walk" }, new[] { "back", "left", "right", "crouch" });
        runClip = Pick(names, new[] { "run" }, new[] { "back", "left", "right" });
        if (string.IsNullOrEmpty(runClip))
            runClip = Pick(names, new[] { "jog" }, new[] { "back", "left", "right" });
        jumpClip = Pick(names, new[] { "jump" }, new[] { "land" });

        SetWrap(idleClip, WrapMode.Loop);
        SetWrap(walkClip, WrapMode.Loop);
        SetWrap(runClip, WrapMode.Loop);
        SetWrap(jumpClip, WrapMode.Once);
    }

    static string Pick(string[] names, string[] required, string[] excluded)
    {
        if (names == null || names.Length == 0) return null;
        string best = null;
        int bestScore = int.MinValue;
        foreach (string raw in names)
        {
            string n = raw.ToLowerInvariant();
            bool ok = required.All(r => n.Contains(r));
            if (!ok) continue;
            int score = 0;
            if (n.Contains("loop")) score += 3;
            if (n.Contains("forward") || n.Contains("fwd")) score += 2;
            foreach (string e in excluded)
                if (n.Contains(e)) score -= 10;
            if (score > bestScore)
            {
                bestScore = score;
                best = raw;
            }
        }
        return best;
    }

    void SetWrap(string clip, WrapMode wrap)
    {
        if (string.IsNullOrEmpty(clip) || animationSource == null || animationSource[clip] == null) return;
        animationSource[clip].wrapMode = wrap;
    }

    public void SetMotion(float normalizedSpeed, bool grounded, float verticalVelocity)
    {
        if (animationSource == null) return;

        if (!grounded && !string.IsNullOrEmpty(jumpClip))
        {
            Play(jumpClip, false, 1f);
            return;
        }

        if (normalizedSpeed > 0.68f && !string.IsNullOrEmpty(runClip))
            Play(runClip, true, Mathf.Lerp(0.9f, 1.15f, normalizedSpeed));
        else if (normalizedSpeed > 0.08f && !string.IsNullOrEmpty(walkClip))
            Play(walkClip, true, Mathf.Lerp(0.82f, 1.12f, normalizedSpeed * 1.35f));
        else if (!string.IsNullOrEmpty(idleClip))
            Play(idleClip, true, 1f);
    }

    void Play(string clip, bool looping, float speed)
    {
        if (animationSource == null || string.IsNullOrEmpty(clip) || animationSource[clip] == null) return;
        AnimationState state = animationSource[clip];
        state.speed = speed;
        state.wrapMode = looping ? WrapMode.Loop : WrapMode.Once;

        if (currentClip == clip)
        {
            if (!animationSource.IsPlaying(clip)) animationSource.Play(clip);
            return;
        }

        animationSource.CrossFade(clip, crossFade, PlayMode.StopAll);
        currentClip = clip;
    }
}
