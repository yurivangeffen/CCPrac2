using System;
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

        private ConnectionManager manager;

        private StreamReader reader;
        private StreamWriter writer;

        private Thread thread;

        private Queue<Tuple<char, string[]>> messageQueue;


        public ConnectionWorker(int id, TcpClient client, ConnectionManager manager)
        {
            this.id = id; // Easy access to process ID
            this.client = client;
            this.manager = manager;
            this.messageQueue = new Queue<Tuple<char, string[]>>();
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
            writer.WriteLine("id {0}", id);;
            remoteId = int.Parse(reader.ReadLine().Split(' ')[1]);
            manager.AddNeighbour(remoteId, this);

            Console.WriteLine("Verbonden: {0}", remoteId);

            // Start our main loop
            thread = new Thread(() => Work());
            thread.Start();
        }

        /// <summary>
        /// Main threadloop.
        /// Receives messages and splits them on spaces (except for quotes), also
        /// adds the messages to the message queue.
        /// </summary>
        public void Work()
        {
            while (true)
            {
                string incomming = reader.ReadLine();

                // Split on spaces (except for when a string is quoted)
                string[] parts = Regex.Matches(incomming, @"[\""].+?[\""]|[^ ]+")
                .Cast<Match>()
                .Select(m => m.Value)
                .ToArray<string>();

                addToQueue(parts[0][0], parts.Skip(1).ToArray());
            }
        }

        /// <summary>
        /// Wraps the Enqueue method of the messageQueue (threadsafe).
        /// </summary>
        public void addToQueue(char command, string[] args)
        {
            lock (messageQueue)
            {
                messageQueue.Enqueue(new Tuple<char, string[]>(command, args));
            }
        }

        /// <summary>
        /// Wraps the Dequeue method of the messageQueue (threadsafe).
        /// </summary>
        public Tuple<char, string[]> getFromQueue()
        {
            Tuple<char, string[]> ret = null;
            lock (messageQueue)
            {
                if(messageQueue.Count != 0)
                    ret = messageQueue.Dequeue();
            }
            return ret;
        }

        public void sendMessage(string message)
        {
            if(writer==null)
            {
                Console.WriteLine("// Writer not yet initialized, can't write \"{0}\"", message);
                return;
            }
            writer.WriteLine(message);
        }
    }
}
