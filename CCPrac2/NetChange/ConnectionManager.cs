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

        /// <summary>
        /// Add a message to the prio-queue.
        /// </summary>
        /// <param name="command"></param>
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

        /// <summary>
        /// Thread that listens for incomming connections.
        /// </summary>
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
                catch (Exception e) { Console.WriteLine("// {0}", e.Message); };
            }
        }

        /// <summary>
        /// Thread that processes the messagequeue
        /// </summary>
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

        /// <summary>
        /// Processes an incomming command
        /// </summary>
        private void ExecuteCommand(MessageData command)
        {
            switch (command.messageType)
            {
                case 'U': // new distance received: (to neighbour, distance)
                    NewDistance(command.fromId, int.Parse(command.data[0]), int.Parse(command.data[1]));
                    break;
				case 'D': // removal of connection
					RemoveConnection(command.fromId);
					break;
				case 'B': // incomming message
					NewMessage(int.Parse(command.data[0]), command.data);
					break;
            }
        }

		/// <summary>
		/// removes a target connection and notifies neigbors of the changed distance
		/// </summary>
		/// <param name="id"></param>
		private void RemoveConnection(int ids) {
		  if (ids == ID)
			return;
			lock(nD){
			  List<Tuple<int,int>> toRem = new List<Tuple<int,int>>();
			  foreach (var key in nD.Keys) {
				if (key.Item1 == ids)
				  toRem.Add(key);
			  }
			  foreach (var key in toRem) {
				nD.Remove(key);
			  }
			}
			List<int> recalc = new List<int>();
			lock (Nb) {		  
			  foreach (var buur in Nb) {
				  recalc.Add(buur.Key);
			  }
			}
			lock (neighbours) {
			  if (neighbours.ContainsKey(ids)) {
				var temp = neighbours[ids];
				neighbours.Remove(ids);
				temp.Dispose();
			  }
			}
			foreach (int i in recalc) {
			  Recompute(i);
			}
		}

        /// <summary>
        /// Called by ExecuteCommand when we received a new distance measurement from a neighbour.
        /// Checks weither it is shorter than one we already have (if we even have the toId) and
        /// spreads the word if we changed our distance-measurement.
        /// </summary>
        private void NewDistance(int neighbourId, int toId, int distance)
        {
			lock (nD) {
				nD[Tuple.Create(neighbourId, toId)] = distance;
			}
            Recompute(toId);
            return;
        }

        /// <summary>
        /// Recomputes our distance values according to the Netchange algorithm
        /// </summary>
        private void Recompute(int recId)
        {
            if (recId != id)
            {
                // Find best neighbour
                int minDist = int.MaxValue;
                int bestNeighbour = -1;
				lock (neighbours) {
					foreach (int neighbour in neighbours.Keys) {
						Tuple<int, int> t = Tuple.Create(neighbour, recId);
						if (nD.ContainsKey(t) && nD[t] < minDist) {
							minDist = nD[t];
							bestNeighbour = neighbour;
						}
					}
				}

                // If we found a path
                if (minDist != int.MaxValue && (!D.ContainsKey(recId) || D[recId] != minDist + 1))
                {
					lock (D) { D[recId] = minDist + 1; }
					lock (Nb) { Nb[recId] = bestNeighbour; }
                    Console.WriteLine("Afstand naar {0} is nu {1} via {2}", recId, minDist + 1, bestNeighbour);
                    UpdateRouteToAllNeighbours(recId);
                }
                // There's no path to be found :(
                else if (minDist == int.MaxValue)
                {
					lock (D) { D.Remove(recId); }
					lock (Nb) { Nb.Remove(recId); }
                }
            }
        }

        ///// <summary>
        ///// Sends an update of the specified route to all neighbours
        ///// </summary>
        private void UpdateRouteToAllNeighbours(int toId)
        {
			lock (neighbours) {
				foreach (ConnectionWorker worker in neighbours.Values) {
					worker.sendMessage(string.Format("U {0} {1}", toId, D[toId]));
				}
			}
        }
        
        /// <summary>
        /// Sends an update of all routes to a neighbour.
        /// </summary>
        /// <param name="neighbour"></param>
        private void UpdateAllRoutesToNeighbour(int neighbour)
        {
            ConnectionWorker worker = neighbours[neighbour];
			lock (Nb) {
				foreach (KeyValuePair<int, int> route in Nb) {
					worker.sendMessage(string.Format("U {0} {1}", route.Key, D[route.Key]));
				}
			}
        }

        /// <summary>
        /// Sends our state to all neighbours (used when connecting to a new neighbour).
        /// </summary>
        private void UpdateAllRoutesToAllNeighbours()
        {
			lock (neighbours) {
				foreach (int neighbour in neighbours.Keys)
					UpdateAllRoutesToNeighbour(neighbour);
			}
        }

        /// <summary>
        /// Sends message in the form of "M [toId] [message]".
        /// </summary>
        private void NewMessage(int toId, string[] message)
        {
            string concatted = "";
            for (int i = 1; i < message.Length; ++i )
                concatted += message[i] + " ";
            concatted = concatted.Remove(concatted.Length - 1);

            // Message is for us!
            if (id == toId)
                Console.WriteLine(concatted);
            // We have a connection to the target
            else if (Nb.ContainsKey(toId))
            {
                int neighbourId = Nb[toId];
                ConnectionWorker worker = neighbours[neighbourId];

                worker.sendMessage(string.Format("B {0} {1}", toId, concatted));
                Console.WriteLine("Bericht voor {0} doorgestuurd naar {1}", toId, neighbourId);
            }
            // No entry in router
            else
                Console.WriteLine("Poort {0} is niet bekend", toId);

        }

        /// <summary>
        /// Adds a new neighbour to our network state.
        /// </summary>
        /// <param name="id">ID of the neighbouring process.</param>
        public void AddNeighbour(int remoteId, ConnectionWorker worker)
        {
            lock (neighbours)   { neighbours[remoteId] = worker; }
            lock (Nb)           { Nb[remoteId] = remoteId; }
            lock (D)            { D[remoteId] = int.MaxValue; }
            lock (nD)           { nD[Tuple.Create(remoteId, remoteId)] = 0; }

            Recompute(remoteId);
            UpdateAllRoutesToAllNeighbours();
        }

        /// <summary>
        /// Connects to the given port on the localhost.
        /// </summary>
        /// <param name="remoteId"></param>
        public void ConnectToPort(int remoteId)
        {
            TcpClient client = new TcpClient();
            client.ExclusiveAddressUse = true;
		  for(int tries = 0; tries < 20 && !client.Connected ; tries++){		  
            client.Connect("localhost", remoteId);
			if (!client.Connected)
			  Thread.Sleep(50);
		  }
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
            Dictionary<int, int> copy;

            // Copy to make sure we don't lock too long
            lock (Nb)
            {
                copy = new Dictionary<int, int>(Nb);

                // Iterate and add to return string
                Dictionary<int, int>.Enumerator dictEnum = copy.GetEnumerator();
                while (dictEnum.MoveNext())
                    ret += string.Format("{0} {1} {2}\n",
                        dictEnum.Current.Key,
                        D[dictEnum.Current.Key],
                        D[dictEnum.Current.Key] == 0 ? "local" : dictEnum.Current.Value.ToString());

            }
            return ret;
        }

        public int ID
        {
            get { return id; }
        }

    }
}
