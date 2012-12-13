using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace MESI
{

    class Program
    {

        static void Main(string[] args)
        {
            //Terminate the program if the arguments are not correct.
            if (args.Length != Constants.PARAMS)
            {
                Console.Write("Invalid number of command line arguments\n");
                return;
            }

            //Main logic of program.
            else
            {
                Simulate obj = new Simulate(args);
                obj.runSimulation();
            }
        }
    }
}
