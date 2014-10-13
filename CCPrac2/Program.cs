using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CCPrac2.NetChange;

namespace CCPrac2
{
    class Program
    {

        static void Main(string[] args)
        {
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
                switch(split[0])
                {
                    case "R":
                        break;
                    case "B":
                        break;
                    case "C":
                        break;
                    case "D":
                        break;
                    default:
                        Console.WriteLine("Unknown command \"{0}\".", split[0]);
                        break;
                }
            }
        }
    }
}
