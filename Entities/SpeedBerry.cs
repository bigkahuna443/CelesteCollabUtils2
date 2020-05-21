﻿using Celeste.Mod.CollabUtils2.Triggers;
using Celeste.Mod.CollabUtils2.UI;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.Utils;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Celeste.Mod.CollabUtils2.Entities {

    /// <summary>
    /// A berry that requires you to go fast to collect it.
    /// </summary>
    [CustomEntity("CollabUtils2/SpeedBerry")]
    [Tracked]
    public class SpeedBerry : Strawberry {

        public static SpriteBank SpriteBank;

        public EntityData EntityData;
        public float BronzeTime;
        public float SilverTime;
        public float GoldTime;
        public bool TimeRanOut;

        private static ParticleType P_OrigGoldGlow;
        private static ParticleType P_SilverGlow;
        private static ParticleType P_BronzeGlow;

        public SpeedBerryTimerDisplay TimerDisplay;

        private Sprite sprite;

        private bool transitioned = false;

        public static void LoadContent() {
            SpriteBank = new SpriteBank(GFX.Game, "Graphics/CollabUtils2/SpeedBerry.xml");
        }

        public SpeedBerry(EntityData data, Vector2 offset, EntityID id) : base(data, offset, id) {
            EntityData = data;
            new DynData<Strawberry>(this)["Golden"] = true;
            BronzeTime = data.Float("bronzeTime", 15f);
            SilverTime = data.Float("silverTime", 10f);
            GoldTime = data.Float("goldTime", 5f);
            Follower.PersistentFollow = true;
            var listener = new TransitionListener() {
                OnOutBegin = () => {
                    SceneAs<Level>().Session.DoNotLoad.Add(ID);
                    transitioned = true;
                }
            };
            Add(listener);

            if (P_SilverGlow == null) {
                P_SilverGlow = new ParticleType(P_Glow) {
                    Color = Calc.HexToColor("B3AF9F"),
                    Color2 = Calc.HexToColor("E6DCB5")
                };
                P_BronzeGlow = new ParticleType(P_Glow) {
                    Color = Calc.HexToColor("9A5B3C"),
                    Color2 = Calc.HexToColor("ECAF66")
                };
                P_OrigGoldGlow = P_GoldGlow;
            }
        }

        public override void Awake(Scene scene) {
            Session session = (scene as Level).Session;
            if (!(SaveData.Instance.CheatMode || SaveData.Instance.Areas_Safe[session.Area.ID].Modes[(int) session.Area.Mode].Completed)) {
                // the berry shouldn't spawn
                RemoveSelf();
                return;
            }
            base.Awake(scene);

            sprite = new DynData<Strawberry>(this).Get<Sprite>("sprite");
            if (TimerDisplay != null) {
                string nextRank = TimerDisplay.GetNextRank(out _).ToLowerInvariant();
                if (nextRank != "none") {
                    sprite.Play("idle_" + nextRank);
                }
            }
        }

        public override void Update() {
            Sprite sprite = Get<Sprite>();

            if (Follower.HasLeader) {
                if (TimerDisplay == null) {
                    TimerDisplay = new SpeedBerryTimerDisplay(this);
                    SceneAs<Level>().Add(TimerDisplay);
                }

                if ((Follower.Leader.Entity as Player)?.CollideCheck<SpeedBerryCollectTrigger>() ?? false) {
                    // collect the speed berry!
                    TimerDisplay.EndTimer();
                    OnCollect();
                }

                // show a message about customizing the speed berry timer position when grabbing it for the first time in the save.
                Player player = Scene.Tracker.GetEntity<Player>();
                if (player != null && !CollabModule.Instance.SaveData.SpeedberryOptionMessageShown) {

                    CollabModule.Instance.SaveData.SpeedberryOptionMessageShown = true;
                    Scene.Add(new DialogCutscene("collabutils2_speedberry_optionmessage", player, false));
                }
            }

            if (TimerDisplay != null) {
                if (BronzeTime < TimeSpan.FromTicks(TimerDisplay.GetSpentTime()).TotalSeconds) {
                    // Time ran out
                    TimeRanOut = true;
                } else {
                    string nextRank = TimerDisplay.GetNextRank(out float nextTime).ToLowerInvariant();
                    // the berry is close to exploding, time is over in 1.2 seconds. Start the exploding animation
                    if (nextRank == "bronze" && sprite.CurrentAnimationID != "explosion"
                        && TimeSpan.FromTicks(TimerDisplay.GetSpentTime()).TotalMilliseconds + 1200 > nextTime * 1000) {

                        sprite.Play("explosion");
                    }

                    // the current animation does not match the expected animation.
                    if (sprite.CurrentAnimationID != "explosion" && !sprite.CurrentAnimationID.Contains(nextRank)) {
                        sprite.Play("transition_to_" + nextRank);
                    }

                    if (nextRank == "bronze") {
                        P_GoldGlow = P_BronzeGlow;
                    } else if (nextRank == "silver") {
                        P_GoldGlow = P_SilverGlow;
                    }
                }
            }

            if (TimeRanOut) {
                dissolve();
            }

            if (transitioned) {
                transitioned = false;
                TimerDisplay?.StartTimer();
            }

            base.Update();

            P_GoldGlow = P_OrigGoldGlow;
        }

        private void dissolve() {
            if (Follower.Leader != null) {
                Player player = Follower.Leader.Entity as Player;
                player.StrawberryCollectResetTimer = 2.5f;
                Add(new Coroutine(dissolveRoutine(player), true));
            } else {
                Add(new Coroutine(dissolveRoutine(null), true));
            }

        }

        private IEnumerator dissolveRoutine(Player follower) {
            Sprite sprite = Get<Sprite>();
            Level level = Scene as Level;
            Session session = level.Session;
            session.DoNotLoad.Remove(ID);
            Collidable = false;
            // sprite.Scale = Vector2.One * 0.5f;
            if (follower != null) {
                foreach (Player player in Scene.Tracker.GetEntities<Player>()) {
                    player.Die(Vector2.Zero, true, true);
                }
            }
            yield return 0.05f;
            Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
            /* for (int i = 0; i < 6; i++) {
                float dir = Calc.Random.NextFloat(6.28318548f);
                level.ParticlesFG.Emit(StrawberrySeed.P_Burst, 1, Position + Calc.AngleToVector(dir, 4f), Vector2.Zero, dir);
            } */
            SceneAs<Level>().Displacement.AddBurst(Position, 0.5f, 8f, 100f);
            // sprite.Scale = Vector2.Zero;
            Visible = false;
            RemoveSelf();
            yield break;
        }
    }
}
