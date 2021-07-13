﻿using System.Collections.Generic;
using System;
using System.Linq;
using Ubiq.Rooms;
using Ubiq.Messaging;
using Ubiq.Dictionaries;
using Ubiq.Voip;
using UnityEngine;
using UnityEngine.Events;

public interface ILayer
{
    void SetObjectLayer(int layer);
}

namespace Ubiq.Avatars
{
    /// <summary>
    /// The AvatarManager creates and maintains Avatars for the local player and remote peers.
    /// AvatarManager operates using the RoomClient to create Avatar instances for all remote peers, though
    /// Avatars may be created outside of AvatarManager and maintained another way.
    /// </summary>
    [NetworkComponentId(typeof(AvatarManager), 2)]
    public class AvatarManager : MonoBehaviour
    {
        public AvatarCatalogue AvatarCatalogue;
        public string LocalPrefabUuid;

        private NetworkScene scene;

        /// <summary>
        /// The current avatar loaded for the local player. Be aware that this reference may change at any time
        /// if the underlying Prefab changes. Use the CreateLocalAvatar method to change the model (prefab).
        /// </summary>
        public Avatar LocalAvatar { get; private set; }

        /// <summary>
        /// The Id for the NetworkObject representing this players avatar in the peer group.
        /// </summary>
        private NetworkId localAvatarId;

        private Dictionary<string, Avatar> playerAvatars;

        public RoomClient RoomClient { get; private set; }

        public IEnumerable<Avatar> Avatars
        {
            get
            {
                return playerAvatars.Values;
            }
        }      

        public class AvatarDestroyEvent : UnityEvent<Avatar>
        {
        }

        public class AvatarCreatedEvent : ListEvent<Avatar>
        {
        }

        /// <summary>
        /// Emitted after an Avatar is created for the first time.
        /// </summary>
        /// <remarks>
        /// This event may be emitted multiple times per peer, if the prefab changes and a Avatar/GameObject needs to be created.
        /// </remarks>
        public AvatarCreatedEvent OnAvatarCreated;

        /// <summary>
        /// Emitted just before an Avatar is destroyed
        /// </summary>
        public AvatarDestroyEvent OnAvatarDestroyed;

        /// <summary>
        /// Wraps a PeerInfo object in a persistent PeerInterface
        /// </summary>
        public class PeerInterfaceWrapper : PeerInterface
        {
            PeerInfo info;

            public PeerInterfaceWrapper(PeerInfo info):base(info.UUID)
            {
                UpdatePeerInfo(info);
            }

            public void UpdatePeerInfo(PeerInfo info)
            {
                this.info = info;
            }

            public override string this[string key]
            {
                get
                {
                    return info[key];
                }
                set
                {
                    Debug.LogError($"Trying to set key {key} on read-only remote peer");
                }
            }

            public override PeerInfo GetPeerInfo()
            {
                return info;
            }
        }


        private void Awake()
        {
            RoomClient = GetComponentInParent<RoomClient>();
            playerAvatars = new Dictionary<string, Avatar>();

            if(OnAvatarCreated == null)
            {
                OnAvatarCreated = new AvatarCreatedEvent();
            }
            OnAvatarCreated.SetList(playerAvatars.Values);

            if(OnAvatarDestroyed == null)
            {
                OnAvatarDestroyed = new AvatarDestroyEvent();
            }

            scene = NetworkScene.FindNetworkScene(this);
        }

        private void Start()
        {
            localAvatarId = NetworkScene.GenerateUniqueId(); 

            RoomClient.OnPeer.AddListener(OnPeerUpdated);
            RoomClient.OnPeerRemoved.AddListener(OnPeerRemoved);
            RoomClient.OnJoinedRoom.AddListener(OnJoinedRoom);

            RoomClient.Me["ubiq.avatar.networkid"] = localAvatarId.ToString();

            // The default prefab for the player has been defined; create the avatar with this prefab
            if (LocalPrefabUuid.Length > 0)
            {
                RoomClient.Me["ubiq.avatar.prefab"] = LocalPrefabUuid;
            }

            OnPeerUpdated(RoomClient.Me.GetPeerInfo());
        }

