using UnityEngine;
using UnityEngine.SceneManagement;

public class TimerToSwitchScene : MonoBehaviour
{
    public float DelayTime = 1.0f;
    public string NextScene = "";

    float RemainingTime = 0;
    bool Triggered = false;

    // Start is called before the first frame update
    void Start()
    {
        RemainingTime = DelayTime;
        Triggered = false;
	}

    // Update is called once per frame
    void Update()
    {
        if (Triggered)
            return;

        RemainingTime -= Time.deltaTime;
        if (RemainingTime <= 0)
        {
            Triggered = true;
			SceneManager.LoadScene(NextScene);
		}
	}
}
