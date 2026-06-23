using Siemens.Collaboration.Net;
using System.Threading.Tasks;

namespace TiaMcpServer.Siemens
{
    public static class Openness
    {
        public static int TiaMajorVersion { get; private set; }

        public static void Initialize(int? tiaMajorVersion = 21)
        {
            TiaMajorVersion = tiaMajorVersion ?? 21; // Default to TIA Portal V21

            // Initialize the Openness API with the specified TIA Portal major version
            Api.Global.Openness().Initialize(tiaMajorVersion: TiaMajorVersion);
        }

        public static async Task<bool> IsUserInGroup()
        {
            if (Api.Global.Openness().IsUserInGroup())
            {
                // user is in group
                return true;
            }
            else
            {
                return await Api.Global.Openness().AddUserToGroupAsync();
            }
        }
    }
}
