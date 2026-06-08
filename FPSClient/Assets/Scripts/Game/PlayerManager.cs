using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PlayerManager : MonoBehaviour {
    public int id;
    public string username;
    
    public void OnApplicationQuit() {
        Client.Instance.Disconnect();
    }
}