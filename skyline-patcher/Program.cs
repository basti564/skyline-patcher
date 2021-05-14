using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;

namespace skyline_patcher
{
    class Program
    {
        static void Main(string[] args)
        {
            bool flag = false;
            bool patched = false;
            if (args.Length == 1)
                flag = args[0] == "--undo";
            Console.Title = "Bastians Oculus Skyline Patcher " + (flag ? "[undo mode]" : "[debug mode]");
            Console.WriteLine("Bastians Oculus Skyline Patcher " + Assembly.GetExecutingAssembly().GetName().Version.ToString());

            RegistryKey oculusKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\Oculus VR, LLC\Oculus");
            Console.WriteLine("Grabbing base path from registry");
            string basePath = (string)oculusKey.GetValue("Base");
            Console.WriteLine(oculusKey.Name + "\\base" + " --> " + (basePath ?? "null"));
            if (basePath == null)
                basePath = @"C:\Program Files\Oculus\sus"; // default base value
            string clientPath = Path.Combine(basePath, "Support", "oculus-client");
            string asarPath = Path.Combine(clientPath, "resources", "app.asar");
            if (!File.Exists(asarPath))
            {
                Console.WriteLine("app.asar not at " + asarPath);
                try
                {
                    Process[] clientProcess = Process.GetProcessesByName("OculusClient");
                    Console.WriteLine("Grabbing the path from Oculus Client process " + clientProcess.FirstOrDefault().Id.ToString());
                    clientPath = Path.GetDirectoryName(clientProcess.FirstOrDefault().MainModule.FileName);
                    asarPath = Path.Combine(clientPath, "resources", "app.asar");
                }
                catch (NullReferenceException)
                {
                    Console.WriteLine("No Oculus Client process is currently running");
                }
            }
            while (!File.Exists(asarPath))
            {
                Console.WriteLine("app.asar not at " + asarPath);
                Console.Write("Manually enter path of app.asar: ");
                asarPath = Console.ReadLine();
                clientPath = Directory.GetParent(Path.GetDirectoryName(asarPath)).FullName;
            }
            Console.WriteLine("Found app.asar at " + asarPath);

            byte[] isPassing = Encoding.ASCII.GetBytes("var t=this.get(e);return t?t.isPassing:null");
            byte[] isUnknown = Encoding.ASCII.GetBytes("var t=this.get(e);return t?t.isUnknown:null");
            byte[] isPassingPatch = Encoding.ASCII.GetBytes("return true                   /*isPassing*/");
            byte[] isUnknownPatch = Encoding.ASCII.GetBytes("return true                   /*isUnknown*/");

            byte[] asarBytes = File.ReadAllBytes(asarPath);
            Console.WriteLine("Read " + asarBytes.Length + " bytes from app.asar");

            BoyerMoore bm = new BoyerMoore();

            bm.SetPattern(flag ? isPassingPatch : isPassing);
            foreach (int offset in bm.SearchAll(asarBytes))
            {
                Console.WriteLine("Patching function 'isPassing' at offset " + offset);
                foreach (int index in Enumerable.Range(0,isPassingPatch.Length))
                    asarBytes[offset + index] = (flag) ? isPassing[index] : isPassingPatch[index];
                patched = true;
            }

            bm.SetPattern(flag ? isUnknownPatch : isUnknown);
            foreach (int offset in bm.SearchAll(asarBytes))
            {
                Console.WriteLine("Patching function 'isUnknown' at offset " + offset);
                foreach (int index in Enumerable.Range(0, isUnknownPatch.Length))
                    asarBytes[offset + index] = (flag) ? isUnknown[index] : isUnknownPatch[index];
                patched = true;
            }

            try
            {
                foreach (Process process in Process.GetProcessesByName("OculusClient"))
                {
                    process.Kill();
                    Console.WriteLine("Killed Oculus Client process " + process.Id);
                }
            }
            catch (NullReferenceException)
            {
                // Oculus Client is not running
            }

            if (patched) {
                File.WriteAllBytes(asarPath, asarBytes);
                Console.WriteLine("Wrote " + asarBytes.Length + " bytes to app.asar");
            }
            else
                Console.WriteLine("No need to patch (app.asar)" + (flag ? " as no patches were found. Run this progeam without \"--undo\" to apply patches." : ". Try opening this as \"skyline-patcher --undo\" to reverse already applied patches"));

            Console.WriteLine("Launching Oculus Client...");
            Process.Start(Path.Combine(clientPath, "OculusClient.exe"));
            Thread.Sleep(5000);
        }
    }
}
