using System;
using System.Linq;
using EloBuddy;
using EloBuddy.SDK;
using EloBuddy.SDK.Constants;
using EloBuddy.SDK.Enumerations;
using EloBuddy.SDK.Events;
using EloBuddy.SDK.Menu;
using EloBuddy.SDK.Menu.Values;
using Color = System.Drawing.Color;
using SharpDX;


namespace InfazLucıan
{
    public class Program
    {
        private static Menu Menu;
        private static AIHeroClient Player = ObjectManager.Player;
        private static HpBarIndicator Indicator = new HpBarIndicator();
        private static bool AAPassive;
        private static Spell.Targeted Q = new Spell.Targeted(SpellSlot.Q, 500) { CastDelay = 250 };
        private static Spell.Skillshot Q1 = new Spell.Skillshot(SpellSlot.Q, 1100, SkillShotType.Linear, 500, int.MaxValue, 50);
        private static Spell.Skillshot W = new Spell.Skillshot(SpellSlot.W, 1000, SkillShotType.Linear, 300, 1600, 80) { AllowedCollisionCount = int.MaxValue };
        private static Spell.Active E = new Spell.Active(SpellSlot.E, 425);
        private static Spell.Skillshot R = new Spell.Skillshot(SpellSlot.R, 1200, SkillShotType.Linear, 200, 2500, 110);


        static void Main()
        {
            Loading.OnLoadingComplete += OnGameLoad;
        }

        static void OnGameLoad(EventArgs args)
        {
            if (Player.ChampionName != "Lucian") return;
            Chat.Print("Infaz Lucian Iyı Oyunlar Dıler Good Game :)");

            OnMenuLoad();


            Spellbook.OnCastSpell += Spellbook_OnCastSpell;
            Game.OnUpdate += Game_OnUpdate;
            Drawing.OnEndScene += Drawing_OnEndScene;
            Obj_AI_Base.OnSpellCast += OnDoCast;
            Drawing.OnDraw += OnDraw;
            Obj_AI_Base.OnSpellCast += OnDoCastLC;
        }

        private static Menu Combo, Misc, Laneclear, HarassMenu, Auto, JungleClear, Drawings, Killsteal;
        private static void OnMenuLoad()
        {
            Menu = MainMenu.AddMenu("Infaz Lucıan", "Infaz");

            Combo = Menu.AddSubMenu("Combo");
            Combo.Add("CQ", new CheckBox("Use Q"));
            Combo.Add("CW", new CheckBox("Use W"));
            StringList(Combo, "CE", "Use E Mode", new[] { "Side", "Cursor", "Enemy", "Never" }, 0);
            Combo.Add("ForceR", new KeyBind("Force R On Target Selector", false, KeyBind.BindTypes.HoldActive, "T".ToCharArray()[0]));

            Misc = Menu.AddSubMenu("Misc");
            Misc.Add("Humanizer", new Slider("Humanizer Delay", 5, 5, 300));
            Misc.Add("Nocolision", new CheckBox("No colision W"));

            HarassMenu = Menu.AddSubMenu("Harass");
            HarassMenu.Add("HEXQ", new CheckBox("Use Extended Q"));
            HarassMenu.Add("HMinMana", new Slider("Extended Q Min Mana (%)", 80));
            HarassMenu.Add("HQ", new CheckBox("Use Q"));
            HarassMenu.Add("HW", new CheckBox("Use W"));
            StringList(HarassMenu, "HE", "Use E Mode", new[] { "Side", "Cursor", "Enemy", "Never" }, 0);
            HarassMenu.Add("HHMinMana", new Slider("Harass Min Mana (%)", 80));

            Laneclear = Menu.AddSubMenu("LaneClear");
            Laneclear.Add("LT", new KeyBind("Use Spell LaneClear (Toggle)", true, KeyBind.BindTypes.PressToggle, "v".ToCharArray()[0]));
            Laneclear.Add("LHQ", new CheckBox("Use Extended Q For Harass"));
            Laneclear.Add("LQ", new Slider("Use Q (0 = Don't)", 0, 0, 5));
            Laneclear.Add("LW", new CheckBox("Use W"));
            Laneclear.Add("LE", new CheckBox("Use E"));
            Laneclear.Add("LMinMana", new Slider("Min Mana (%)", 70));

            JungleClear = Menu.AddSubMenu("JungleClear");
            JungleClear.Add("JQ", new CheckBox("Use Q"));
            JungleClear.Add("JW", new CheckBox("Use W"));
            JungleClear.Add("JE", new CheckBox("Use E"));

            Auto = Menu.AddSubMenu("Auto");
            Auto.Add("AutoQ", new KeyBind("Auto Extended Q (Toggle)", true, KeyBind.BindTypes.PressToggle, "G".ToCharArray()[0]));
            Auto.Add("MinMana", new Slider("Min Mana (%)", 80));

            Drawings = Menu.AddSubMenu("Drawings");
            Drawings.Add("Dind", new CheckBox("Draw Damage Incidator"));
            Drawings.Add("DEQ", new CheckBox("Draw Extended Q", false));
            Drawings.Add("DQ", new CheckBox("Draw Q", true));
            Drawings.Add("DW", new CheckBox("Draw W", false));
            Drawings.Add("DE", new CheckBox("Draw E", false));

            Killsteal = Menu.AddSubMenu("KillSteal");
            Killsteal.Add("KillstealQ", new CheckBox("Killsteal Q"));

        }