        /// <summary>
        /// Creates a local Avatar for this peer based on the supplied prefab.
        /// </summary>
        public void CreateLocalAvatar(GameObject prefab)
        {
            RoomClient.Me["ubiq.avatar.prefab"] = prefab.GetComponent<Avatar>().PrefabUuid;
        }

        private void UpdateAvatar(PeerInfo peer)
        {
            // Gather some basic parameters about the avatar & peer we are updating

            var local = peer.UUID == RoomClient.Me.UUID;
            var prefabUuid = peer["ubiq.avatar.prefab"];
            var id = new NetworkId(peer["ubiq.avatar.networkid"]);

            // If we have an existing instance, but it is the wrong prefab, destroy it so we can start again

            if (playerAvatars.ContainsKey(peer.UUID))
            {
                var existing = playerAvatars[peer.UUID];
                if (existing.PrefabUuid != prefabUuid)
                {
                    OnAvatarDestroyed.Invoke(existing);
                    Destroy(existing.gameObject);
                    playerAvatars.Remove(peer.UUID);
                }
            }

            // Avatars require a valid id and a prefab. If either of these are missing, it means the remote player does not want an avatar.

            if (!id.Valid)
            {
                return; 
            }

            if (String.IsNullOrWhiteSpace(prefabUuid))
            {
                return;
            }

            // Create an instance of the correct prefab for this avatar

            if (!playerAvatars.ContainsKey(peer.UUID))
            {
                var prefab = AvatarCatalogue.GetPrefab(prefabUuid);
                var created = Instantiate(prefab, transform).GetComponentInChildren<Avatar>();
                created.Id = id;
                
                if(local)
                {
                    created.SetPeer(RoomClient.Me);
                }
                else
                {
                    created.SetPeer(new PeerInterfaceWrapper(peer));
                }

                playerAvatars.Add(peer.UUID, created);

                if (local)
                {
                    if (LocalAvatar != null) // If we are changing the Avatar the LocalAvatar will not be destroyed until next frame so we can still get its transform.
                    {
                        created.transform.localPosition = LocalAvatar.transform.localPosition;
                        created.transform.localRotation = LocalAvatar.transform.localRotation;
                    }
                    LocalAvatar = created;
                }

                OnAvatarCreated.Invoke(created);
            }

            // Update the avatar instance

            var avatar = playerAvatars[peer.UUID];

            avatar.IsLocal = local;
            if (local)
            {
                avatar.gameObject.name = "My Avatar #" + avatar.Id.ToString();
            }
            else
            {
                avatar.gameObject.name = "Remote Avatar #" + avatar.Id.ToString();
            }

            if(avatar.Peer is PeerInterfaceWrapper)
            {
                (avatar.Peer as PeerInterfaceWrapper).UpdatePeerInfo(peer);
            }

            avatar.OnPeerUpdated.Invoke(avatar.Peer.GetPeerInfo());

            IfRecordingSetLayer(avatar, 0); // default (need this to generate a message that can be replayed later)

        }

        private void IfRecordingSetLayer(Avatar avatar, int layer)
        {
            if (scene.recorder != null && scene.recorder.IsRecording())
            {
                ILayer layerer = avatar.gameObject.GetComponentsInChildren<MonoBehaviour>().Where(mb => mb is ILayer).FirstOrDefault() as ILayer;
                layerer.SetObjectLayer(layer); 
            }
        }

        private void OnJoinedRoom(RoomInfo room)
        {
            foreach (var item in RoomClient.Peers)
            {
                OnPeerUpdated(item);
            }
        }

        private void OnPeerUpdated(PeerInfo peer)
        {
            UpdateAvatar(peer);
        }

        private void OnPeerRemoved(PeerInfo peer)
        {
            if (playerAvatars.ContainsKey(peer.UUID))
            {
                IfRecordingSetLayer(playerAvatars[peer.UUID], 8); // generate "hide" message for replay

                Destroy(playerAvatars[peer.UUID].gameObject);
                playerAvatars.Remove(peer.UUID);
            }
        }

        /// <summary>
        /// Find the AvatarManager for forest the Component is a member of. May return null if there is no AvatarManager for the scene.
        /// </summary>
        public static AvatarManager Find(MonoBehaviour Component)
        {
            try
            {
                return NetworkScene.FindNetworkScene(Component).GetComponentInChildren<AvatarManager>();
            }
            catch
            {
                return null;
            }
        }
    }

}