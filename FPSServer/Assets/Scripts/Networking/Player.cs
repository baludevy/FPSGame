using System.Collections.Generic;
using System.Linq;
using UnityEngine;



public class Player : MonoBehaviour {
    public int id;
    public string username;

    public void Initialize(int id, string username) {
        this.id = id;
        this.username = username;
    }
}