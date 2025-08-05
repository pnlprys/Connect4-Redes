using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public void EscolherVermelho()
    {
        PlayerPreferences.Instance.SetPlayerAsRed();
        SceneManager.LoadScene("Gameplay");
    }

    public void EscolherVerde()
    {
        PlayerPreferences.Instance.SetPlayerAsGreen();
        SceneManager.LoadScene("Gameplay");
    }
}