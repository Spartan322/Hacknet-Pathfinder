using Pathfinder.Event;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Pathfinder
{
    static class Pathfinder
    {
        static Dictionary<string, PathfinderMod> mods = new Dictionary<string, PathfinderMod>();
        static Dictionary<string, Tuple<string, int, int, string, Type>> exePrograms =
            new Dictionary<string, Tuple<string, int, int, string, Type>>();

        public static void init()
        {
            EventManager.RegisterListener(typeof(CommandSentEvent), CommandHandler.CommandListener);
            LoadMods();
        }

        public static void testEventListener(PathfinderEvent pathfinderEvent)
        {
            Console.WriteLine("HEY ! LISTEN !!");
        }

        /// <summary>
        /// Gets if a mod is loaded or not
        /// </summary>
        /// <param name="modID">Name / ID of the mod</param>
        /// <returns>True if loaded, False if not</returns>
        static public bool IsModLoaded(string modID)
        {
            return mods.ContainsKey(modID);
        }

        /// <summary>
        /// Get the Type of a mod
        /// </summary>
        /// <param name="modID">Name / ID of the mod</param>
        /// <returns>Type of the mod</returns>
        static public Type GetModType(string modID)
        {
            if (!mods.ContainsKey(modID))
                return null;
            foreach (KeyValuePair<string, PathfinderMod> modEntry in mods)
            {
                if (modEntry.Key == modID)
                    return modEntry.Value.GetType();
            }
            return null;
        }

        /// <summary>
        /// Get the instance of a mod
        /// </summary>
        /// <param name="modID">Name / ID of the mod</param>
        /// <returns>Instance of the mod</returns>
        static public PathfinderMod GetModInstance(string modID)
        {
            if (!mods.ContainsKey(modID))
                return null;
            foreach (KeyValuePair<string, PathfinderMod> modEntry in mods)
            {
                if (modEntry.Key == modID)
                    return modEntry.Value;
            }
            return null;
        }

        public static void LoadMods()
        {
            var path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            char separator = Path.DirectorySeparatorChar;

            if (!Directory.Exists(path + separator + "Mods"))
                Directory.CreateDirectory(path + separator + "Mods");

            foreach (string dll in Directory.GetFiles(path + separator + "Mods" + separator, "*.dll"))
            {
                try
                {
                    var modAssembly = Assembly.LoadFile(dll);
                    Type modType = null;
                    foreach (Type t in (modAssembly.GetTypes().Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(PathfinderMod)))))
                    {
                        modType = t;
                        break;
                    }
                    var modInstance = (PathfinderMod)Activator.CreateInstance(modType);

                    var methodInfo = modType.GetMethod("GetIdentifier");
                    if (methodInfo == null)
                        throw new NotSupportedException("Method 'GetIdentifier' doesn't exist : invalid Mod.dll");
                    var name = (string)methodInfo.Invoke(modInstance, null);
                    Console.WriteLine("Loading mod : " + name);

                    mods.Add(name, modInstance);

                    modInstance.Load();

                }
                catch (Exception ex)
                {
                    Console.WriteLine("Impossible to load mod " + dll + " : " + ex.Message);
                }
            }
        }

        public static void LoadModContent()
        {
            foreach (KeyValuePair<string, PathfinderMod> mod in mods)
            {
                mod.Value.LoadContent();
            }
        }

        /// <summary>
        /// Adds a Hacknet .exe program to the Mod Manager
        /// Use it inside onLoadContent()
        /// </summary>
        /// <param name="name">Name of the program ("example.exe")</param>
        /// <param name="progID">In-game ID of the program</param>
        /// <param name="progCode">In-game binary code of the program (string, size 500, binary)</param>
        /// <param name="exeProg">Type of the program; Needs to be a HackmoreExe subclass</param>
        /// <returns>True if added succesfully, false if not</returns>
        public static bool addExeProgram(string name, int progID, string progCode, Type exeProg)
        {
            if (exePrograms.ContainsKey(name))
                return false;
            if (!exeProg.IsSubclassOf(typeof(PathfinderExe)))
                return false;
            exePrograms.Add(name, new Tuple<string, int, int, string, Type>(null, 0, progID, progCode, exeProg));

            Hacknet.PortExploits.exeNums.Add(progID);
            Hacknet.PortExploits.cracks[progID] = name;
            Hacknet.PortExploits.needsPort[progID] = false;
            Hacknet.PortExploits.crackExeData[progID] = progCode;
            Hacknet.PortExploits.crackExeDataLocalRNG[progID] = progCode;
            return true;
        }

        /// <summary>
        /// Adds a Hacknet .exe PortCracker to the Mod Manager
        /// Use it inside onLoadContent()
        /// </summary>
        /// <param name="name">Name of the program ("example.exe")</param>
        /// <param name="serviceName">Used by Hacknet</param>
        /// <param name="portNum">Port to be cracked with the program</param>
        /// <param name="progID">In-game ID of the program (maybe needs to be equal to portNum)</param>
        /// <param name="progCode">In-game binary code of the program (string, size 500, binary)</param>
        /// <param name="exeProg">Type of the program; Needs to be a HackmoreExe subclass</param>
        /// <returns>True if added succesfully, false if not</returns>
        public static bool addExeProgram(string name, string serviceName, int portNum, int progID, string progCode, Type exeProg)
        {
            if (exePrograms.ContainsKey(name))
                return false;
            if (!exeProg.IsSubclassOf(typeof(PathfinderExe)))
                return false;
            exePrograms.Add(name, new Tuple<string, int, int, string, Type>(serviceName, portNum, progID, progCode, exeProg));

            Hacknet.PortExploits.exeNums.Add(progID);
            Hacknet.PortExploits.services[progID] = serviceName;
            Hacknet.PortExploits.cracks[progID] = name;
            Hacknet.PortExploits.needsPort[progID] = true;
            Hacknet.PortExploits.crackExeData[progID] = progCode;
            Hacknet.PortExploits.crackExeDataLocalRNG[progID] = progCode;
            return true;
        }
    }
}
