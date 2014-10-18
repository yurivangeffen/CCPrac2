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
        private Dictionary<int, Tuple<int, int>> routing;
        private Queue<MessageData> _priorityQueue;
        private Queue<MessageData> _normalQueue;

        TcpListener listener;

        /// <summary>
        /// Constructs a ConnectionManager object.
        /// </summary>
        /// <param name="id">ID of this process (also used as incomming portnumber).</param>
        public ConnectionManager(int id)
        {
            this.id = id;
            neighbours = new Dictionary<int, ConnectionWorker>();
            routing = new Dictionary<int, Tuple<int, int>>();

            routing.Add(id, new Tuple<int, int>(id, 0));
            _priorityQueue = new Queue<MessageData>();
            _normalQueue = new Queue<MessageData>();

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

        public void Enqueue(MessageData command)
        {
            new Task(() =>
            {
                lock (_priorityQueue)
                {
                    _priorityQueue.Enqueue(command);
                }
            }).Start();
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
                if (_priorityQueue.Count > 0)
                    Thread.Sleep(20);

                MessageData command = null;
                lock (_priorityQueue)
                {
                    if (_priorityQueue.Count > 0)
                        command = _priorityQueue.Dequeue();
                }

                if(command != null)
                    ExecuteCommand(command);
            }
        }

        private void ExecuteCommand(MessageData command)
        {
            switch (command.messageType)
            {
                case 'U': // new distance received: (to neighbour, distance)
                    NewDistance(command.fromId, int.Parse(command.data[0]), int.Parse(command.data[1]));
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
            // If we don't have it yet, add it to our routing
            if(!routing.ContainsKey(toId))
            {
                // Add to routing and increase distance with 1
                routing.Add(toId, new Tuple<int, int>(neighbourId, distance + 1));
                UpdateRouteToAllNeighbours(toId);
                return;
            }
            else if(routing[toId].Item2 > distance + 1)
            {
                routing[toId] = new Tuple<int, int>(neighbourId, distance + 1);
                UpdateRouteToAllNeighbours(toId);
                return;
            }
        }

        /// <summary>
        /// Sends an update of the specified route to all neighbours
        /// </summary>
        private void UpdateRouteToAllNeighbours(int toId)
        {
            foreach (ConnectionWorker worker in neighbours.Values)
            {
                worker.sendMessage(string.Format("U {0} {1}", toId, routing[toId].Item2));
            }
        }
        
        /// <summary>
        /// Sends an update of the specified route to all neighbours
        /// </summary>
        private void UpdateAllRoutesToNeighbour(int neighbour)
        {
            ConnectionWorker worker = neighbours[neighbour];
            foreach (KeyValuePair<int, Tuple<int, int>> route in routing)
            {
                worker.sendMessage(string.Format("U {0} {1}", route.Key, route.Value.Item2));
            }
        }

        private void UpdateAllRoutesToAllNeighbours()
        {
            foreach (int neighbour in neighbours.Keys)
                UpdateAllRoutesToNeighbour(neighbour);
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
            lock (neighbours)
            {
                neighbours.Add(remoteId, worker);
                routing.Add(remoteId, new Tuple<int, int>(remoteId, 1));
            }
            UpdateAllRoutesToAllNeighbours();
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
            lock (routing)
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
