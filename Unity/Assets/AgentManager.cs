using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Messaging;
using Ubiq.Samples;

public class AgentManager : MonoBehaviour
{
    public GameObject Agent;
    public void agentSpawner()
    {
        NetworkSpawner.Spawn(this, Agent);
    }
    // Start is called before the first frame update
    void Start()
    {
        agentSpawner();
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
