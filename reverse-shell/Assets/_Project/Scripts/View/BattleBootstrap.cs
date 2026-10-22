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
        [Tooltip("Scene \"Leave Fight\" goes to when this battle was opened directly (not launched from another scene). " +
                 "Empty or not in Build Settings: leaving stops Play mode in the editor.")]
        public string leaveScene = "Overworld_Mossline";

        public BattleController Controller { get; private set; }
        public BattleSetup ActiveSetup { get; private set; }

        /// <summary>True when launched from another scene that wants the result back.</summary>
        public bool HasReturn { get { return request != null && request.OnComplete != null; } }

        /// <summary>Set when the fight ends: outcome, XP and level-ups, shown on the result screen.</summary>
        public BattleResult Result { get; private set; }

        /// <summary>Party progress the fight started from (null when run standalone: everyone is level 1).</summary>
        System.Collections.Generic.Dictionary<CharacterDefinition, MemberProgress> StartParty
        {
            get { return request != null ? request.Party : null; }
        }

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
            Controller.ApplyParty(StartParty); // restarts begin from the same levels and HP
            Result = null;
            Controller.BattleEnded += victory =>
            {
                lastVictory = victory;
                Result = BattleResult.Build(victory ? BattleOutcome.Victory : BattleOutcome.Defeat, Controller, StartParty);
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

        /// <summary>Pause menu "Leave Fight": back to the caller as a retreat, or out to <see cref="leaveScene"/>.</summary>
        public void Leave()
        {
            if (HasReturn)
            {
                Finish(BattleOutcome.Fled);
                return;
            }
            Time.timeScale = 1f;
            if (!string.IsNullOrEmpty(leaveScene) && Application.CanStreamedLevelBeLoaded(leaveScene))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(leaveScene);
                return;
            }
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Finish(BattleOutcome outcome)
        {
            Time.timeScale = 1f;
            var callback = request.OnComplete;
            var result = outcome == BattleOutcome.Fled || Result == null ? BattleResult.Build(outcome, Controller, StartParty) : Result;
            request = null;
            callback(result);
        }

        static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }
}
