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
        private Dictionary<int, ConnectionWorker> neighbours;
        private Dictionary<int, Tuple<int,int>> routing;
        TcpListener listener;

        /// <summary>
        /// Constructs a ConnectionManager object.
        /// </summary>
        /// <param name="id">ID of this process (also used as incomming portnumber).</param>
        public ConnectionManager(int id)
        {
            this.id = id;
            neighbours = new Dictionary<int, ConnectionWorker>();
            routing = new Dictionary<int, Tuple<int,int>>();

            routing.Add(id, new Tuple<int, int>(id, 0));

            listener = new TcpListener(IPAddress.Any, id);
        }

        /// <summary>
        /// Starts listening for incomming connections and tries to connect to others.
        /// </summary>
        public void Start()
        {
            Thread t = new Thread(new ThreadStart(listenerThread));
						Thread w = new Thread(workerThread);
            t.Start();
						w.Start();
        }

        private void listenerThread()
        {
            listener.ExclusiveAddressUse = true;
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

        private void workerThread()
        {
            while (true)
            {
                bool hadItems = false;

                // Copying dicitonary to make sure we don't have a modification during looping
                Dictionary<int, ConnectionWorker> copy;
                lock (neighbours)
                {
                    copy = new Dictionary<int, ConnectionWorker>(neighbours);
                }

                // Loop and check if there are available messages
                foreach (KeyValuePair<int, ConnectionWorker> k in copy)
                {
                    Tuple<char, string[]> work = k.Value.getFromQueue();
                    if (work != null)
                    {
                        hadItems = true;
                        ExecuteCommand(work, k.Key);
                    }

                    if (!hadItems) // If there are no items, make way for other threads.
                        Thread.Yield();
                }
            }
        }

        private void ExecuteCommand(Tuple<char, string[]> command, int fromNeighbour) {
            switch(command.Item1)
            {
                case 'D': // new distance received: (to neighbour, distance)
                    NewDistance(fromNeighbour, int.Parse(command.Item2[0]), int.Parse(command.Item2[1]));
                    break;
            }
        }

        /// <summary>
        /// Called by ExecuteCommand when we received a new distance measurement from a neighbour.
        /// Checks weither it is shorter than one we already have (if we even have the toId) and
        /// spreads the word if we changed our distance-measurement.
        /// </summary>
        private void NewDistance(int neighbourId, int toId, int distance)
        {

        }

        /// <summary>
        /// Sends message in the form of "M [toId] [message]".
        /// </summary>
        private void NewMessage(int toId, string message)
        {
            // Message is for us!
            if (id == toId)
                Console.WriteLine(message);
            // We have a connection to the target
            else if (routing.ContainsKey(toId))
            {
                int neighbourId = routing[toId].Item1;
                ConnectionWorker worker = neighbours[neighbourId];

                worker.sendMessage(string.Format("M {0} {1}", toId, message));
                Console.WriteLine("Bericht voor {0} doorgestuurd naar {1}", toId, neighbourId);
            }
            // No entry in router
            else
                Console.WriteLine("// Unable to send message, id {0} isn't in routing table!", toId);

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
                routing.Add(remoteId, new Tuple<int, int>(remoteId, 1));
            }
        }

        /// <summary>
        /// Connects to the given port on the localhost.
        /// </summary>
        /// <param name="remoteId"></param>
        public void ConnectToPort(int remoteId)
        {
            Console.WriteLine("// Connecting to: {0}", remoteId);

            TcpClient client = new TcpClient();
            client.ExclusiveAddressUse = true;
            client.Connect("localhost", remoteId);
            
            ConnectionWorker worker = new ConnectionWorker(id, client, this);
            worker.Start();
        }

        /// <summary>
        /// Returns a formatted string of the routing table.
        /// </summary>
        /// <returns></returns>
        public string RoutingString()
        {
            string ret = "";
            Dictionary<int, Tuple<int, int>> copy;

            // Copy to make sure we don't lock too long
            lock(routing)
            {
                copy = new Dictionary<int, Tuple<int, int>>(routing);
            }

            // Iterate and add to return string
            Dictionary<int, Tuple<int, int>>.Enumerator dictEnum = copy.GetEnumerator();
            while (dictEnum.MoveNext())
                ret += string.Format("{0} {1} {2}\n",
                    dictEnum.Current.Key,
                    dictEnum.Current.Value.Item1,
                    dictEnum.Current.Value.Item2 == 0 ? "local" : dictEnum.Current.Value.Item2.ToString());

            return ret;
        }

        public int ID
        {
            get { return id; }
        }

    }
}
