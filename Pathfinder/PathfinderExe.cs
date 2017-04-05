using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Pathfinder
{
    abstract class PathfinderExe : Hacknet.ExeModule
    {
        PathfinderExe(Rectangle location, Hacknet.OS operatingSystem, string[] arguments) : base(location, operatingSystem)
        {

        }
    }
}
