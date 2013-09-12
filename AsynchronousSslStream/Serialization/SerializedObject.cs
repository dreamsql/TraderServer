﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Trader.Common;
using Trader.Server.Ssl;
using Trader.Server.SessionNamespace;

namespace Trader.Server.Serialization
{
    public class SerializedObject
    {
        public SerializedObject(Session session, string clientInvokeId, XElement content)
        {
            this.Session = session;
            this.ClientInvokeID = clientInvokeId;
            this.Content = content;
        }
        private SerializedObject() { }

        public static SerializedObject Create(Session session, bool isKeepAlive, byte[] keepAlivePacket)
        {
            SerializedObject target = new SerializedObject();
            target.IsKeepAlive = isKeepAlive;
            target.KeepAlivePacket = keepAlivePacket;
            target.Session = session;
            return target;
        }
        public XElement Content { get;  set; }
        public UnmanagedMemory  ContentInPointer { get; set; }
        public string ClientInvokeID { get; private set; }
        public Session ClientID { get; set; }
        public bool IsKeepAlive { get; private set; }
        public bool IsKeepAliveSuccess { get;set; }
        public byte[] KeepAlivePacket { get; private set; }
        public Session Session { get; set; }

        public Client Sender { get; set; }

    }
}