using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        
            // Create a ConnectionManager object and pass the ports

            // Start the ConnectionManager

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
