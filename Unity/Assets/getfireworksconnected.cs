using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Messaging;

public class getfireworksconnected : MonoBehaviour, INetworkObject, INetworkComponent
{
    public NetworkId Id { get; } = new NetworkId();
    public struct Message
    {
        public TransformMessage transform;
        public Message(Transform transform)
        {
            this.transform = new TransformMessage(transform);
        }
    }
    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        //decoding
        var msg = message.FromJson<Message>();
        //process content of the message
        // shape colour
        transform.localPosition = msg.transform.position;
        transform.localRotation = msg.transform.rotation;
        
    }

    NetworkContext ctx;
    // Start is called before the first frame update
    void Start()
    {
        ctx = NetworkScene.Register(this);
    }

    // Update is called once per frame
    void Update()
    {
        ctx.SendJson(new Message(transform));
    }
}
