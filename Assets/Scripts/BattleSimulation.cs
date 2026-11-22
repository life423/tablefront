using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tablefront
{
    public enum UnitKind { Scout, Rifle, Tank }

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

    public struct CombatEvent
    {
        public Vector2 From, To;
        public int Team;
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

        public readonly List<Army> Armies = new List<Army>(3);
        public readonly List<Troop> Units = new List<Troop>(80);
        public readonly List<ControlPoint> Points = new List<ControlPoint>(3);
        public readonly List<CombatEvent> Events = new List<CombatEvent>(80);
        public float Elapsed;
        public int Winner = -1;
        public bool Finished;
        /// <summary>Training harness sets this to true and supplies actions directly.</summary>
        public bool UseExternalAI;

        readonly System.Random random;
        readonly System.Random[] policyRandom = new System.Random[3];
        readonly List<Hit> pendingHits = new List<Hit>(90);
        readonly List<int> orderScratch = new List<int>(30);
        readonly int[] captureCounts = new int[3];
        int nextId = 1, stepNumber;

        struct Hit
        {
            public int TargetId, BaseTeam, AttackerTeam;
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
                default: return new Stats(115f, 2.85f, 6.4f, 11.5f, .82f, .48f);
            }
        }

        public static int Cost(UnitKind kind)
        {
            switch (kind)
            {
                case UnitKind.Scout: return 55;
                case UnitKind.Tank: return 170;
                default: return 85;
            }
        }

        public BattleSimulation(bool spectator, int seed = 42)
        {
            random = new System.Random(seed);
            for (int i = 0; i < 3; i++) policyRandom[i] = new System.Random(unchecked(seed * 7919 + i * 104729));
            Armies.Add(NewArmy(0, "COBALT", new Vector2(0f, -21f), spectator));
            Armies.Add(NewArmy(1, "EMBER", new Vector2(18.2f, 10.5f), true));
            Armies.Add(NewArmy(2, "VIOLET", new Vector2(-18.2f, 10.5f), true));
            Points.Add(new ControlPoint { Id = 0, Name = "WEST", Position = new Vector2(-10f, -6f) });
            Points.Add(new ControlPoint { Id = 1, Name = "EAST", Position = new Vector2(10f, -6f) });
            Points.Add(new ControlPoint { Id = 2, Name = "NORTH", Position = new Vector2(0f, 12f) });
            foreach (Army army in Armies)
            {
                Spawn(army, UnitKind.Scout);
                Spawn(army, UnitKind.Rifle);
                Spawn(army, UnitKind.Rifle);
                Spawn(army, UnitKind.Tank);
                Spawn(army, UnitKind.Rifle);
                Spawn(army, UnitKind.Scout);
            }
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

        public bool Recruit(int team, UnitKind kind)
        {
            if (Finished || team < 0 || team >= Armies.Count || (int)kind < 0 || (int)kind > 2) return false;
            Army army = Armies[team];
            int cost = Cost(kind);
            if (!army.Alive || army.RecruitCooldown > 0f || army.Credits + .001f < cost || UnitCount(team) >= UnitCap) return false;
            army.Credits -= cost;
            army.RecruitCooldown = kind == UnitKind.Tank ? 3.5f : 2f;
            Spawn(army, kind);
            return true;
        }

        /// <summary>
        /// Strategic policy action. 0..2: send army to point; 3/4: attack cyclic enemy HQ;
        /// 5: defend own HQ; 6..8: paid Scout/Rifle/Tank recruit; 9: hold current orders.
        /// This is an action vocabulary, not a rule-based choice of what the commander should do.
        /// </summary>
        public void ApplyAction(int team, int action)
        {
            if (Finished || team < 0 || team >= Armies.Count || !Armies[team].Alive || action < 0 || action >= ActionCount) return;
            Army army = Armies[team];
            if (action >= 6 && action <= 8)
            {
                UnitKind kind = (UnitKind)(action - 6);
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

        /// <summary>
        /// Fixed normalized features shared by training and inference:
        /// 0..7 own HQ health, credits, score, total/scout/rifle/tank counts, recruit-ready;
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
                    else if (unit.Kind == UnitKind.Rifle) rifles++;
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
                unit.HasOrder = true;
                unit.AttackMove = attackMove;
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
                unit.BaseTargetTeam = -1;
                unit.Destination = target.Position;
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
                Turret(army, dt);
            }

            // Rotate the first mover; hits are resolved together after all units have fired.
            int count = Units.Count;
            for (int pass = 0; pass < count; pass++)
            {
                Troop unit = Units[(pass + stepNumber) % count];
                if (unit.Alive) UpdateUnit(unit, dt);
            }
            ResolveHits();
            for (int i = 0; i < Points.Count; i++) UpdatePoint(Points[i], dt);
            for (int i = Units.Count - 1; i >= 0; i--) if (!Units[i].Alive) Units.RemoveAt(i);
            CheckVictory();
        }

        void UpdateUnit(Troop unit, float dt)
        {
            Stats stats = Specifications(unit.Kind);
            unit.ShotFlash = Mathf.Max(0f, unit.ShotFlash - dt * 5f);
            unit.WeaponTimer = Mathf.Max(0f, unit.WeaponTimer - dt);
            Army home = Armies[unit.Team];
            if (!home.Alive) { unit.Health = 0f; return; }
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
                target = NearestEnemy(unit.Position, unit.Team, stats.Range + (unit.HasOrder ? 1.7f : 2.5f));

            Vector2 movementTarget = unit.Destination;
            bool move = unit.HasOrder;
            bool firedOrAiming = false;
            if (target != null)
            {
                float distance = Vector2.Distance(unit.Position, target.Position);
                Face(unit, target.Position - unit.Position, dt);
                if (distance <= stats.Range)
                {
                    move = false;
                    firedOrAiming = true;
                    if (unit.WeaponTimer <= 0f)
                    {
                        QueueShot(unit, target.Id, -1, target.Position, stats.Damage);
                        unit.WeaponTimer = stats.Delay;
                    }
                }
                else if (unit.AttackMove || unit.AttackTargetId >= 0)
                { movementTarget = target.Position; move = true; }
            }

            if (target == null && unit.AttackMove)
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
                    if (distance <= stats.Range + 1.25f)
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
                    else { movementTarget = targetBase.BasePosition; move = true; }
                }
            }

            Vector2 steering = Vector2.zero;
            if (move)
            {
                Vector2 delta = movementTarget - unit.Position;
                float distance = delta.magnitude;
                if (distance > .22f)
                {
                    steering = delta / distance * Mathf.Min(stats.Speed, distance / Mathf.Max(dt, .001f));
                    if (!firedOrAiming) Face(unit, delta, dt);
                }
                else if (target == null && unit.BaseTargetTeam < 0) unit.HasOrder = false;
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
            unit.Position = Bound(unit.Position + (steering + Vector2.ClampMagnitude(repel, 3f)) * dt);
        }

        void Face(Troop unit, Vector2 direction, float dt)
        {
            if (direction.sqrMagnitude < .001f) return;
            float targetAngle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
            unit.Heading = Mathf.MoveTowardsAngle(unit.Heading, targetAngle, dt * (unit.Kind == UnitKind.Tank ? 110f : 360f));
        }

        void QueueShot(Troop unit, int target, int baseTeam, Vector2 to, float damage)
        {
            unit.ShotFlash = 1f;
            pendingHits.Add(new Hit
            {
                TargetId = target, BaseTeam = baseTeam, AttackerTeam = unit.Team,
                Damage = damage, From = unit.Position, To = to, Heavy = unit.Kind == UnitKind.Tank
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
                TargetId = target.Id, BaseTeam = -1, AttackerTeam = army.Id, Damage = 20f,
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
                    Army army = Armies[hit.BaseTeam];
                    if (!army.Alive) continue;
                    army.BaseHealth = Mathf.Max(0f, army.BaseHealth - hit.Damage);
                    army.SinceDamage = 0f;
                    killed = !army.Alive;
                    if (killed) Eliminate(army);
                }
                else
                {
                    Troop target = FindUnit(hit.TargetId);
                    if (target == null || !target.Alive) continue;
                    target.Health = Mathf.Max(0f, target.Health - hit.Damage);
                    killed = !target.Alive;
                    if (killed) Armies[hit.AttackerTeam].Credits = Mathf.Min(999f, Armies[hit.AttackerTeam].Credits + 9f);
                    // Tank shells damage nearby opponents only. There is no hidden friendly immunity difference by controller.
                    if (hit.Heavy)
                    {
                        for (int u = 0; u < Units.Count; u++)
                        {
                            Troop splash = Units[u];
                            if (!splash.Alive || splash.Id == hit.TargetId || splash.Team == hit.AttackerTeam) continue;
                            float distance = Vector2.Distance(splash.Position, hit.To);
                            if (distance < 1.65f) splash.Health = Mathf.Max(0f, splash.Health - hit.Damage * .22f * (1f - distance / 1.65f));
                        }
                    }
                }
                Events.Add(new CombatEvent { From = hit.From, To = hit.To, Team = hit.AttackerTeam, Heavy = hit.Heavy, Killed = killed });
            }
        }

        void Eliminate(Army army)
        {
            army.Intent = "Headquarters destroyed";
            army.Credits = 0f;
            for (int i = 0; i < Units.Count; i++) if (Units[i].Team == army.Id) Units[i].Health = 0f;
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
            for (int i = 0; i < Units.Count; i++)
            {
                Troop unit = Units[i];
                if (unit.Alive && (unit.Position - point.Position).sqrMagnitude <= CaptureRadius * CaptureRadius) captureCounts[unit.Team]++;
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
                    point.CaptureProgress = Mathf.Min(1f, point.CaptureProgress + dt * .135f * Mathf.Sqrt(Mathf.Min(4, captureCounts[soleTeam])));
                    if (point.CaptureProgress >= 1f)
                    {
                        point.Owner = soleTeam;
                        Armies[soleTeam].Credits = Mathf.Min(999f, Armies[soleTeam].Credits + 20f);
                        Armies[soleTeam].Score += 5f;
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
                Armies[point.Owner].Score += dt * 1.1f;
        }

        void CheckVictory()
        {
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

        Troop NearestEnemy(Vector2 position, int team, float radius)
        {
            Troop best = null;
            float distance = radius * radius;
            for (int i = 0; i < Units.Count; i++)
            {
                Troop unit = Units[i];
                if (!unit.Alive || unit.Team == team) continue;
                float candidate = (unit.Position - position).sqrMagnitude;
                if (candidate < distance) { best = unit; distance = candidate; }
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
