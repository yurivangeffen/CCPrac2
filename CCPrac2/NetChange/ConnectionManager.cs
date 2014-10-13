using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace CCPrac2.NetChange
{
    /// <summary>
    /// Manages worker threads and overall network state.
    /// </summary>
    class ConnectionManager
    {
        private int id;
        Dictionary<int, ConnectionWorker> neighbours;
        TcpListener listener;

        /// <summary>
        /// Constructs a ConnectionManager object.
        /// </summary>
        /// <param name="id">ID of this process (also used as incomming portnumber).</param>
        public ConnectionManager(int id)
        {
            this.id = id;
            neighbours = new Dictionary<int, ConnectionWorker>();
            listener = new TcpListener(IPAddress.Any, id);
        }

        /// <summary>
        /// Starts listening for incomming connections and tries to connect to others.
        /// </summary>
        public void Start()
        {
            Thread t = new Thread(new ThreadStart(listenerThread));
            t.Start();
        }

        private void listenerThread()
        {
            listener.Start();
            while (true)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    ConnectionWorker worker = new ConnectionWorker(id, client, this);
                    worker.Start();
                }
                catch (Exception e) { Console.WriteLine(e.Message); };
            }
        }

        /// <summary>
        /// Adds a new neighbour to our network state.
        /// </summary>
        /// <param name="id">ID of the neighbouring process.</param>
        public void AddNeighbour(int remoteId, ConnectionWorker worker)
        {
            lock(neighbours)
            {
                neighbours.Add(remoteId, worker);
            }
        }

        public void ConnectToPort(int remoteId)
        {
            Console.WriteLine("// Connecting to: {0}", remoteId);
            TcpClient client = new TcpClient();
            client.Connect("localhost", remoteId);
            
            ConnectionWorker worker = new ConnectionWorker(id, client, this);
            worker.Start();
        }

        public int ID
        {
            get { return id; }
        }

    }
}
