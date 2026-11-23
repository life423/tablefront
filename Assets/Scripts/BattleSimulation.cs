using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tablefront
{
    public enum UnitKind { Scout, Rifle, Tank, Gunner, Artillery }

    [Serializable]
    public sealed class Army
    {
        public int Id;
        public string Name;
        public float Credits, Score, BaseHealth;
        public Vector2 BasePosition;
        public bool IsAI;
        public float RecruitCooldown;
        public string Intent;
        public int UnitsLost, UnitsDestroyed, BeaconsCaptured, ReinforcementsDeployed;
        public float BeaconSeconds;
        public bool Alive => BaseHealth > 0f;
        internal float DecisionTimer, TurretTimer, SinceDamage;
        internal int Recruited;
    }

    [Serializable]
    public sealed class Troop
    {
        public int Id, Team;
        public UnitKind Kind;
        public Vector2 Position, Destination;
        public float Health, MaxHealth, Heading, ShotFlash;
        public bool HasOrder;
        public bool HoldPosition;
        public bool WaitingForBridge;
        public int AttackTargetId = -1;
        public bool Alive => Health > 0f;
        internal float WeaponTimer;
        internal bool AttackMove = true;
        internal int BaseTargetTeam = -1;
    }

    [Serializable]
    public sealed class ControlPoint
    {
        public int Id, Owner = -1, CapturingTeam = -1;
        public string Name;
        public Vector2 Position;
        public float CaptureProgress;
        public bool Contested;
    }

    [Serializable]
    public sealed class CoverZone
    {
        public string Name;
        public Vector2 Position;
        public float Radius;
    }

    public struct CombatEvent
    {
        public Vector2 From, To;
        public int Team, ShooterId;
        public UnitKind WeaponKind;
        public bool Heavy, Killed;
    }

    /// <summary>
    /// Deterministic, offline RTS rules. No scene objects, input, or wall-clock dependency.
    /// AI commanders issue the same movement orders and pay the same recruitment costs as a human.
    /// </summary>
    public sealed class BattleSimulation
    {
        public const float MapHalfSize = 25f;
        public const int UnitCap = 26;
        public const float VictoryScore = 350f;
        public const float BaseMaxHealth = 1200f;
        public const float CaptureRadius = 3.3f;
        public const int ObservationCount = 40;
        public const int ActionCount = 10;
        public const float ScoutCaptureWeight = 1.5f;
        public const float RifleCoverDamageMultiplier = .65f;
        public const float TankArmorDamageMultiplier = 1.2f;
        public const float ArtilleryMinimumRange = 4.5f;
        public const float MissionHoldRequired = 45f;
        public static readonly UnitKind[] UnitKinds = { UnitKind.Scout, UnitKind.Rifle, UnitKind.Tank, UnitKind.Gunner, UnitKind.Artillery };

        public BattlefieldDefinition Battlefield { get; }
        public bool MissionActive => Battlefield.Id == BattlefieldId.BridgeGambit;
        public TablefrontBridge Bridge { get; }
        public string MissionWinReason { get; private set; } = "";
        /// <summary>Current skirmishes enable the five-unit role adapter; old trainers/replays default to the original three.</summary>
        public bool ExpandedRoster;

        public readonly List<Army> Armies = new List<Army>(3);
        public readonly List<Troop> Units = new List<Troop>(80);
        public readonly List<ControlPoint> Points = new List<ControlPoint>(3);
        public readonly List<CombatEvent> Events = new List<CombatEvent>(80);
        public readonly List<CoverZone> CoverZones = new List<CoverZone>(6);
        public string LastTurningPoint { get; private set; } = "The opening deployment";
        public float LastTurningPointTime { get; private set; }
        public float Elapsed;
        public int Winner = -1;
        public bool Finished;
        /// <summary>Training harness sets this to true and supplies actions directly.</summary>
        public bool UseExternalAI;
        /// <summary>Explicit teaching table only; ordinary matches and trainers keep normal victory rules.</summary>
        public bool PracticeMode;

        readonly System.Random random;
        readonly System.Random[] policyRandom = new System.Random[3];
        readonly List<Hit> pendingHits = new List<Hit>(90);
        readonly List<int> orderScratch = new List<int>(30);
        readonly int[] captureCounts = new int[3];
        readonly float[] captureWeights = new float[3];
        readonly float[] missionHold = new float[2];
        readonly float[] bridgeDecisionTimers = new float[2];
        int nextId = 1, stepNumber;

        struct Hit
        {
            public int TargetId, BaseTeam, AttackerTeam, ShooterId;
            public UnitKind WeaponKind;
            public float Damage;
            public Vector2 From, To;
            public bool Heavy;
        }

        struct Stats
        {
            public float Health, Speed, Range, Damage, Delay, Radius;
            public Stats(float health, float speed, float range, float damage, float delay, float radius)
            { Health = health; Speed = speed; Range = range; Damage = damage; Delay = delay; Radius = radius; }
        }

        static Stats Specifications(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Scout: return new Stats(65f, 4.15f, 4.8f, 6.5f, .58f, .42f);
                case UnitKind.Tank: return new Stats(330f, 1.95f, 8.2f, 32f, 1.7f, .86f);
                case UnitKind.Gunner: return new Stats(145f, 2.45f, 5.8f, 9f, .32f, .52f);
                case UnitKind.Artillery: return new Stats(125f, 1.5f, 12.2f, 40f, 2.5f, .75f);
                default: return new Stats(115f, 2.85f, 6.4f, 11.5f, .82f, .48f);
            }
        }

        public static int Cost(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Scout: return 55;
                case UnitKind.Tank: return 170;
                case UnitKind.Gunner: return 115;
                case UnitKind.Artillery: return 190;
                default: return 85;
            }
        }

        public static float WeaponRange(UnitKind kind) => Specifications(kind).Range;
        public static float UnitRadius(UnitKind kind) => Specifications(kind).Radius;
        public static float MinimumWeaponRange(UnitKind kind) => kind == UnitKind.Artillery ? ArtilleryMinimumRange : 0f;

        public static string UnitName(UnitKind kind, bool plural = false)
        {
            switch (kind)
            {
                case UnitKind.Scout: return plural ? "Scouts" : "Scout";
                case UnitKind.Rifle: return plural ? "Rifles" : "Rifle";
                case UnitKind.Tank: return plural ? "Tanks" : "Tank";
                case UnitKind.Gunner: return plural ? "Gunners" : "Gunner";
                case UnitKind.Artillery: return "Artillery";
                default: return "Unit";
            }
        }

        public BattleSimulation(bool spectator, int seed = 42, BattlefieldId battlefield = BattlefieldId.Greenfield)
        {
            Battlefield = TablefrontBattlefields.Get(battlefield);
            random = new System.Random(seed);
            for (int i = 0; i < 3; i++) policyRandom[i] = new System.Random(unchecked(seed * 7919 + i * 104729));
            Armies.Add(NewArmy(0, "COBALT", Battlefield.BasePositions[0], spectator));
            Armies.Add(NewArmy(1, "EMBER", Battlefield.BasePositions[1], true));
            Armies.Add(NewArmy(2, "VIOLET", Battlefield.BasePositions[2], true));
            for (int i = 0; i < 3; i++)
                Points.Add(new ControlPoint { Id = i, Name = Battlefield.PointNames[i], Position = Battlefield.PointPositions[i] });
            // Cover is low, passable terrain, not a pathfinding obstacle. Its flanking
            // positions overlap the outside of each capture ring rather than its centre.
            foreach (var point in Points)
            {
                var radial = point.Position.normalized;
                var flank = new Vector2(-radial.y, radial.x);
                CoverZones.Add(new CoverZone { Name = point.Name + " left cover", Position = point.Position - flank * Battlefield.CoverOffset, Radius = Battlefield.CoverRadius });
                CoverZones.Add(new CoverZone { Name = point.Name + " right cover", Position = point.Position + flank * Battlefield.CoverOffset, Radius = Battlefield.CoverRadius });
            }
            foreach (Army army in Armies)
            {
                Spawn(army, UnitKind.Scout);
                Spawn(army, UnitKind.Rifle);
                Spawn(army, UnitKind.Rifle);
                Spawn(army, UnitKind.Tank);
                Spawn(army, UnitKind.Rifle);
                Spawn(army, UnitKind.Scout);
            }
            if (MissionActive)
            {
                Bridge = new TablefrontBridge();
                Armies[2].BaseHealth = 0; Armies[2].IsAI = false; Armies[2].Credits = 0; Armies[2].Intent = "Not deployed";
                Units.RemoveAll(unit => unit.Team == 2);
                Points[0].Owner = Points[0].CapturingTeam = 0; Points[0].CaptureProgress = 1;
                Points[1].Owner = Points[1].CapturingTeam = 1; Points[1].CaptureProgress = 1;
                CoverZones.Clear();
                CoverZones.Add(new CoverZone { Name = "Cobalt north cover", Position = new Vector2(-11, 12), Radius = 2.2f });
                CoverZones.Add(new CoverZone { Name = "Cobalt south cover", Position = new Vector2(-8, -12), Radius = 2.2f });
                CoverZones.Add(new CoverZone { Name = "Cobalt depot cover", Position = new Vector2(-15, -5), Radius = 2.2f });
                CoverZones.Add(new CoverZone { Name = "Ember south cover", Position = new Vector2(11, -12), Radius = 2.2f });
                CoverZones.Add(new CoverZone { Name = "Ember north cover", Position = new Vector2(8, 12), Radius = 2.2f });
                CoverZones.Add(new CoverZone { Name = "Ember depot cover", Position = new Vector2(15, 5), Radius = 2.2f });
                foreach (var unit in Units) unit.Position = unit.Destination = Bridge.ProjectDestination(unit.Position, Specifications(unit.Kind).Radius);
            }
        }

        public float MissionHoldSeconds(int team) => team >= 0 && team < 2 ? missionHold[team] : 0;
        public bool DestinationAllowed(Vector2 location, UnitKind kind = UnitKind.Rifle) =>
            !MissionActive || Bridge.IsWalkable(location, UnitRadius(kind));
        public bool TryRequestBridge(int team, BridgeRoute target)
        {
            if (!MissionActive || PracticeMode || Finished || team < 0 || team > 1 || !Armies[team].Alive) return false;
            bool accepted = Bridge.TryRequest(team, target, Units);
            if (accepted) RecordTurningPoint(Armies[team].Name + " ordered the " + target.ToString().ToLowerInvariant() + " bridge route");
            return accepted;
        }

        Army NewArmy(int id, string name, Vector2 position, bool ai)
        {
            return new Army
            {
                Id = id, Name = name, BasePosition = position, BaseHealth = BaseMaxHealth,
                Credits = 170f, IsAI = ai, Intent = ai ? "Mobilizing squads" : "Awaiting your orders",
                DecisionTimer = .15f + id * .09f, SinceDamage = 10f
            };
        }

        public int OwnedPoints(int team)
        {
            int count = 0;
            for (int i = 0; i < Points.Count; i++) if (Points[i].Owner == team) count++;
            return count;
        }

        public int UnitCount(int team)
        {
            int count = 0;
            for (int i = 0; i < Units.Count; i++) if (Units[i].Alive && Units[i].Team == team) count++;
            return count;
        }

        public bool IsInCover(Vector2 position)
        {
            if (!Finite(position)) return false;
            for (int i = 0; i < CoverZones.Count; i++)
            {
                CoverZone zone = CoverZones[i];
                if ((position - zone.Position).sqrMagnitude <= zone.Radius * zone.Radius) return true;
            }
            return false;
        }

        public bool IsProtected(Troop troop)
        {
            return troop != null && troop.Alive && troop.Kind == UnitKind.Rifle && IsInCover(troop.Position);
        }

        public static string RoleDescription(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Scout: return "Fast flanker / quicker capture";
                case UnitKind.Tank: return "Heavy gun / anti-armour / splash";
                case UnitKind.Gunner: return "Sustained anti-infantry fire / weak against tanks";
                case UnitKind.Artillery: return "Long-range splash / cannot fire within 4.5m";
                default: return "Line infantry / protected in cover";
            }
        }

        public bool Recruit(int team, UnitKind kind)
        {
            if (Finished || team < 0 || team >= Armies.Count || (int)kind < 0 || (int)kind >= UnitKinds.Length) return false;
            if (MissionActive && (kind == UnitKind.Gunner || kind == UnitKind.Artillery)) return false;
            Army army = Armies[team];
            int cost = Cost(kind);
            if (!army.Alive || army.RecruitCooldown > 0f || army.Credits + .001f < cost || UnitCount(team) >= UnitCap) return false;
            army.Credits -= cost;
            army.RecruitCooldown = kind == UnitKind.Artillery ? 4f : kind == UnitKind.Tank ? 3.5f : 2f;
            Spawn(army, kind);
            army.ReinforcementsDeployed++;
            return true;
        }

        /// <summary>
        /// Strategic policy action. 0..2: send army to point; 3/4: attack cyclic enemy HQ;
        /// 5: defend own HQ; 6..8: paid reconnaissance/infantry/armour recruit; 9: keep current orders.
        /// This is an action vocabulary, not a rule-based choice of what the commander should do.
        /// </summary>
        public void ApplyAction(int team, int action)
        {
            if (Finished || team < 0 || team >= Armies.Count || !Armies[team].Alive || action < 0 || action >= ActionCount) return;
            Army army = Armies[team];
            if (action >= 6 && action <= 8)
            {
                UnitKind kind = ReinforcementForAction(team, action);
                bool recruited = Recruit(team, kind);
                army.Intent = recruited ? "Deploying " + kind.ToString().ToLowerInvariant() : "Saving for " + kind.ToString().ToLowerInvariant();
                return;
            }
            if (action == 9) return;
            orderScratch.Clear();
            for (int i = 0; i < Units.Count; i++)
                if (Units[i].Alive && Units[i].Team == team) orderScratch.Add(Units[i].Id);
            // Copy because OrderMove owns the reusable order scratch list.
            int[] ids = orderScratch.ToArray();
            if (action <= 2)
            {
                OrderMove(ids, Points[action].Position);
                army.Intent = "Securing " + Points[action].Name;
            }
            else if (action <= 4)
            {
                int opponent = (team + action - 2) % 3;
                if (!Armies[opponent].Alive) { army.Intent = "Holding position"; return; }
                OrderMove(ids, Armies[opponent].BasePosition);
                for (int i = 0; i < Units.Count; i++)
                    if (Units[i].Alive && Units[i].Team == team) Units[i].BaseTargetTeam = opponent;
                army.Intent = "Assaulting " + Armies[opponent].Name + " HQ";
            }
            else
            {
                Vector2 towardCentre = -army.BasePosition.normalized;
                OrderMove(ids, army.BasePosition + towardCentre * 4.5f);
                army.Intent = "Defending headquarters";
            }
        }

        UnitKind ReinforcementForAction(int team, int action)
        {
            UnitKind original = (UnitKind)(action - 6);
            if (!ExpandedRoster || PracticeMode || MissionActive) return original;
            int rifles = 0, gunners = 0, tanks = 0, artillery = 0;
            foreach (var unit in Units)
            {
                if (!unit.Alive || unit.Team != team) continue;
                if (unit.Kind == UnitKind.Rifle) rifles++;
                else if (unit.Kind == UnitKind.Gunner) gunners++;
                else if (unit.Kind == UnitKind.Tank) tanks++;
                else if (unit.Kind == UnitKind.Artillery) artillery++;
            }
            // The frozen policy chooses the strategic recruitment role. This explicit,
            // deterministic adapter supplies new units without pretending it learned them.
            if (action == 7 && rifles >= 2 && gunners * 2 < rifles) return UnitKind.Gunner;
            if (action == 8 && tanks >= 1 && artillery * 2 < tanks) return UnitKind.Artillery;
            return original;
        }

        /// <summary>
        /// Fixed normalized features shared by training and inference:
        /// 0..7 own HQ health, credits, score, total/scout/infantry/heavy counts, recruit-ready;
        /// expanded Gunners aggregate into infantry and Artillery into heavy, without adding features;
        /// 8..11 first cyclic enemy HQ health/score/count/credits; 12..15 second enemy;
        /// 16..36 seven per point: self/enemy1/enemy2 owner flags, signed capture progress,
        /// nearby friendly/enemy strength counts, nearest friendly distance;
        /// 37 own-HQ hostile count; 38..39 own army world centroid x/z.
        /// </summary>
        public float[] GetObservation(int team)
        {
            var observation = new float[ObservationCount];
            if (team < 0 || team >= Armies.Count) return observation;
            Army army = Armies[team];
            int enemy1 = (team + 1) % 3, enemy2 = (team + 2) % 3;
            int count = 0, scouts = 0, rifles = 0, tanks = 0, intruders = 0;
            Vector2 centroid = Vector2.zero;
            for (int i = 0; i < Units.Count; i++)
            {
                Troop unit = Units[i];
                if (!unit.Alive) continue;
                if (unit.Team == team)
                {
                    count++; centroid += unit.Position;
                    if (unit.Kind == UnitKind.Scout) scouts++;
                    else if (unit.Kind == UnitKind.Rifle || unit.Kind == UnitKind.Gunner) rifles++;
                    else tanks++;
                }
                else if ((unit.Position - army.BasePosition).sqrMagnitude < 121f) intruders++;
            }
            observation[0] = Mathf.Clamp01(army.BaseHealth / BaseMaxHealth);
            observation[1] = Mathf.Clamp01(army.Credits / 300f);
            observation[2] = Mathf.Clamp01(army.Score / VictoryScore);
            observation[3] = count / (float)UnitCap;
            observation[4] = scouts / (float)UnitCap;
            observation[5] = rifles / (float)UnitCap;
            observation[6] = tanks / (float)UnitCap;
            observation[7] = army.RecruitCooldown <= 0f ? 1f : 0f;
            for (int enemy = 0; enemy < 2; enemy++)
            {
                Army opponent = Armies[enemy == 0 ? enemy1 : enemy2];
                int offset = 8 + enemy * 4;
                observation[offset] = Mathf.Clamp01(opponent.BaseHealth / BaseMaxHealth);
                observation[offset + 1] = Mathf.Clamp01(opponent.Score / VictoryScore);
                observation[offset + 2] = UnitCount(opponent.Id) / (float)UnitCap;
                observation[offset + 3] = Mathf.Clamp01(opponent.Credits / 300f);
            }
            for (int p = 0; p < Points.Count; p++)
            {
                ControlPoint point = Points[p];
                int offset = 16 + p * 7;
                observation[offset] = point.Owner == team ? 1f : 0f;
                observation[offset + 1] = point.Owner == enemy1 ? 1f : 0f;
                observation[offset + 2] = point.Owner == enemy2 ? 1f : 0f;
                observation[offset + 3] = point.CaptureProgress * (point.CapturingTeam == team ? 1f : point.CapturingTeam < 0 ? 0f : -1f);
                int friendly = 0, hostile = 0;
                float nearest = 50f;
                for (int i = 0; i < Units.Count; i++)
                {
                    Troop unit = Units[i];
                    if (!unit.Alive) continue;
                    float distance = Vector2.Distance(unit.Position, point.Position);
                    if (unit.Team == team) { nearest = Mathf.Min(nearest, distance); if (distance < 5f) friendly++; }
                    else if (distance < 5f) hostile++;
                }
                observation[offset + 4] = Mathf.Clamp01(friendly / 12f);
                observation[offset + 5] = Mathf.Clamp01(hostile / 12f);
                observation[offset + 6] = Mathf.Clamp01(nearest / 50f);
            }
            observation[37] = Mathf.Clamp01(intruders / 12f);
            if (count > 0) centroid /= count;
            observation[38] = centroid.x / 24f;
            observation[39] = centroid.y / 24f;
            return observation;
        }

        void Spawn(Army army, UnitKind kind)
        {
            Stats stats = Specifications(kind);
            // A fan facing the centre keeps new recruits outside the physical HQ footprint.
            float centre = Mathf.Atan2(-army.BasePosition.y, -army.BasePosition.x);
            int slot = army.Recruited++;
            float angle = centre + ((slot % 7) - 3) * .19f;
            float distance = 3.1f + (slot / 7 % 2) * .8f;
            Vector2 position = Bound(army.BasePosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance);
            var unit = new Troop
            {
                Id = nextId++, Team = army.Id, Kind = kind, Position = position, Destination = position,
                Health = stats.Health, MaxHealth = stats.Health,
                Heading = Mathf.Atan2(-position.x, -position.y) * Mathf.Rad2Deg,
                WeaponTimer = (float)random.NextDouble() * .4f
            };
            Units.Add(unit);
        }

        public void OrderMove(IEnumerable<int> ids, Vector2 target, bool attackMove = true)
        {
            if (Finished || ids == null || !Finite(target)) return;
            orderScratch.Clear();
            foreach (int id in ids) if (!orderScratch.Contains(id)) orderScratch.Add(id);
            int count = orderScratch.Count;
            if (count == 0) return;
            target = Bound(target);
            int width = Mathf.CeilToInt(Mathf.Sqrt(count));
            int rows = Mathf.CeilToInt(count / (float)width);
            for (int slot = 0; slot < count; slot++)
            {
                Troop unit = FindUnit(orderScratch[slot]);
                if (unit == null || !unit.Alive) continue;
                Vector2 offset = new Vector2((slot % width - (width - 1) * .5f) * 1.45f,
                    (slot / width - (rows - 1) * .5f) * 1.45f);
                unit.Destination = Bound(target + offset);
                if (MissionActive) unit.Destination = Bridge.ProjectDestination(unit.Destination, Specifications(unit.Kind).Radius);
                unit.HasOrder = true;
                unit.WaitingForBridge = false;
                unit.AttackMove = attackMove;
                unit.HoldPosition = false;
                unit.AttackTargetId = -1;
                unit.BaseTargetTeam = -1;
            }
        }

        public void OrderAttack(IEnumerable<int> ids, int targetId)
        {
            if (Finished || ids == null) return;
            Troop target = FindUnit(targetId);
            if (target == null || !target.Alive) return;
            foreach (int id in ids)
            {
                Troop unit = FindUnit(id);
                if (unit == null || !unit.Alive || unit.Team == target.Team) continue;
                unit.AttackTargetId = targetId;
                unit.HasOrder = true;
                unit.AttackMove = true;
                unit.HoldPosition = false;
                unit.WaitingForBridge = false;
                unit.BaseTargetTeam = -1;
                unit.Destination = target.Position;
            }
        }

        public void OrderStop(IEnumerable<int> ids, bool hold = false)
        {
            if (Finished || ids == null) return;
            foreach (int id in ids)
            {
                Troop unit = FindUnit(id);
                if (unit == null || !unit.Alive) continue;
                unit.Destination = unit.Position;
                unit.HasOrder = false; unit.HoldPosition = hold;
                unit.WaitingForBridge = false;
                unit.AttackTargetId = unit.BaseTargetTeam = -1;
                unit.AttackMove = true;
            }
        }

        public void Step(float dt)
        {
            Events.Clear();
            if (Finished || float.IsNaN(dt) || float.IsInfinity(dt) || dt <= 0f) return;
            // Catch-up callers may supply a large delta; substeps retain targeting/capture fairness.
            float remaining = Mathf.Min(dt, 2f);
            while (remaining > .00001f && !Finished)
            {
                float slice = Mathf.Min(remaining, .05f);
                StepSlice(slice);
                remaining -= slice;
            }
        }

        void StepSlice(float dt)
        {
            Elapsed += dt;
            stepNumber++;
            pendingHits.Clear();
            if (MissionActive) Bridge.Step(dt, Units);
            for (int i = 0; i < Armies.Count; i++)
            {
                Army army = Armies[i];
                if (!army.Alive) continue;
                army.RecruitCooldown = Mathf.Max(0f, army.RecruitCooldown - dt);
                army.Credits = Mathf.Min(999f, army.Credits + (6f + 3f * OwnedPoints(i)) * dt);
                army.SinceDamage += dt;
                if (army.SinceDamage > 12f) army.BaseHealth = Mathf.Min(BaseMaxHealth, army.BaseHealth + dt * 2.5f);
                army.DecisionTimer -= dt;
                if (army.IsAI && army.DecisionTimer <= 0f)
                {
                    army.DecisionTimer = 1.65f;
                    if (!UseExternalAI)
                    {
                        int action = NeuralCommander.Decide(GetObservation(army.Id), policyRandom[army.Id]);
                        if (action >= 0) ApplyAction(army.Id, action);
                        else army.Intent = "Neural policy unavailable";
                    }
                }
                if (!PracticeMode) Turret(army, dt);
            }
            if (MissionActive && !UseExternalAI) UpdateBridgeCommanders(dt);

            // Rotate the first mover; hits are resolved together after all units have fired.
            int count = Units.Count;
            for (int pass = 0; pass < count; pass++)
            {
                Troop unit = Units[(pass + stepNumber) % count];
                if (unit.Alive) UpdateUnit(unit, dt);
            }
            ResolveHits();
            for (int i = 0; i < Points.Count; i++) UpdatePoint(Points[i], dt);
            if (MissionActive)
                for (int team = 0; team < 2; team++)
                {
                    ControlPoint depot = Points[1 - team];
                    missionHold[team] = Armies[team].Alive && depot.Owner == team && !depot.Contested &&
                        (depot.CapturingTeam < 0 || depot.CapturingTeam == team) ? missionHold[team] + dt : 0;
                }
            for (int i = Units.Count - 1; i >= 0; i--) if (!Units[i].Alive) Units.RemoveAt(i);
            CheckVictory();
        }

        void UpdateBridgeCommanders(float dt)
        {
            // Prototype terrain control, deliberately separate from the frozen neural macro policy.
            // Evaluate only actual cross-bank orders and spend the same resources as a player.
            for (int team = 0; team < 2; team++)
            {
                bridgeDecisionTimers[team] -= dt;
                if (bridgeDecisionTimers[team] > 0 || !Armies[team].Alive || !Armies[team].IsAI || Elapsed < 8) continue;
                bridgeDecisionTimers[team] = 3f;
                if (Bridge.Phase != BridgePhase.Stable || Bridge.CooldownRemaining(team) > 0 || Bridge.CommandsRemaining(team) <= 0) continue;
                BridgeRoute alternative = Bridge.CurrentRoute == BridgeRoute.North ? BridgeRoute.South : BridgeRoute.North;
                float current = 0, changed = 0;
                int relevant = 0;
                foreach (var unit in Units)
                {
                    if (!unit.Alive || unit.Team != team || !unit.HasOrder || unit.HoldPosition ||
                        Mathf.Abs(unit.Position.x) < TablefrontBridge.RiverHalfWidth + UnitRadius(unit.Kind) ||
                        unit.Position.x * unit.Destination.x >= 0) continue;
                    relevant++;
                    current += Bridge.RouteCost(unit.Position, unit.Destination, UnitRadius(unit.Kind), Bridge.CurrentRoute);
                    changed += Bridge.RouteCost(unit.Position, unit.Destination, UnitRadius(unit.Kind), alternative);
                }
                if (relevant > 0 && current - changed > relevant * 3f) TryRequestBridge(team, alternative);
            }
        }

        void UpdateUnit(Troop unit, float dt)
        {
            Stats stats = Specifications(unit.Kind);
            float minimumRange = MinimumWeaponRange(unit.Kind);
            unit.ShotFlash = Mathf.Max(0f, unit.ShotFlash - dt * 5f);
            unit.WeaponTimer = Mathf.Max(0f, unit.WeaponTimer - dt);
            Army home = Armies[unit.Team];
            if (!home.Alive) { unit.Health = 0f; return; }
            if (MissionActive && Bridge.IsRiding(unit.Id)) return;
            unit.WaitingForBridge = false;
            if ((unit.Position - home.BasePosition).sqrMagnitude < 21f && home.SinceDamage > 3f)
                unit.Health = Mathf.Min(unit.MaxHealth, unit.Health + dt * 3f);

            Troop target = null;
            if (unit.AttackTargetId >= 0)
            {
                target = FindUnit(unit.AttackTargetId);
                if (target == null || !target.Alive || target.Team == unit.Team)
                { unit.AttackTargetId = -1; target = null; }
                else unit.Destination = target.Position;
            }
            if (target == null && (unit.AttackMove || !unit.HasOrder))
                target = NearestEnemy(unit.Position, unit.Team,
                    stats.Range + (unit.HoldPosition ? 0f : unit.HasOrder ? 1.7f : 2.5f), minimumRange);

            Vector2 movementTarget = unit.Destination;
            bool move = unit.HasOrder;
            bool firedOrAiming = false;
            if (target != null)
            {
                float distance = Vector2.Distance(unit.Position, target.Position);
                Face(unit, target.Position - unit.Position, dt);
                if (distance < minimumRange)
                {
                    // Explicit artillery focus orders seek firing distance, but a hold
                    // order never retreats or pursues. Scouts can close the firing gap.
                    move = !unit.HoldPosition && unit.AttackTargetId >= 0;
                    Vector2 away = unit.Position - target.Position;
                    if (away.sqrMagnitude < .001f) away = Vector2.up;
                    movementTarget = unit.Position + away.normalized * (minimumRange + .75f - distance);
                }
                else if (distance <= stats.Range)
                {
                    move = false;
                    firedOrAiming = true;
                    if (unit.WeaponTimer <= 0f)
                    {
                        QueueShot(unit, target.Id, -1, target.Position, stats.Damage);
                        unit.WeaponTimer = stats.Delay;
                    }
                }
                else if (!unit.HoldPosition && (unit.AttackMove || unit.AttackTargetId >= 0))
                { movementTarget = target.Position; move = true; }
            }

            if (target == null && unit.AttackMove && !PracticeMode)
            {
                Army targetBase = null;
                if (unit.BaseTargetTeam >= 0 && Armies[unit.BaseTargetTeam].Alive)
                    targetBase = Armies[unit.BaseTargetTeam];
                else
                {
                    for (int i = 0; i < Armies.Count; i++)
                    {
                        Army candidate = Armies[i];
                        if (i != unit.Team && candidate.Alive && (candidate.BasePosition - unit.Position).sqrMagnitude < stats.Range * stats.Range)
                        { targetBase = candidate; break; }
                    }
                }
                if (targetBase != null)
                {
                    float distance = Vector2.Distance(unit.Position, targetBase.BasePosition);
                    if (distance < minimumRange)
                    {
                        move = !unit.HoldPosition;
                        Vector2 away = unit.Position - targetBase.BasePosition;
                        if (away.sqrMagnitude < .001f) away = Vector2.up;
                        movementTarget = unit.Position + away.normalized * (minimumRange + .75f - distance);
                    }
                    else if (distance <= stats.Range + 1.25f)
                    {
                        move = false;
                        firedOrAiming = true;
                        Face(unit, targetBase.BasePosition - unit.Position, dt);
                        if (unit.WeaponTimer <= 0f)
                        {
                            QueueShot(unit, -1, targetBase.Id, targetBase.BasePosition, stats.Damage);
                            unit.WeaponTimer = stats.Delay;
                        }
                    }
                    else if (!unit.HoldPosition) { movementTarget = targetBase.BasePosition; move = true; }
                }
            }

            Vector2 steering = Vector2.zero;
            bool finalDestination = true;
            if (move && !unit.HoldPosition)
            {
                if (MissionActive) movementTarget = Bridge.NextWaypoint(unit, movementTarget, stats.Radius, out finalDestination);
                Vector2 delta = movementTarget - unit.Position;
                float distance = delta.magnitude;
                if (distance > .22f)
                {
                    steering = delta / distance * Mathf.Min(stats.Speed, distance / Mathf.Max(dt, .001f));
                    if (!firedOrAiming) Face(unit, delta, dt);
                }
                else if (finalDestination && target == null && unit.BaseTargetTeam < 0) unit.HasOrder = false;
            }

            // Soft separation keeps miniature bases readable while permitting movement through a crowd.
            Vector2 repel = Vector2.zero;
            for (int i = 0; i < Units.Count; i++)
            {
                Troop other = Units[i];
                if (other == unit || !other.Alive) continue;
                Vector2 away = unit.Position - other.Position;
                float desired = stats.Radius + Specifications(other.Kind).Radius;
                float squaredDistance = away.sqrMagnitude;
                if (squaredDistance >= desired * desired) continue;
                float distance = Mathf.Sqrt(squaredDistance);
                if (distance < .001f)
                    away = new Vector2(Mathf.Sin(unit.Id * 2.39996f), Mathf.Cos(unit.Id * 2.39996f));
                else away /= distance;
                repel += away * (desired - distance) * 3.2f;
            }
            // HQ footprints are solid for all factions, without a navigation dependency.
            for (int i = 0; i < Armies.Count; i++)
            {
                Vector2 away = unit.Position - Armies[i].BasePosition;
                float distance = away.magnitude;
                float desired = 2.1f + stats.Radius;
                if (distance < desired && distance > .001f) repel += away / distance * (desired - distance) * 6f;
            }
            Vector2 proposed = Bound(unit.Position + (steering + Vector2.ClampMagnitude(repel, 3f)) * dt);
            unit.Position = MissionActive ? Bridge.ConstrainMovement(unit, proposed, stats.Radius) : proposed;
        }

        void Face(Troop unit, Vector2 direction, float dt)
        {
            if (direction.sqrMagnitude < .001f) return;
            float targetAngle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            unit.Heading = Mathf.MoveTowardsAngle(unit.Heading, targetAngle, dt * (unit.Kind == UnitKind.Artillery ? 85f : unit.Kind == UnitKind.Tank ? 110f : 360f));
        }

        void QueueShot(Troop unit, int target, int baseTeam, Vector2 to, float damage)
        {
            unit.ShotFlash = 1f;
            pendingHits.Add(new Hit
            {
                TargetId = target, BaseTeam = baseTeam, AttackerTeam = unit.Team, ShooterId = unit.Id, WeaponKind = unit.Kind,
                Damage = damage, From = unit.Position, To = to, Heavy = unit.Kind == UnitKind.Tank || unit.Kind == UnitKind.Artillery
            });
        }

        void Turret(Army army, float dt)
        {
            army.TurretTimer = Mathf.Max(0f, army.TurretTimer - dt);
            if (army.TurretTimer > 0f) return;
            Troop target = NearestEnemy(army.BasePosition, army.Id, 9.2f);
            if (target == null) return;
            army.TurretTimer = 1.15f;
            pendingHits.Add(new Hit
            {
                TargetId = target.Id, BaseTeam = -1, AttackerTeam = army.Id, ShooterId = -1, WeaponKind = UnitKind.Tank, Damage = 20f,
                From = army.BasePosition, To = target.Position, Heavy = true
            });
        }

        void ResolveHits()
        {
            for (int i = 0; i < pendingHits.Count; i++)
            {
                Hit hit = pendingHits[i];
                bool killed = false;
                if (hit.BaseTeam >= 0)
                {
                    if (PracticeMode) continue;
                    Army army = Armies[hit.BaseTeam];
                    if (!army.Alive) continue;
                    float baseDamage = hit.WeaponKind == UnitKind.Gunner ? hit.Damage * .35f : hit.Damage;
                    army.BaseHealth = Mathf.Max(0f, army.BaseHealth - baseDamage);
                    army.SinceDamage = 0f;
                    killed = !army.Alive;
                    if (killed)
                    {
                        Eliminate(army);
                        RecordTurningPoint(Armies[hit.AttackerTeam].Name + " destroyed " + army.Name + " HQ");
                    }
                }
                else
                {
                    Troop target = FindUnit(hit.TargetId);
                    if (target == null || !target.Alive) continue;
                    killed = DamageTroop(target, hit.Damage, hit);
                    if (killed) Armies[hit.AttackerTeam].Credits = Mathf.Min(999f, Armies[hit.AttackerTeam].Credits + 9f);
                    // Heavy shells damage nearby opponents only; all controllers share this rule.
                    if (hit.Heavy)
                    {
                        float splashRadius = hit.WeaponKind == UnitKind.Artillery ? 3.1f : 1.65f;
                        float splashStrength = hit.WeaponKind == UnitKind.Artillery ? .6f : .22f;
                        for (int u = 0; u < Units.Count; u++)
                        {
                            Troop splash = Units[u];
                            if (!splash.Alive || splash.Id == hit.TargetId || splash.Team == hit.AttackerTeam) continue;
                            float distance = Vector2.Distance(splash.Position, hit.To);
                            if (distance < splashRadius)
                                killed |= DamageTroop(splash, hit.Damage * splashStrength * (1f - distance / splashRadius), hit);
                        }
                    }
                }
                Events.Add(new CombatEvent { From = hit.From, To = hit.To, Team = hit.AttackerTeam, ShooterId = hit.ShooterId, WeaponKind = hit.WeaponKind, Heavy = hit.Heavy, Killed = killed });
            }
        }

        bool DamageTroop(Troop target, float damage, Hit hit)
        {
            if (!target.Alive) return false;
            if (IsProtected(target)) damage *= RifleCoverDamageMultiplier;
            // Only actual tank units receive the anti-armour bonus; the HQ gun does not.
            if (hit.ShooterId > 0 && hit.WeaponKind == UnitKind.Tank && target.Kind == UnitKind.Tank)
                damage *= TankArmorDamageMultiplier;
            if (hit.WeaponKind == UnitKind.Gunner)
                damage *= target.Kind == UnitKind.Tank ? .35f : target.Kind == UnitKind.Artillery ? .75f : 1.25f;
            target.Health = Mathf.Max(0f, target.Health - damage);
            if (target.Alive) return false;
            Armies[target.Team].UnitsLost++;
            Armies[hit.AttackerTeam].UnitsDestroyed++;
            return true;
        }

        void RecordTurningPoint(string text)
        {
            LastTurningPoint = text;
            LastTurningPointTime = Elapsed;
        }

        void Eliminate(Army army)
        {
            army.Intent = "Headquarters destroyed";
            army.Credits = 0f;
            for (int i = 0; i < Units.Count; i++)
                if (Units[i].Team == army.Id && Units[i].Alive)
                {
                    // Disbanded survivors are losses, not fictitious kills for an opponent.
                    army.UnitsLost++;
                    Units[i].Health = 0f;
                }
            for (int i = 0; i < Points.Count; i++)
            {
                ControlPoint point = Points[i];
                if (point.Owner == army.Id) point.Owner = -1;
                if (point.CapturingTeam == army.Id) { point.CapturingTeam = -1; point.CaptureProgress = 0f; }
            }
        }

        void UpdatePoint(ControlPoint point, float dt)
        {
            Array.Clear(captureCounts, 0, captureCounts.Length);
            Array.Clear(captureWeights, 0, captureWeights.Length);
            for (int i = 0; i < Units.Count; i++)
            {
                Troop unit = Units[i];
                if (unit.Alive && (unit.Position - point.Position).sqrMagnitude <= CaptureRadius * CaptureRadius)
                {
                    captureCounts[unit.Team]++;
                    captureWeights[unit.Team] += unit.Kind == UnitKind.Scout ? ScoutCaptureWeight : 1f;
                }
            }
            int soleTeam = -1, present = 0;
            for (int i = 0; i < 3; i++) if (captureCounts[i] > 0) { present++; soleTeam = i; }
            point.Contested = present > 1;
            if (present == 1)
            {
                if (soleTeam == point.Owner)
                {
                    point.CapturingTeam = soleTeam;
                    point.CaptureProgress = 1f;
                }
                else
                {
                    if (point.CapturingTeam != soleTeam) { point.CapturingTeam = soleTeam; point.CaptureProgress = 0f; }
                    point.CaptureProgress = Mathf.Min(1f, point.CaptureProgress + dt * .135f * Mathf.Sqrt(Mathf.Min(4f, captureWeights[soleTeam])));
                    if (point.CaptureProgress >= 1f)
                    {
                        point.Owner = soleTeam;
                        Armies[soleTeam].Credits = Mathf.Min(999f, Armies[soleTeam].Credits + 20f);
                        Armies[soleTeam].Score += 5f;
                        Armies[soleTeam].BeaconsCaptured++;
                        RecordTurningPoint(Armies[soleTeam].Name + " captured " + point.Name);
                    }
                }
            }
            else if (present == 0 && point.CapturingTeam != point.Owner)
            {
                point.CaptureProgress = Mathf.Max(0f, point.CaptureProgress - dt * .08f);
                if (point.CaptureProgress <= 0f)
                {
                    point.CapturingTeam = point.Owner;
                    point.CaptureProgress = point.Owner < 0 ? 0f : 1f;
                }
            }
            if (point.Owner >= 0 && !point.Contested && Armies[point.Owner].Alive)
            {
                Armies[point.Owner].Score += dt * 1.1f;
                Armies[point.Owner].BeaconSeconds += dt;
            }
        }

        void CheckVictory()
        {
            if (PracticeMode) return;
            if (MissionActive)
            {
                bool cobalt = missionHold[0] >= MissionHoldRequired, ember = missionHold[1] >= MissionHoldRequired;
                if (cobalt || ember)
                {
                    Winner = cobalt == ember ? -1 : cobalt ? 0 : 1; Finished = true;
                    MissionWinReason = Winner < 0 ? "Both depots secured" : "Enemy depot held for 45 seconds";
                }
                else if (!Armies[0].Alive || !Armies[1].Alive)
                {
                    Winner = Armies[0].Alive == Armies[1].Alive ? -1 : Armies[0].Alive ? 0 : 1; Finished = true;
                    MissionWinReason = Winner < 0 ? "Both headquarters destroyed" : "Enemy headquarters destroyed";
                }
                return;
            }
            int living = 0, survivor = -1, scoreWinner = -1;
            float highestScore = VictoryScore - .0001f;
            for (int i = 0; i < Armies.Count; i++)
            {
                Army army = Armies[i];
                if (!army.Alive) continue;
                living++; survivor = i;
                if (army.Score > highestScore)
                { highestScore = army.Score; scoreWinner = i; }
            }
            if (scoreWinner >= 0) { Winner = scoreWinner; Finished = true; }
            else if (living <= 1) { Winner = survivor; Finished = true; }
        }

        Troop FindUnit(int id)
        {
            for (int i = 0; i < Units.Count; i++) if (Units[i].Id == id) return Units[i];
            return null;
        }

        Troop NearestEnemy(Vector2 position, int team, float radius, float minimumRadius = 0f)
        {
            Troop best = null;
            float distance = radius * radius;
            for (int i = 0; i < Units.Count; i++)
            {
                Troop unit = Units[i];
                if (!unit.Alive || unit.Team == team) continue;
                float candidate = (unit.Position - position).sqrMagnitude;
                if (candidate >= minimumRadius * minimumRadius && candidate < distance) { best = unit; distance = candidate; }
            }
            return best;
        }

        static Vector2 Bound(Vector2 position)
        {
            return new Vector2(Mathf.Clamp(position.x, -MapHalfSize + 1f, MapHalfSize - 1f),
                Mathf.Clamp(position.y, -MapHalfSize + 1f, MapHalfSize - 1f));
        }

        static bool Finite(Vector2 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
        }
    }
}
