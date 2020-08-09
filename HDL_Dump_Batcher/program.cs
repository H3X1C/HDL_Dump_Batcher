using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace HDL_Dump_Batcher
{
    class Program
    {
        static readonly string HDLExe = "hdl_dump_092.exe";
        static void Main(string[] args)
        {
            bool namingMethod = true; // True == ISO, False == Dynamic
            string operation = "inject_dvd";
            int hddNum;

            Console.WriteLine("--HDL_DUMP Batch Utility V1.0--\n");

            // Basic error check
            if (!File.Exists(HDLExe))
            {
                ErrorHandler(HDLExe + " not found. Please place " + HDLExe + " in the working directory and relaunch. Press any key to exit...");
            }

            Console.WriteLine("Note: Please ensure your PS2 formatted hard drive is connected and the games (.iso's) you wish to install are in the same folder as this exe (HDL_DUMP_Batcher.exe) which needs to be run as admin.\n");
            Console.WriteLine("It is required prior to running this utility that you seperate your games into different folers for CD and DVD.");
            Console.WriteLine("Which type of games do you wish to install? (CD = 700MB or less)");
            Console.WriteLine("Options: 'dvd' or 'cd'\n");
            Console.Write("Please enter game type: ");

            var operationConsoleLine = Console.ReadLine();
            if (operationConsoleLine == "dvd")
            {
                operation = "inject_dvd";
            }
            else if (operationConsoleLine == "cd")
            {
                operation = "inject_cd";
            }
            else
            {
                ErrorHandler("Invalid option selected. Press any key to exit...");
            }

            Console.WriteLine("This utility can name games (The title of games seen within OPL) based off either the iso file name itself (recommended) or hdl dump can be used to analyse the iso to query it's name.");
            Console.WriteLine("Options: 'iso' or 'dynamic'\n");
            Console.Write("Please enter Game naming method: ");

            var namingMethodInput = Console.ReadLine();
            if (namingMethodInput == "iso")
            {
                namingMethod = true;
            }
            else if (namingMethodInput == "dynamic")
            {
                namingMethod = false;
            }
            else
            {
                ErrorHandler("Invalid option selected. Press any key to exit...");
            }

            Console.WriteLine();
            Console.WriteLine("--Quering attached HDD's--");

            // Setup
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = HDLExe,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    Arguments = "query"
                }
            };

            try
            {
                process.Start();
            }
            catch(Exception ex)
            {
                ErrorHandler("Error: Unable to execute " + HDLExe + " Please ensure you are running HDL_Dump_Batcher as Admin.\n\nException: " + ex);
            }

            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                Console.WriteLine(line);
            }

            process.WaitForExit();

            Console.WriteLine("Please enter the harddrive number of the drive labled as 'formatted Playstation 2 HDD'\n");
            Console.Write("Please enter HDD number: ");

            var hddNumberInput = Console.ReadLine();
            if (string.IsNullOrEmpty(hddNumberInput) || !int.TryParse(hddNumberInput, out int hddNumberResult))
            {
                Console.WriteLine("Invalid option selected. HDD number must be a number. Press any key to exit...");
                Console.ReadLine();
                return;
            }
            hddNum = hddNumberResult;

            // Traverse current directory
            DirectoryInfo directory = new DirectoryInfo(Directory.GetCurrentDirectory());
            FileInfo[] Files = directory.GetFiles("*.iso"); //Getting Text files
            if (Files.Length == 0)
            {
                ErrorHandler("No ISO files found. Press any key to exit...");
            }

            // Populate IsoInfo
            var isoList = new List<IsoInfo>();
            foreach (FileInfo file in Files)
            {
                var currentIso = new IsoInfo { IsoPath = "\"" + file.Name + "\"" };
                process.StartInfo.Arguments = "cdvd_info " + currentIso.IsoPath;
                process.Start();
                var isoInfoString = process.StandardOutput.ReadLine();

                // Santity check
                if (string.IsNullOrEmpty(isoInfoString))
                {
                    Console.WriteLine("ERROR: Could not retrieve info for " + file.Name + "\n\nDEBUG: " + HDLExe + " " + process.StartInfo.Arguments);
                    process.WaitForExit();
                    continue;
                }

                var isoStringRegex = Regex.Matches(isoInfoString, "\"([^\"]*)\"");

                // Santity check on results
                if (isoStringRegex.Count != 2)
                {
                    Console.WriteLine("ERROR: Could not parse GameId or GameName from cdvd_info. Unable to proceed.\n\nDEBUG: " + isoInfoString);
                    process.WaitForExit();
                    continue;
                }

                // Santity check
                if (!isoStringRegex[0].Success || !isoStringRegex[1].Success)
                {
                    Console.WriteLine("ERROR: Regex failure for GameId or GameName.\n\nDEBUG: " + isoInfoString);
                    process.WaitForExit();
                    continue;
                }

                currentIso.GameId = isoStringRegex[0].ToString();
                currentIso.GameName = namingMethod ? "\"" + Path.GetFileNameWithoutExtension(file.Name) + "\"" : isoStringRegex[1].ToString();

                isoList.Add(currentIso);
                Console.WriteLine(currentIso.IsoPath + " added to queue.");
                process.WaitForExit();
            }


            Console.WriteLine("Press Enter to begin batch installation.");
            Console.ReadLine();

            // Write games to disc based on IsoInfo
            foreach (var iso in isoList)
            {
                if(string.IsNullOrEmpty(iso.GameName) || string.IsNullOrEmpty(iso.GameId) || string.IsNullOrEmpty(iso.IsoPath))
                {
                    Console.WriteLine("ERROR: GameName or GameID or IsoName is null or empty. Debug Info: GameName " + iso.GameName + " GameID " + iso.GameId + " IsoPath " + iso.IsoPath);
                    continue;
                }

                Console.WriteLine("\nInstalling GameID: " + iso.GameId + " Title: " + iso.GameName);

                process.StartInfo.Arguments = operation + " hdd" + hddNum +": " + iso.GameName + " " + iso.IsoPath + " " + iso.GameId + " *u4";
                process.Start();

                // Output progress %
                while (!process.StandardOutput.EndOfStream)
                {
                    var line = process.StandardOutput.ReadLine();
                    Console.Write("\r{0}", line);
                }

                process.WaitForExit();
            }

            Console.WriteLine("\n\nBatch Installation Complete.\n");
            Console.WriteLine("Press Enter to view HDL Table of Contents");
            Console.ReadLine();


            Console.WriteLine("--HDL Table of Contents--");
            process.StartInfo.Arguments = "hdl_toc hdd" + hddNum + ":";
            process.Start();

            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                Console.WriteLine(line);
            }
            process.WaitForExit();

            Console.WriteLine();
            Console.WriteLine("Press Enter to view Partition Table of Contents");
            Console.ReadLine();

            Console.WriteLine("--Partition Table of Contents--");
            process.StartInfo.Arguments = "toc hdd" + hddNum + ":";
            process.Start();

            while (!process.StandardOutput.EndOfStream)
            {
                var line = process.StandardOutput.ReadLine();
                Console.WriteLine(line);
            }
            process.WaitForExit();

            Console.WriteLine();
            Console.WriteLine("Press Enter to Exit");
            Console.ReadLine();
        }

        private static void ErrorHandler(string errorMessage)
        {
            Console.WriteLine(errorMessage);
            Console.ReadKey();
            Environment.Exit(1);
        }
    }
}