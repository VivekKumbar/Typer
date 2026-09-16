using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

// Plays animation clips DIRECTLY from Inspector slots — no Animator Controller
// needed. Put this on the enemy (next to the Enemy script) and drag in that
// specific enemy's Walk and Death clips. Every enemy prefab can use different
// clips; nothing is shared.
//
// IMPORTANT: use the NON-"_RM" clips. The _RM versions have root motion baked
// in and will fight the Enemy script's movement.
[RequireComponent(typeof(Animator))]
public class EnemyAnimator : MonoBehaviour
{
    [Header("Clips for THIS enemy")]
    public AnimationClip walkClip;
    public AnimationClip deathClip;

    [Header("Playback")]
    [Tooltip("Speed multiplier for the walk animation.")]
    public float walkSpeed = 1f;
    [Tooltip("Blend time when switching to the death animation.")]
    public float deathBlend = 0.1f;

    private Animator animator;
    private PlayableGraph graph;
    private AnimationMixerPlayable mixer;
    private bool dying;

    void Awake()
    {
        animator = GetComponent<Animator>();
        animator.applyRootMotion = false; // we drive movement from the Enemy script
        BuildGraph();
    }

    void BuildGraph()
    {
        graph = PlayableGraph.Create(name + "_AnimGraph");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

        mixer = AnimationMixerPlayable.Create(graph, 2);

        if (walkClip != null)
        {
            var walk = AnimationClipPlayable.Create(graph, walkClip);
            walk.SetSpeed(walkSpeed);
            graph.Connect(walk, 0, mixer, 0);
            mixer.SetInputWeight(0, 1f);
        }

        if (deathClip != null)
        {
            var death = AnimationClipPlayable.Create(graph, deathClip);
            death.SetDuration(deathClip.length);
            graph.Connect(death, 0, mixer, 1);
            mixer.SetInputWeight(1, 0f);
        }

        var output = AnimationPlayableOutput.Create(graph, "Anim", animator);
        output.SetSourcePlayable(mixer);
        graph.Play();
    }

    // Called by the Enemy script when it dies.
    public void PlayDeath()
    {
        if (dying || deathClip == null || !graph.IsValid()) return;
        dying = true;

        // Restart the death clip from the beginning, then crossfade to it
        var death = (AnimationClipPlayable)mixer.GetInput(1);
        death.SetTime(0);
        StartCoroutine(BlendToDeath());
    }

    System.Collections.IEnumerator BlendToDeath()
    {
        float t = 0f;
        while (t < deathBlend && graph.IsValid())
        {
            t += Time.deltaTime;
            float w = Mathf.Clamp01(t / deathBlend);
            mixer.SetInputWeight(0, 1f - w);
            mixer.SetInputWeight(1, w);
            yield return null;
        }
        if (graph.IsValid())
        {
            mixer.SetInputWeight(0, 0f);
            mixer.SetInputWeight(1, 1f);
        }
    }

    // How long the death clip runs — the Enemy script uses this to delay destroy.
    public float DeathLength => deathClip != null ? deathClip.length : 0f;

    void OnDestroy()
    {
        if (graph.IsValid()) graph.Destroy();
    }
}