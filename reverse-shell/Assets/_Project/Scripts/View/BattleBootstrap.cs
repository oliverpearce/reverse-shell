using UnityEngine;
using UnityEngine.EventSystems;

namespace Combat.View
{
    /// <summary>
    /// Drop on an empty GameObject, assign a BattleSetup, press Play. The whole UI is built in code.
    /// When another scene launched the fight through <see cref="BattleLauncher"/>, that request's setup
    /// is used instead, and the result screen / pause menu return to the caller.
    /// </summary>
    public class BattleBootstrap : MonoBehaviour
    {
        public BattleSetup setup;

        public BattleController Controller { get; private set; }
        public BattleSetup ActiveSetup { get; private set; }

        /// <summary>True when launched from another scene that wants the result back.</summary>
        public bool HasReturn { get { return request != null && request.OnComplete != null; } }

        BattleView view;
        BattleRequest request;
        bool lastVictory;

        void Start()
        {
            request = BattleLauncher.Consume();
            Begin();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f; // never leave the game paused
        }

        /// <summary>Starts (or restarts) the fight from the active setup.</summary>
        public void Begin()
        {
            Time.timeScale = 1f;
            ActiveSetup = request != null && request.Setup != null ? request.Setup : setup;
            if (ActiveSetup == null)
            {
                Debug.LogError("BattleBootstrap: no BattleSetup assigned. Run Tools > Combat > Generate Sample Content.", this);
                return;
            }

            EnsureEventSystem();
            if (view != null) Destroy(view.gameObject);

            Controller = BattleController.FromSetup(ActiveSetup);
            Controller.BattleEnded += victory =>
            {
                lastVictory = victory;
                Debug.Log("Battle ended: " + (victory ? "Victory" : "Defeat"));
            };
            view = BattleView.Create(this, Controller);
            Controller.StartBattle();
        }

        /// <summary>Result screen button: return to the caller, or replay when run standalone.</summary>
        public void OnResultButton()
        {
            if (!HasReturn)
            {
                Begin();
                return;
            }
            Finish(lastVictory ? BattleOutcome.Victory : BattleOutcome.Defeat);
        }

        /// <summary>Pause menu "Leave Fight": back to the caller as a retreat.</summary>
        public void Leave()
        {
            if (HasReturn) Finish(BattleOutcome.Fled);
        }

        void Finish(BattleOutcome outcome)
        {
            Time.timeScale = 1f;
            var callback = request.OnComplete;
            request = null;
            callback(outcome);
        }

        static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
