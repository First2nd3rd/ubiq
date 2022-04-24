using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Messaging;
using Ubiq.Samples;

public class getConnected : MonoBehaviour, INetworkObject, INetworkComponent
{
    public NetworkId Id { get; } = new NetworkId();

    //Message with colour, location and orientation
    public struct Message
    {
        public TransformMessage transform;
        public Color colour;

        public Message(Transform transform, Color colour)
        {
            this.transform = new TransformMessage(transform);
            this.colour = colour;
        }
    }

    NetworkContext ctx;


    public GameObject Prefab;
    public void SpawnBasketball()
    {
        NetworkSpawner.Spawn(this, Prefab);
    }


    private Material objectMaterial;
    public Material ObjectMaterial
    {
        get 
        {
            return objectMaterial != null ? objectMaterial : GetComponent<Renderer>().material;
        } 
    }
    // Start is called before the first frame update
    void Start()
    {
        ctx = NetworkScene.Register(this);
        SpawnBasketball();
    }



    
    // Update is called once per frame
    void Update()
    {
        ctx.SendJson(new Message(transform, ObjectMaterial.color));
        
    }

    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        //decoding from Json
        var msg = message.FromJson<Message>();
        //content of the message 
        transform.localPosition = msg.transform.position;
        transform.localRotation = msg.transform.rotation;
        ObjectMaterial.color = msg.colour;
    }
}
