using System.Collections;
using Bonkadoo.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Bonkadoo.Tests.PlayMode
{
    public class BootstrapPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapSceneComposesServicesOffline()
        {
            foreach (var old in Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None)) Object.Destroy(old.gameObject);
            GameServices.Reset();
            yield return null;
            SceneManager.LoadScene("Bootstrap");
            yield return null;
            yield return null;

            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.IsNotNull(bootstrap, "GameBootstrap missing from scene");
            Assert.IsNull(bootstrap.LastError, bootstrap.LastError);
            Assert.IsNotNull(GameServices.Current);
            Assert.AreEqual(45, GameServices.Current.Content.Rules.Puzzles.Count);
            Assert.IsNotNull(GameServices.Current.Session.State.CurrentSceneId);
        }
    }
}
