using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CCPrac2.NetChange;
using System.Threading;

namespace CCPrac2
{
    class Program
    {

        static void Main(string[] args)
        {
            if (args.Length == 0)
                return;

            // Make a portlist
            int[] ports = new int[args.Length];
            for (int i = 0; i < ports.Length; ++i)
                ports[i] = int.Parse(args[i]);

            Console.WriteLine("// Port: {0}", ports[0]);
        
            // Create a ConnectionManager object and pass the ports
            ConnectionManager manager = new ConnectionManager(ports[0]);

            // Start the ConnectionManager
            manager.Start();

            // Make it connect to lower portnumbers
            for (int i = 1; i < ports.Length; ++i)
                if (ports[i] < ports[0])
                    manager.ConnectToPort(ports[i]);

            // Listen to incomming commands
            string command;
            while((command=Console.ReadLine()) != "")
            {
                string[] split = command.Split(' ');
								int port;
                switch(split[0])
                {
                    case "R":
                        Console.Write(manager.RoutingString());
                        break;
                    case "B":
										manager.Enqueue(new MessageData('B',manager.ID,split.Skip(1).ToArray()));
                        break;
                    case "C":	
										if (int.TryParse(split[1],out port))
											manager.ConnectToPort(port);
										else
											Console.WriteLine("{0} is not a valid port number",split[1]);
                        break;
                    case "D":
											manager.Enqueue(new MessageData('D',manager.ID,split.Skip(1).ToArray()));
                        break;
                    default:
                        Console.WriteLine("Unknown command \"{0}\".", split[0]);
                        break;
                }
            }
        }
    }
}
