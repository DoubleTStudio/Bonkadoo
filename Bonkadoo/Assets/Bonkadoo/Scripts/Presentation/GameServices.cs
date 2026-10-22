using Bonkadoo.Application;
using Bonkadoo.Domain;
using Bonkadoo.Infrastructure.Content;
using Bonkadoo.Infrastructure.Localization;
using Bonkadoo.Infrastructure.Save;

namespace Bonkadoo.Presentation
{
    /// <summary>Composition root output; one instance per app run.</summary>
    public sealed class GameServices
    {
        public static GameServices Current { get; internal set; }

        /// <summary>Lets PlayMode tests re-run the composition root.</summary>
        public static void Reset() => Current = null;

        public GameContent Content { get; }
        public InteractionEngine Engine { get; }
        public ILocalizer Localizer { get; }
        public CheckpointService Checkpoints { get; }
        public GameSession Session { get; internal set; }
        public InteractionController Controller { get; internal set; }
        public AutosaveCoordinator Autosave { get; internal set; }

        public GameServices(GameContent content, InteractionEngine engine, ILocalizer localizer, CheckpointService checkpoints,
            GameSession session)
        {
            Content = content;
            Engine = engine;
            Localizer = localizer;
            Checkpoints = checkpoints;
            Session = session;
        }
    }
}
