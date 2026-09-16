using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class RallyRaceFlow : MonoBehaviour
{
    [SerializeField] RallyCheckpointManager checkpointManager;
    [SerializeField, Min(0f)] float resultsDelay = 1.25f;

    bool loadingResults;

    public void Configure(RallyCheckpointManager manager) => checkpointManager = manager;

    void Awake()
    {
        if (checkpointManager == null)
            checkpointManager = FindAnyObjectByType<RallyCheckpointManager>();
    }

    void Update()
    {
        if (!loadingResults && checkpointManager != null && checkpointManager.IsFinished)
        {
            loadingResults = true;
            RallyGameSession.RecordResult(checkpointManager.ElapsedTime);
            StartCoroutine(OpenResults());
        }
    }

    IEnumerator OpenResults()
    {
        yield return new WaitForSecondsRealtime(resultsDelay);
        SceneManager.LoadScene(RallyGameSession.ResultsScene);
    }
}
