using System;
using TMPro;
using UnityEngine;

public class NetworkUIManager : MonoBehaviour {
    public static NetworkUIManager Instance;

    public GameObject connectMenu;
    public TMP_InputField usernameField;
    public TMP_InputField ipField;

    private void Awake() {
        Instance = this;
    }

    public void ConnectToServer() {
        if(NetworkManager.Instance.currentState != NetworkManager.State.disconnected) return;
        
        string ip = ipField.text == "" ? "127.0.0.1" : ipField.text;
        
        NetworkManager.Instance.Connect(ip);
    }

    public void EnableConnectUI() {
        usernameField.interactable = true;
        connectMenu.SetActive(true);
    }

    public void DisableConnectUI() {
        usernameField.interactable = false;
        connectMenu.SetActive(false);
    }
}