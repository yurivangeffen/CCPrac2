using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace CCPrac2.NetChange
{
    /// <summary>
    /// Handles the connection to neighbouring processes.
    /// Also keeps state for that neighbour.
    /// </summary>
    class ConnectionWorker
    {
        private int id;
        private int remoteId;
        //private int port; // Differs from "id" because we get assigned a separate handler port

        private TcpClient client;

        private ConnectionManager manager;

        private StreamReader reader;
        private StreamWriter writer;

        public ConnectionWorker(int id, TcpClient client, ConnectionManager manager)
        {
            this.id = id; // Easy access to process ID
            this.client = client;
            this.manager = manager;
            //this.port = ((IPEndPoint)client.Client.RemoteEndPoint).Port; // Easy access to actual port
        }

        /// <summary>
        /// Starts probing for a connection.
        /// </summary>
        public void Start()
        {
            reader = new StreamReader(client.GetStream());
            writer = new StreamWriter(client.GetStream());

            //Console.WriteLine("// ID: {0}, Socket port: {1}", id, ((IPEndPoint)client.Client.RemoteEndPoint).Port);

            // Handshake
            writer.WriteLine("id {0}", id);
            writer.Flush();
            remoteId = int.Parse(reader.ReadLine().Split(' ')[1]);
            manager.AddNeighbour(remoteId, this);

            Console.WriteLine("Verbonden: {0}", remoteId);
        }
    }
}
