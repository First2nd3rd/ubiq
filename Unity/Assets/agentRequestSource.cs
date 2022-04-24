using UnityEngine;
using UnityEngine.Events;

namespace Ubiq.XR
{

    public class agentRequestSource : MonoBehaviour
    {
        [System.Serializable]
        public class RequestEvent : UnityEvent<GameObject> { };

        public RequestEvent OnRequest;

        public void Request(GameObject requester)
        {
            OnRequest.Invoke(requester);
        }
    }
}