        private static bool Getcheckboxvalue(Menu menu, string menuvalue)
        {
            return menu[menuvalue].Cast<CheckBox>().CurrentValue;
        }
        private static bool Getkeybindvalue(Menu menu, string menuvalue)
        {
            return menu[menuvalue].Cast<KeyBind>().CurrentValue;
        }
        private static int Getslidervalue(Menu menu, string menuvalue)
        {
            return menu[menuvalue].Cast<Slider>().CurrentValue;
        }

        public static void StringList(Menu menu, string uniqueId, string displayName, string[] values, int defaultValue)
        {
            var mode = menu.Add(uniqueId, new Slider(displayName, defaultValue, 0, values.Length - 1));
            mode.DisplayName = displayName + ": " + values[mode.CurrentValue];
            mode.OnValueChange +=
                delegate (ValueBase<int> sender, ValueBase<int>.ValueChangeArgs args)
                {
                    sender.DisplayName = displayName + ": " + values[args.NewValue];
                };
        }

        private static void OnDoCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            var spellName = args.SData.Name;
            if (!sender.IsMe || !AutoAttacks.IsAutoAttack(spellName)) return;

            if (args.Target is AIHeroClient)
            {
                var target = (Obj_AI_Base)args.Target;
                if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo) && target.IsValid)
                {
                    Core.DelayAction(() => OnDoCastDelayed(args), Getslidervalue(Misc, "Humanizer"));
                }
                if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Harass) && target.IsValid)
                {
                    Core.DelayAction(() => OnDoCastDelayed(args), Getslidervalue(Misc, "Humanizer"));
                }
            }
            if (args.Target is Obj_AI_Minion)
            {
                if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear) && args.Target.IsValid)
                {
                    Core.DelayAction(() => OnDoCastDelayed(args), Getslidervalue(Misc, "Humanizer"));
                }
            }
        }
        private static void OnDoCastLC(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            var spellName = args.SData.Name;
            if (!sender.IsMe || !AutoAttacks.IsAutoAttack(spellName)) return;

            if (args.Target is Obj_AI_Minion)
            {
                if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear) && args.Target.IsValid)
                {
                    Core.DelayAction(() => OnDoCastDelayedLC(args), Getslidervalue(Misc, "Humanizer"));
                }
            }
        }

        static void killsteal()
        {
            if (Getcheckboxvalue(Killsteal, "KillstealQ") && Q.IsReady())
            {
                var targets = EntityManager.Heroes.Enemies.Where(x => x.IsValidTarget(Q.Range) && !x.IsZombie);
                foreach (var target in targets)
                {
                    if (target.Health < Player.GetSpellDamage(target, SpellSlot.Q) && (!target.HasBuff("kindrednodeathbuff") && !target.HasBuff("Undying Rage") && !target.HasBuff("JudicatorIntervention")))
                        Q.Cast(target);
                }
            }
        }
        private static void OnDoCastDelayedLC(GameObjectProcessSpellCastEventArgs args)
        {
            AAPassive = false;
            if (args.Target is Obj_AI_Minion && args.Target.IsValid)
            {
                if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear) && Player.ManaPercent >= Getslidervalue(Laneclear, "LMinMana"))
                {
                    var Minions =
                        EntityManager.MinionsAndMonsters.Get(EntityManager.MinionsAndMonsters.EntityType.Both,
                            EntityManager.UnitTeam.Enemy, Player.Position, Player.GetAutoAttackRange())
                            .OrderByDescending(i => i.Health).ToList();
                    if (Minions[0].IsValid && Minions.Count != 0)
                    {
                        if (!Getkeybindvalue(Laneclear, "LT")) return;


                        if (Q.IsReady() && (!E.IsReady() || (E.IsReady() && !Getcheckboxvalue(Laneclear, "LE"))) && Getslidervalue(Laneclear, "LQ") != 0 && !AAPassive)
                        {
                            var QMinions = EntityManager.MinionsAndMonsters.GetLaneMinions(
                                EntityManager.UnitTeam.Enemy, Player.Position, Q.Range);
                            var exminions = EntityManager.MinionsAndMonsters.GetLaneMinions(
                                EntityManager.UnitTeam.Enemy, Player.Position, Q1.Range);
                            foreach (var Minion in QMinions)
                            {


                                {
                                    Q.Cast(Minion);
                                    break;
                                }
                            }
                        }
                        if ((!E.IsReady() || (E.IsReady() && !Getcheckboxvalue(Laneclear, "LE"))) && (!Q.IsReady() || (Q.IsReady() && Getslidervalue(Laneclear, "LQ") == 0)) && Getcheckboxvalue(Laneclear, "LW") && W.IsReady() && !AAPassive) W.Cast(Minions[0].Position);
                    }
                }
            }
        }
        public static Vector2 Deviation(Vector2 point1, Vector2 point2, double angle)
        {
            angle *= Math.PI / 180.0;
            Vector2 temp = Vector2.Subtract(point2, point1);
            Vector2 result = new Vector2(0);
            result.X = (float)(temp.X * Math.Cos(angle) - temp.Y * Math.Sin(angle)) / 4;
            result.Y = (float)(temp.X * Math.Sin(angle) + temp.Y * Math.Cos(angle)) / 4;
            result = Vector2.Add(result, point1);
            return result;
        }
        private static void OnDoCastDelayed(GameObjectProcessSpellCastEventArgs args)
        {
            AAPassive = false;
            var @base = args.Target as AIHeroClient;
            if (@base != null)
            {
                var target = (Obj_AI_Base)args.Target;
                if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Combo) && target.IsValid)
                {
                    if (Item.HasItem(ItemId.Youmuus_Ghostblade) && Item.CanUseItem(ItemId.Youmuus_Ghostblade)) Item.UseItem(ItemId.Youmuus_Ghostblade);
                    if (E.IsReady() && !AAPassive && Getslidervalue(Combo, "CE") == 0) EloBuddy.Player.CastSpell(SpellSlot.E, (Deviation(Player.Position.To2D(), target.Position.To2D(), 65).To3D()));
                    if (E.IsReady() && !AAPassive && Getslidervalue(Combo, "CE") == 1) EloBuddy.Player.CastSpell(SpellSlot.E, Game.CursorPos);

                    if (Q.IsReady() && (!E.IsReady() || (E.IsReady() && Getslidervalue(Combo, "CE") == 3)) && Getcheckboxvalue(Combo, "CQ") && !AAPassive) Q.Cast(target);
                    if ((!E.IsReady() || (E.IsReady() && Getslidervalue(Combo, "CE") == 3)) && (!Q.IsReady() || (Q.IsReady() && !Getcheckboxvalue(Combo, "CQ"))) && Getcheckboxvalue(Combo, "CW") && W.IsReady() && !AAPassive) W.Cast(target.Position);
                }
                if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Harass) && target.IsValid)
                {
                    if (Player.ManaPercent < Getslidervalue(HarassMenu, "HHMinMana")) return;

                    if (E.IsReady() && !AAPassive && Getslidervalue(HarassMenu, "HE") == 0) EloBuddy.Player.CastSpell(SpellSlot.E, (Deviation(Player.Position.To2D(), target.Position.To2D(), 65).To3D()));

                    if (Q.IsReady() && (!E.IsReady() || (E.IsReady() && Getslidervalue(HarassMenu, "HE") == 3)) && Getcheckboxvalue(HarassMenu, "HQ") && !AAPassive) Q.Cast(target);
                    if ((!E.IsReady() || (E.IsReady() && Getslidervalue(HarassMenu, "HE") == 3)) && (!Q.IsReady() || (Q.IsReady() && !Getcheckboxvalue(HarassMenu, "HQ")) && Getcheckboxvalue(Combo, "HW") && W.IsReady() && !AAPassive)) W.Cast(target.Position);
                }
            }
            if (args.Target is Obj_AI_Minion && args.Target.IsValid)
            {
                if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.JungleClear))
                {
                    var Mobs =
                        EntityManager.MinionsAndMonsters.GetJungleMonsters(Player.Position, Player.GetAutoAttackRange())
                            .OrderByDescending(i => i.MaxHealth).ToList();
                    if (Mobs[0].IsValid && Mobs.Count != 0)
                    {

                        if (Q.IsReady() && (!E.IsReady() || (E.IsReady() && !Getcheckboxvalue(JungleClear, "JE"))) && Getcheckboxvalue(JungleClear, "JQ") && !AAPassive) Q.Cast(Mobs[0]);
                        if ((!E.IsReady() || (E.IsReady() && !Getcheckboxvalue(JungleClear, "JE"))) && (!Q.IsReady() || (Q.IsReady() && !Getcheckboxvalue(JungleClear, "JQ"))) && Getcheckboxvalue(JungleClear, "JW") && W.IsReady() && !AAPassive) W.Cast(Mobs[0].Position);
                    }
                }
            }
        }

        private static void Harass()
        {
            if (Player.ManaPercent < Getslidervalue(HarassMenu, "HMinMana")) return;

            if (Q.IsReady() && Getcheckboxvalue(HarassMenu, "HEXQ"))
            {
                var target = TargetSelector.GetTarget(Q1.Range, DamageType.Physical);
                var Minions = EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy,
                    Player.Position, Q.Range);
                if (target == null || !target.IsValidTarget(Q1.Range) || Minions == null) return;
                foreach (var Minion in Minions)
                {

                    var QPred = Q1.GetPrediction(target);

                    {
                        Q.Cast(Minion);
                        break;
                    }
                }
            }
        }
        static void LaneClear()
        {
            if (Player.ManaPercent < Getslidervalue(Laneclear, "LMinMana")) return;
            if (Q.IsReady() && Player.ManaPercent >= Getslidervalue(Laneclear, "LMinMana"))
            {
                var extarget = TargetSelector.GetTarget(Q1.Range, DamageType.Physical);
                var Minions = EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy,
                    Player.Position, Q.Range);
                if (extarget != null && extarget.IsValidTarget())
                    foreach (var Minion in Minions)
                    {


                        var QPred = Q1.GetPrediction(extarget);

                        {
                            Q.Cast(Minion);
                            break;
                        }
                    }
            }
        }
        static void AutoUseQ()
        {
            if (Q.IsReady() && Getkeybindvalue(Auto, "AutoQ") && Player.ManaPercent > Getslidervalue(Auto, "MinMana"))
            {
                var extarget = TargetSelector.GetTarget(Q1.Range, DamageType.Physical);
                var Minions = EntityManager.MinionsAndMonsters.GetLaneMinions(EntityManager.UnitTeam.Enemy,
                    Player.Position, Q.Range);
                if (Minions == null || extarget == null || extarget.IsValidTarget()) return;
                foreach (var Minion in Minions)
                {

                    var QPred = Q1.GetPrediction(extarget);

                    {
                        Q.Cast(Minion);
                        break;
                    }
                }
            }
        }

        static void UseRTarget()
        {
            var target = TargetSelector.GetTarget(R.Range, DamageType.Physical);
            if (target != null && Getkeybindvalue(Combo, "ForceR") && R.IsReady() && target.IsValid && !Player.HasBuff("LucianR")) R.Cast(target.Position);
        }
        static void Game_OnUpdate(EventArgs args)
        {
            if (Getcheckboxvalue(Misc, "Nocolision"))
            {
                W.AllowedCollisionCount = 0;
            }
            else
            {
                W.AllowedCollisionCount = int.MaxValue;
            }
            AutoUseQ();

            if (Getkeybindvalue(Combo, "ForceR")) UseRTarget();
            killsteal();
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.Harass)) Harass();
            if (Orbwalker.ActiveModesFlags.HasFlag(Orbwalker.ActiveModes.LaneClear)) LaneClear();
        }
        static void Spellbook_OnCastSpell(Spellbook sender, SpellbookCastSpellEventArgs args)
        {
            if (args.Slot == SpellSlot.Q || args.Slot == SpellSlot.W || args.Slot == SpellSlot.E) AAPassive = true;
            if (args.Slot == SpellSlot.E) Orbwalker.ResetAutoAttack(); ;
            if (args.Slot == SpellSlot.R) Item.UseItem(ItemId.Youmuus_Ghostblade);
        }

        static float getComboDamage(Obj_AI_Base enemy)
        {
            if (enemy != null)
            {
                float damage = 0;
                if (E.IsReady()) damage = damage + (float)Player.GetAutoAttackDamage(enemy) * 2;
                if (W.IsReady()) damage = damage + Player.GetSpellDamage(enemy, SpellSlot.W) + (float)Player.GetAutoAttackDamage(enemy);
                if (Q.IsReady())
                {
                    damage = damage + Player.GetSpellDamage(enemy, SpellSlot.Q) + (float)Player.GetAutoAttackDamage(enemy);
                }
                damage = damage + (float)Player.GetAutoAttackDamage(enemy);

                return damage;
            }
            return 0;
        }

        static void OnDraw(EventArgs args)
        {
            if (Getcheckboxvalue(Drawings, "DEQ")) Drawing.DrawCircle(Player.Position, Q1.Range, Q.IsReady() ? Color.DarkGoldenrod : Color.IndianRed);
            if (Getcheckboxvalue(Drawings, "DQ")) Drawing.DrawCircle(Player.Position, Q.Range, Q.IsReady() ? Color.DarkGoldenrod : Color.IndianRed);
            if (Getcheckboxvalue(Drawings, "DW")) Drawing.DrawCircle(Player.Position, W.Range, W.IsReady() ? Color.DarkGoldenrod : Color.IndianRed);
            if (Getcheckboxvalue(Drawings, "DE")) Drawing.DrawCircle(Player.Position, E.Range, E.IsReady() ? Color.DarkGoldenrod : Color.IndianRed);
        }
        static void Drawing_OnEndScene(EventArgs args)
        {
            if (Getcheckboxvalue(Drawings, "Dind"))
            {
                foreach (
                    var enemy in
                        ObjectManager.Get<AIHeroClient>()
                            .Where(ene => ene.IsValidTarget() && !ene.IsZombie))
                {
                    Indicator.unit = enemy;
                    Indicator.drawDmg(getComboDamage(enemy), new ColorBGRA(255, 204, 0, 160));

                }
            }
        }
    }

    internal class HpBarIndicator
    {
        internal AIHeroClient unit;

        public HpBarIndicator()
        {
        }

        internal void drawDmg(float v, ColorBGRA colorBGRA)
        {
            throw new NotImplementedException();
        }
    }
}