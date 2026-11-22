using UnityEngine;

namespace MyGame
{
    /// <summary>
    /// First-visit world map tutorial, one lesson at a time: "enter the stage" while standing on an enterable node, and
    /// "next / previous stage" once a neighbouring node is open (normally after the first clear).
    /// Each lesson first explains (map input held, screen dimmed except the node pad, bubble advanced with Confirm), then
    /// leaves the small prompt up until the real action (TryEnter / a node move). The map never uses Time.timeScale here.
    /// </summary>
    public sealed class WorldMapTutorialDirector : MonoBehaviour
    {
        private enum Lesson { None, Move, Enter }

        private WorldMapController map;
        private TutorialConfig config;
        private TutorialPromptView view;
        private TutorialExplainView explainView;
        private TutorialExplainRunner runner;
        private GameSystems systems;
        private Lesson shown = Lesson.None, explaining = Lesson.None;
        private bool explainedMove, explainedEnter; // this visit
        private float idleFor;
        private bool subscribed;

        public bool IsExplaining => explaining != Lesson.None;

        public static WorldMapTutorialDirector Attach(WorldMapController map, Transform canvas)
        {
            if (!GameSystems.Exists || map == null || canvas == null)
                return null;
            var go = new GameObject("WorldMapTutorial");
            go.transform.SetParent(map.transform, false);
            var d = go.AddComponent<WorldMapTutorialDirector>();
            d.map = map;
            d.systems = GameSystems.Instance;
            d.config = TutorialConfig.Load();
            d.view = TutorialPromptView.Create(canvas, d.config.view, map.InfoPanel, true, 400f);
            d.explainView = TutorialExplainView.Create(canvas, d.config.explain, true);
            d.runner = new TutorialExplainRunner(d.explainView, d.config.explain, d.ResolveSpot);
            d.Subscribe(true);
            return d;
        }

        private void OnDestroy()
        {
            Subscribe(false);
            if (map != null) map.TutorialHold = false;
            if (view != null) Destroy(view.gameObject);
            if (explainView != null) Destroy(explainView.gameObject);
        }

        private void Subscribe(bool on)
        {
            if (on == subscribed || map == null)
                return;
            subscribed = on;
            if (on) { map.NodeMoveStarted += OnMoved; map.StageEntering += OnEntering; }
            else { map.NodeMoveStarted -= OnMoved; map.StageEntering -= OnEntering; }
        }

        private TutorialProgress Progress => systems.Save.HasSave ? systems.Save.Current.Tutorial : null;

        private void OnMoved()
        {
            // Moving teaches the move lesson even if it was not on screen yet (clicking a node also counts).
            if (Progress != null)
                Finish(config.mapMoveId, shown == Lesson.Move, true); // saved by the map on arrival
        }

        private void OnEntering()
        {
            if (Progress == null)
                return;
            Finish(config.mapEnterId, shown == Lesson.Enter, false);
            systems.Save.Save(); // the map is a normal save point; keeps the flag even if the stage is abandoned
        }

        private void Finish(string id, bool wasShown, bool successBeat)
        {
            Progress?.MarkSeen(id);
            if (wasShown)
            {
                if (successBeat) view.Complete(config.successHoldSeconds, config.successKey);
                else view.Complete(0.3f);
                shown = Lesson.None;
            }
        }

        private void EndExplain()
        {
            explaining = Lesson.None;
            map.TutorialHold = false;
            idleFor = 0f;
        }

        private void Update()
        {
            var progress = Progress;
            bool blocked = progress == null || map.IsTutorialBlocked || DemoMode.HidesTutorials;

            if (explaining != Lesson.None)
            {
                if (blocked)
                {
                    // Something else took the screen (popup, load): drop the explanation; it shows again later.
                    runner.Cancel();
                    EndExplain();
                    return;
                }
                var result = runner.Poll();
                if (result == ExplainResult.Running)
                    return;
                var lesson = explaining;
                EndExplain();
                if (result == ExplainResult.Skipped)
                {
                    progress.SkipAll(); // grants nothing: only the hints count as seen
                    systems.Save.Save();
                    return;
                }
                if (lesson == Lesson.Move) explainedMove = true; else explainedEnter = true;
                ShowPrompt(lesson);
                return;
            }

            idleFor = blocked ? 0f : idleFor + Time.unscaledDeltaTime;
            view.SetSuppressed(blocked);
            if (progress == null)
                return;

            // A prompt whose condition went away (cheat reload, node became locked) is withdrawn.
            if (shown == Lesson.Move && (progress.IsSeen(config.mapMoveId) || !map.CanStepToOtherNode)) { view.Dismiss(); shown = Lesson.None; }
            if (shown == Lesson.Enter && (progress.IsSeen(config.mapEnterId) || !map.CanEnterCurrentNode)) { view.Dismiss(); shown = Lesson.None; }
            if (blocked || shown != Lesson.None || !view.IsIdle || idleFor < config.mapIdleDelay)
                return;

            // Moving first when possible: it leads to the next stage, whose entry lesson then follows.
            if (!progress.IsSeen(config.mapMoveId) && map.CanStepToOtherNode)
                Begin(Lesson.Move, explainedMove, config.mapMoveExplain);
            else if (!progress.IsSeen(config.mapEnterId) && map.CanEnterCurrentNode)
                Begin(Lesson.Enter, explainedEnter, config.mapEnterExplain);
        }

        private void Begin(Lesson lesson, bool alreadyExplained, TutorialExplainPage page)
        {
            if (alreadyExplained || page == null || string.IsNullOrEmpty(page.bodyKey))
            {
                ShowPrompt(lesson);
                return;
            }
            explaining = lesson;
            map.TutorialHold = true;
            runner.Open(new[] { page });
        }

        private void ShowPrompt(Lesson lesson)
        {
            shown = lesson;
            bool move = lesson == Lesson.Move;
            view.Show(new TutorialPromptContent
            {
                textKey = move ? config.mapMoveKey : config.mapEnterKey,
                glyphs = new[] { move ? TutorialGlyph.MapStep : TutorialGlyph.Confirm },
            });
        }

        /// <summary>Enter lesson: the pad the avatar stands on. Move lesson: the open neighbouring pad (else the current one).</summary>
        private SpotShape ResolveSpot(TutorialExplainPage page)
        {
            var overlay = (RectTransform)explainView.transform;
            Vector3 anchor = map.CurrentNodeAnchor;
            if (explaining == Lesson.Move && map.TryGetOpenNeighbourAnchor(out var neighbour))
            {
                if (TutorialSpot.TryWorld(overlay, map.MapCamera, neighbour, page.radius, config.explain.minSpotPixels, out var next))
                    return next;
            }
            if (TutorialSpot.TryWorld(overlay, map.MapCamera, anchor + new Vector3(page.point.x, page.point.y, 0f), page.radius, config.explain.minSpotPixels, out var spot))
                return spot;
            return SpotShape.Circle(Vector2.zero, config.explain.minSpotPixels * 1.5f);
        }
    }
}
