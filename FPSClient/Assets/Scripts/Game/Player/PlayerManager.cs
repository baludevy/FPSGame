using UnityEngine;

public class PlayerManager : MonoBehaviour {
    public int id;
    public string username;

    public void OnApplicationQuit() {
        Client.Instance.Disconnect();
    }
}