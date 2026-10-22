using System;
using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Content;
using Bonkadoo.Infrastructure.Localization;
using Bonkadoo.Infrastructure.Save;
using Bonkadoo.Infrastructure.Unity;
using UnityEngine;

namespace Bonkadoo.Presentation
{
    /// <summary>Composition root in the Bootstrap scene. Works fully offline.</summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        public string LastError { get; private set; }

        private void Awake()
        {
            if (GameServices.Current != null)
            {
                Destroy(gameObject);
                return;
            }
            DontDestroyOnLoad(gameObject);
            try
            {
                GameServices.Current = Compose();
                var s = GameServices.Current;
                Debug.Log($"[Bonkadoo] content {s.Content.World.ContentVersion}: {s.Content.Rules.Puzzles.Count} puzzles, " +
                          $"scene {s.Session.State.CurrentSceneId}, lang {s.Localizer.Language}");
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Debug.LogError("[Bonkadoo] bootstrap failed: " + ex);
            }
        }

        private static GameServices Compose()
        {
            var content = ContentLoader.Load(ResourcesContent.Load());
            var engine = new InteractionEngine(content.Rules);
            var localizer = new Localizer(content.Localization, DefaultLanguage(), Debug.isDebugBuild);
            var checkpoints = new CheckpointService(new SaveService(new DiskFileStore(UnityPaths.Saves)), content.World, engine);

            if (!checkpoints.TryRestore(CheckpointService.PrimarySlot, out var session, out var load))
            {
                if (load.Status != LoadStatus.Empty) Debug.LogWarning("[Bonkadoo] save unreadable: " + string.Join("; ", load.Problems));
                session = new GameSession(content.World, engine, new InMemoryEventStore());
                session.EnterScene(content.World.StartSceneId, "boot-" + Guid.NewGuid().ToString("N"), 0);
            }
            var services = new GameServices(content, engine, localizer, checkpoints, session)
            {
                Controller = new InteractionController(session, new RequestIdFactory(Guid.NewGuid().ToString("N")),
                    new UnityTickSource(session.State is WorldState ws ? ws.LastTick : 0)),
                Autosave = new AutosaveCoordinator(session, checkpoints, null),
            };
            services.Autosave.SaveFailed += ex => Debug.LogWarning("[Bonkadoo] autosave failed: " + ex.Message);
            return services;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) GameServices.Current?.Autosave?.OnApplicationBackground();
        }

        private void OnApplicationQuit() => GameServices.Current?.Autosave?.OnApplicationBackground();

        private static string DefaultLanguage() =>
            UnityEngine.Application.systemLanguage == SystemLanguage.Vietnamese ? "vi" : "en";
    }
}
