using Siemens.Engineering.CrossReference;
using Siemens.Engineering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.Test
{
    internal class Common
    {
        public static bool OpenProject(Portal tiaPortal, string projectPath)
        {
            bool success = false;

            if (Portal.IsLocalProjectFile(projectPath))
            {
                success = tiaPortal.OpenProject(projectPath);
            }

            if (Portal.IsLocalSessionFile(projectPath))
            {
                success = tiaPortal.OpenSession(projectPath);
            }

            return success;
        }

        public static bool CloseProject(Portal tiaPortal, string projectPath)
        {
            bool success = false;

            if (Portal.IsLocalProjectFile(projectPath))
            {
                success = tiaPortal.CloseProject();
            }

            if (Portal.IsLocalSessionFile(projectPath))
            {
                success = tiaPortal.CloseSession();
            }

            return success;
        }
    }
}
