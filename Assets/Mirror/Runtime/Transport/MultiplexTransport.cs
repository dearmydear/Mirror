﻿using System;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Mirror
{
    // a transport that can listen to multiple underlying transport at the same time
    public class MultiplexTransport : Transport
    {
        public Transport[] transports;

        public void Awake()
        {
            if (transports == null || transports.Length == 0)
            {
                Debug.LogError("Multiplex transport requires at least 1 underlying transport");
            }
            InitClient();
            InitServer();
        }

        #region Client
        // clients always pick the first transport
        private void InitClient()
        {
            // wire all the base transports to my events
            foreach (Transport transport in transports)
            {
                transport.OnClientConnected.AddListener(OnClientConnected.Invoke );
                transport.OnClientDataReceived.AddListener(OnClientDataReceived.Invoke);
                transport.OnClientError.AddListener(OnClientError.Invoke );
                transport.OnClientDisconnected.AddListener(OnClientDisconnected.Invoke);
            }
        }

        public override void ClientConnect(string address)
        {
            transports[0].ClientConnect(address);
        }

        public override bool ClientConnected()
        {
            return transports[0].ClientConnected();
        }

        public override void ClientDisconnect()
        {
            transports[0].ClientDisconnect();
        }

        public override bool ClientSend(int channelId, byte[] data)
        {
            return transports[0].ClientSend(channelId, data);
        }

        public override int GetMaxPacketSize(int channelId = 0)
        {
            return transports[0].GetMaxPacketSize(channelId);
        }

        #endregion


        #region Server
        // connection ids get mapped to base transports
        // if we have 3 transports,  then
        // transport 0 will produce connection ids [0, 3, 6, 9, ...]
        // transport 1 will produce connection ids [1, 4, 7, 10, ...]
        // transport 2 will produce connection ids [2, 5, 8, 11, ...]
        private int FromBaseId(int transportId, int connectionId)
        {
            return connectionId * transports.Length + transportId;
        }

        private int ToBaseId(int connectionId)
        {
            return connectionId / transports.Length;
        }

        private int ToTransportId(int connectionId)
        {
            return connectionId % transports.Length;
        }

        void InitServer()
        {
            // wire all the base transports to my events
            for (int i=0; i< transports.Length; i++)
            {
                // this is required for the handlers,  if I use i directly
                // then all the handlers will use the last i
                int locali = i;
                Transport transport = transports[i];

                transport.OnServerConnected.AddListener(baseConnectionId =>
                {
                    OnServerConnected.Invoke(FromBaseId(locali, baseConnectionId));
                });

                transport.OnServerDataReceived.AddListener((baseConnectionId, data) =>
                {
                    OnServerDataReceived.Invoke(FromBaseId(locali, baseConnectionId), data);
                });

                transport.OnServerError.AddListener((baseConnectionId, error) =>
                {
                    OnServerError.Invoke(FromBaseId(locali, baseConnectionId), error);
                });
                transport.OnServerDisconnected.AddListener(baseConnectionId =>
                {
                    OnServerDisconnected.Invoke(FromBaseId(locali, baseConnectionId));
                });
            }
        }


        public override bool ServerActive()
        {
            return transports.All(t => t.ServerActive());
        }


        public override bool GetConnectionInfo(int connectionId, out string address)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);
            return transports[transportId].GetConnectionInfo(baseConnectionId, out address);
        }

        public override bool ServerDisconnect(int connectionId)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);
            return transports[transportId].ServerDisconnect(baseConnectionId);
        }

        public override bool ServerSend(int connectionId, int channelId, byte[] data)
        {
            int baseConnectionId = ToBaseId(connectionId);
            int transportId = ToTransportId(connectionId);
            return transports[transportId].ServerSend(baseConnectionId, channelId, data);
        }

        public override void ServerStart()
        {
            foreach (Transport transport in transports)
            {
                transport.ServerStart();
            }
        }

        public override void ServerStop()
        {
            foreach (Transport transport in transports)
            {
                transport.ServerStop();
            }
        }
        #endregion

        public override void Shutdown()
        {
            foreach (Transport transport in transports)
            {
                transport.Shutdown();
            }
        }

        public override string ToString()
        {
            StringBuilder builder = new StringBuilder();
            foreach (Transport transport in transports)
            {
                builder.AppendLine(transport.ToString());
            }
            return builder.ToString().Trim();
        }
    }
}