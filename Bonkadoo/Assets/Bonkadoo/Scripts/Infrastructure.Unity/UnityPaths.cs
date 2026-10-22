using System.IO;

namespace Bonkadoo.Infrastructure.Unity
{
    public static class UnityPaths
    {
        /// <summary>Tests point this at a temp folder so they never touch a real player's save.</summary>
        public static string OverrideRoot { get; set; }

        public static string Saves => OverrideRoot ?? Path.Combine(UnityEngine.Application.persistentDataPath, "saves");
    }
}
