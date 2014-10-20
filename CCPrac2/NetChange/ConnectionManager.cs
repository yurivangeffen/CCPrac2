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
        /// <summary>
        /// Estimates to all known nodes
        /// </summary>
        private Dictionary<int, int> D;
        /// <summary>
        /// Prefered neighbours
        /// </summary>
        private Dictionary<int, int> Nb; 
        /// <summary>
        /// Distance estimates of our neighbours <<neighbour, target> distance>
        /// </summary>
        private Dictionary<Tuple<int, int>, int> nD;
        

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
            D = new Dictionary<int, int>();
            Nb = new Dictionary<int, int>();
            nD = new Dictionary<Tuple<int, int>, int>();

            Nb.Add(id, id);
            D.Add(id, 0);

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
                if (_priorityQueue.Count == 0)
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
				case 'D':
					RemoveConnection(command.fromId);
					break;
				case 'B':
					NewMessage(int.Parse(command.data[0]), command.ToString());
					break;
            }
        }

		/// <summary>
		/// removes a target connection and notifies neigbors of the changed distance
		/// </summary>
		/// <param name="id"></param>
		private void RemoveConnection(int id) {
			//todo: actually remove stuff
		}

        /// <summary>
        /// Called by ExecuteCommand when we received a new distance measurement from a neighbour.
        /// Checks weither it is shorter than one we already have (if we even have the toId) and
        /// spreads the word if we changed our distance-measurement.
        /// </summary>
        private void NewDistance(int neighbourId, int toId, int distance)
        {
            nD[Tuple.Create(neighbourId, toId)] = distance + 1;
            Recompute(toId);
            return;
        }

        /// <summary>
        /// Recomputes our distance values according to the Netchange algorithm
        /// </summary>
        private void Recompute(int recId)
        {
            Console.WriteLine("// Recomputing {0}", recId);

            // Find best neighbour
            int minDist = int.MaxValue;
            int bestNeighbour = -1;
            foreach(int neighbour in neighbours.Keys)
            {
                Tuple<int,int> t = Tuple.Create(neighbour, recId);
                if(nD.ContainsKey(t) && nD[t] < minDist)
                {
                    minDist = nD[t];
                    bestNeighbour = neighbour;
                }
            }

            // If we found a path
            if (minDist != int.MaxValue && (!D.ContainsKey(recId) ||  D[recId] > minDist + 1))
            {
                D[recId] = minDist + 1;
                Nb[recId] = bestNeighbour;

                UpdateRouteToAllNeighbours(recId);
            }
            // There's no path to be found :(
            else
            {
                D.Remove(recId);
                Nb.Remove(recId);
            }
            
        }

        ///// <summary>
        ///// Sends an update of the specified route to all neighbours
        ///// </summary>
        private void UpdateRouteToAllNeighbours(int toId)
        {
            foreach (ConnectionWorker worker in neighbours.Values)
            {
                worker.sendMessage(string.Format("U {0} {1}", toId, D[toId]));
            }
        }
        
        /// <summary>
        /// Sends an update of all routes to a neighbour.
        /// </summary>
        /// <param name="neighbour"></param>
        private void UpdateAllRoutesToNeighbour(int neighbour)
        {
            ConnectionWorker worker = neighbours[neighbour];
            foreach (KeyValuePair<int, int> route in Nb)
            {
                worker.sendMessage(string.Format("U {0} {1}", route.Key, D[route.Key]));
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
            else if (Nb.ContainsKey(toId))
            {
                int neighbourId = Nb[toId];
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
            lock (neighbours)   { neighbours.Add(remoteId, worker); }
            lock (Nb)           { Nb.Add(remoteId, remoteId); }
            lock (D)            { D.Add(remoteId, 1); }
            lock (nD)           { nD.Add(Tuple.Create(remoteId, remoteId), 1); }
            
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
            Dictionary<Tuple<int, int>, int> copy;

            // Copy to make sure we don't lock too long
            lock (nD)
            {
                copy = new Dictionary<Tuple<int, int>, int>(nD);
            }

            // Iterate and add to return string
            Dictionary<Tuple<int, int>, int>.Enumerator dictEnum = copy.GetEnumerator();
            while (dictEnum.MoveNext())
                ret += string.Format("{0} {1} {2}\n",
                    dictEnum.Current.Key.Item2,
                    dictEnum.Current.Key.Item1,
                    dictEnum.Current.Value == 0 ? "local" : dictEnum.Current.Value.ToString());

            return ret;
        }

        public int ID
        {
            get { return id; }
        }

    }
}
