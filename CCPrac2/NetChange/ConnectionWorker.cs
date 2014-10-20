﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;

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

        private TcpClient client;
		private bool connected = false;

        private ConnectionManager manager;

        private StreamReader reader;
        private StreamWriter writer;

        private Thread thread;

        public ConnectionWorker(int id, TcpClient client, ConnectionManager manager)
        {
            this.id = id; // Easy access to process ID
            this.client = client;
            this.manager = manager;
        }

        /// <summary>
        /// Starts probing for a connection.
        /// </summary>
        public void Start()
        {
            reader = new StreamReader(client.GetStream());
            writer = new StreamWriter(client.GetStream());
            writer.AutoFlush = true;

            Console.WriteLine("// ID: {0}, Socket port: {1}", id, ((IPEndPoint)client.Client.LocalEndPoint).Port);

            // Handshake
            writer.WriteLine("id {0}", id); ;
            remoteId = int.Parse(reader.ReadLine().Split(' ')[1]);

            manager.AddNeighbour(remoteId, this);

            Console.WriteLine("Verbonden: {0}", remoteId);
			connected = true;
            // Start our main loop
            thread = new Thread(() => Work());
            thread.Start();
        }

        /// <summary>
        /// Main threadloop.
        /// Receives messages and splits them on spaces (except for quotes), also
        /// adds the messages to the message queue.
        /// </summary>
        private void Work()
        {
			try {
				while (connected) {
					string incomming = reader.ReadLine();

					// Split on spaces (except for when a string is quoted)
					string[] parts = Regex.Matches(incomming, @"[\""].+?[\""]|[^ ]+")
					.Cast<Match>()
					.Select(m => m.Value)
					.ToArray<string>();

					addToQueue(new MessageData(parts[0][0], id, parts.Skip(1).ToArray()));
				}
			} catch {
				connected = false;
				addToQueue(new MessageData('D', id, null));
			}
        }

        /// <summary>
        /// Wraps the Enqueue method of the messageQueue (threadsafe).
        /// </summary>
        public void addToQueue(MessageData message)
        {
            manager.Enqueue(message);
        }

        public void sendMessage(string message)
        {
			if (connected) {
				if (writer == null) {
					Console.WriteLine("// Writer not yet initialized, can't write \"{0}\"", message);
					return;
				}
				try {
					writer.WriteLine(message);
				} catch {
					addToQueue(new MessageData('D',id,null));
					connected = false;
				}
			}
        }
    }
}
