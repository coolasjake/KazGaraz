using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class Menu : MonoBehaviour
{
    public Button TapToPlay;
    public Button EasyButton;
    public Button HardButton;
    public Image Dim;

    private bool showingButtons = false;

    // Start is called before the first frame update
    void Start()
    {
        EasyButton.onClick.AddListener(Easy);
        HardButton.onClick.AddListener(Hard);

        Dim.gameObject.SetActive(false);
        EasyButton.gameObject.SetActive(false);
        HardButton.gameObject.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if (!showingButtons)
        {
            if (Input.GetMouseButtonDown(0))
                FirstTap();

            Color textCol = Color.white;
            float x = 0.5f + Mathf.PingPong(Time.time, 1) * 0.5f;
            textCol = Color.HSVToRGB(0, 0, x);
          //  TapToPlay.color = textCol;
        }
    }

    public void FirstTap()
    {
        showingButtons = true;

        TapToPlay.gameObject.SetActive(false);
        Dim.gameObject.SetActive(true);
        EasyButton.gameObject.SetActive(true);
        HardButton.gameObject.SetActive(true);
    }

    public void Easy()
    {
        Controller.holdToMoveMode = true;
        SceneManager.LoadScene(1);
    }

    public void Hard()
    {
        Controller.holdToMoveMode = false;
        SceneManager.LoadScene(1);
    }
}
