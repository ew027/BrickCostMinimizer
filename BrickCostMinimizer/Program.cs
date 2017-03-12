using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;

namespace BrickCostMinimizer {
    class Program {
        static void Main(string[] args) {
            // check the right arguments have been supplied
            if (args.Length == 2) {
                string wantedListFilename = args[0];
                int maxSellers = 0;

                if (Int32.TryParse(args[1], out maxSellers) && maxSellers > 0) {
                    CostMinimizer minimizer = new CostMinimizer();

                    // if an exception occurs give the user the error message and exit gracefully
                    try {
                        minimizer.FindLowestCostCombination(wantedListFilename, maxSellers);
                    } catch (Exception ex) {
                        Console.WriteLine("An error occurred: {0}", ex.Message);
                    }
                } else {
                    Console.WriteLine("Max sellers must be a number > 0");
                    Console.WriteLine();
                    PrintUsageNotes();
                }
            } else {
                Console.WriteLine("2 arguments are required");
                Console.WriteLine();
                PrintUsageNotes();
            } 
        }

        private static void PrintUsageNotes() {
            Console.WriteLine("Usage: BrickCostMinimizer wantedlist.bsx maxsellers");
            Console.WriteLine();
            Console.WriteLine("Example: BrickCostMinimizer c:\\temp\\wanted.bsx 3");
        }
    }
}
