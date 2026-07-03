using UnityEngine;
using UnityEngine.Splines;

public class ForestWalkController : MonoBehaviour
{
    [SerializeField] private SplineAnimate splineAnimate;
    [SerializeField] private bool playOnStart = true;

    void Awake()
    {
        if (splineAnimate == null)
            splineAnimate = FindFirstObjectByType<SplineAnimate>();
    }

    void Start()
    {
        if (playOnStart && splineAnimate != null)
            splineAnimate.Play();
    }

    public void StartWalk()
    {
        if (splineAnimate == null)
            return;

        splineAnimate.Restart(true);
    }

    public void PauseWalk()
    {
        if (splineAnimate != null)
            splineAnimate.Pause();
    }

    public void ResumeWalk()
    {
        if (splineAnimate != null)
            splineAnimate.Play();
    }
}
