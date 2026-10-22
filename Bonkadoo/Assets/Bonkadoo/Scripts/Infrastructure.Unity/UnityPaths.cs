using System.IO;
using UnityEngine;

namespace Bonkadoo.Infrastructure.Unity
{
    public static class UnityPaths
    {
        public static string Saves => Path.Combine(UnityEngine.Application.persistentDataPath, "saves");
    }
}
