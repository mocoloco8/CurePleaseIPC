namespace CurePlease.Game
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;

    // One character's game handle, backed by the Windower "cortana" addon through CortanaIPC.dll.
    // It replaces the EliteAPI object Cure Please used to hold in _ELITEAPIPL / _ELITEAPIMonitored and
    // keeps its tool groups and member names (Player.HPP, Entity.GetEntity(i), Party.GetPartyMembers(),
    // ThirdParty.SendString(...)), so the call sites stay as they were.
    //
    // EliteAPI semantics kept on purpose:
    //   - coordinates use EliteAPI axes (X/Z ground, Y height); Windower's y and z are swapped here;
    //   - absent entities / pet / party slots come back as zeroed objects, never null;
    //   - GetPartyMembers() always returns 18 slots (index = slot, Active 0 = empty);
    //   - recasts are in 1/60 s frames; ability recasts are addressed by slot (GetAbilityIds().IndexOf);
    //   - LoginStatus is LoggedIn only while the addon feed is fresh, so a dropped link reads as zoning.
    public sealed class GameClient
    {
        public string CharacterName { get; private set; }
        public int ProcessId { get; private set; }

        public PlayerTools Player { get; private set; }
        public EntityTools Entity { get; private set; }
        public TargetTools Target { get; private set; }
        public PartyTools Party { get; private set; }
        public RecastTools Recast { get; private set; }
        public ResourceTools Resources { get; private set; }
        public AutoFollowTools AutoFollow { get; private set; }
        public ThirdPartyTools ThirdParty { get; private set; }
        public CastBarTools CastBar { get; private set; }
        public InventoryTools Inventory { get; private set; }
        public ChatTools Chat { get; private set; }

        public GameClient(int processId) : this(processId, CortanaLink.CharacterName(processId)) { }

        public GameClient(int processId, string characterName)
        {
            ProcessId = processId;
            CharacterName = (characterName ?? "").Trim();
            Player = new PlayerTools(this);
            Entity = new EntityTools(this);
            Target = new TargetTools(this);
            Party = new PartyTools(this);
            Recast = new RecastTools(this);
            Resources = new ResourceTools(this);
            AutoFollow = new AutoFollowTools(this);
            ThirdParty = new ThirdPartyTools(this);
            CastBar = new CastBarTools(this);
            Inventory = new InventoryTools(this);
            Chat = new ChatTools(this);
            RunKeepalive.Register(this);
        }

        // The library's character; created on first use, so a client can exist before its addon links.
        private Cortana.CortanaCharacter _char;
        internal Cortana.CortanaCharacter C
        {
            get
            {
                if (_char == null && CharacterName.Length > 0) _char = CortanaLink.Ipc.GetCharacter(CharacterName);
                return _char;
            }
        }
        internal Cortana.CharacterState S { get { var c = C; return c == null ? null : c.State; } }
        internal Cortana.PlayerState P { get { var s = S; return s == null ? null : s.Player; } }

        // Linked and streaming an in-world snapshot.
        public bool IsReady { get { return CortanaLink.IsReady(CharacterName); } }

        private static readonly XiEntity _empty = new XiEntity { Name = "" };

        internal static XiEntity ToXi(Cortana.EntityState e)
        {
            if (e == null) return _empty;
            return new XiEntity
            {
                TargetID = (uint)e.Index,
                ServerID = e.Id,
                Name = e.Name ?? "",
                HealthPercent = (byte)e.Hpp,
                X = e.X, Z = e.Y, Y = e.Z,              // Windower y (ground) = EliteAPI Z; z (height) = Y
                H = e.Heading,
                Distance = e.Distance,
                Status = (uint)e.Status,
                ClaimID = e.ClaimId,
                SpawnFlags = e.SpawnType,
                ModelSize = e.ModelSize,
                PetIndex = (ushort)e.PetIndex,
                TargetingIndex = (ushort)e.TargetIndex,
                Race = (byte)e.Race,
            };
        }

        internal XiEntity EntityAt(int index)
        {
            var s = S;
            return index <= 0 || s == null ? _empty : ToXi(s.GetEntity(index));
        }

        // ================================================================ Player
        public sealed class PlayerTools
        {
            private readonly GameClient g;
            internal PlayerTools(GameClient g) { this.g = g; }

            public string Name { get { return g.CharacterName; } }
            public uint HP { get { var p = g.P; return p == null ? 0 : (uint)p.Hp; } }
            public uint HPP { get { var p = g.P; return p == null ? 0 : (uint)p.Hpp; } }
            public uint MP { get { var p = g.P; return p == null ? 0 : (uint)p.Mp; } }
            public uint MPP { get { var p = g.P; return p == null ? 0 : (uint)p.Mpp; } }
            public uint MPMax { get { var p = g.P; return p == null ? 0 : (uint)p.MaxMp; } }
            public uint TP { get { var p = g.P; return p == null ? 0 : (uint)p.Tp; } }
            public float X { get { var p = g.P; return p == null ? 0 : p.X; } }
            public float Y { get { var p = g.P; return p == null ? 0 : p.Z; } }     // height
            public float Z { get { var p = g.P; return p == null ? 0 : p.Y; } }     // ground
            public uint Status { get { var p = g.P; return p == null ? 0 : (uint)p.Status; } }
            public int ZoneId { get { var p = g.P; return p == null ? 0 : p.Zone; } }
            public byte MainJob { get { var p = g.P; return p == null ? (byte)0 : (byte)p.MainJob; } }
            public byte SubJob { get { var p = g.P; return p == null ? (byte)0 : (byte)p.SubJob; } }
            public byte MainJobLevel { get { var p = g.P; return p == null ? (byte)0 : (byte)p.MainJobLevel; } }
            public byte SubJobLevel { get { var p = g.P; return p == null ? (byte)0 : (byte)p.SubJobLevel; } }
            public uint TargetID { get { var p = g.P; return p == null ? 0 : (uint)p.Index; } }   // own entity index
            public ushort PetIndex { get { var p = g.P; return p == null ? (ushort)0 : (ushort)p.PetIndex; } }
            public XiEntity Pet { get { return g.EntityAt(PetIndex); } }
            public int LoginStatus { get { var s = g.S; return s == null ? 0 : (int)s.Login; } }   // stale feed = Loading

            // 32 slots padded with 255, as EliteAPI returned them.
            public short[] Buffs
            {
                get
                {
                    var b = new short[32];
                    for (int i = 0; i < b.Length; i++) b[i] = 255;
                    var p = g.P;
                    if (p != null) for (int i = 0; i < p.Buffs.Count && i < b.Length; i++) b[i] = (short)p.Buffs[i];
                    return b;
                }
            }

            public PlayerInfo GetPlayerInfo() { return new PlayerInfo { Buffs = Buffs }; }

            // The known-spell / known-ability lists arrive on their own feeds (SSP / SAB) a moment
            // after the link comes up, and an older cortana addon does not send SAB at all. Fail OPEN
            // while a list is still empty: a missing feed must not silently veto every spell / JA.
            public bool HasSpell(uint spellId)
            {
                var s = g.S;
                if (s == null) return false;
                return s.KnownSpells.Count == 0 || s.KnowsSpell((int)spellId);
            }

            // Job-ability ids are Windower id + 512 (IAbility.ID); weapon skills aren't tracked.
            public bool HasAbility(uint abilityId)
            {
                var s = g.S;
                if (s == null || abilityId < 512) return false;
                return s.KnownAbilities.Count == 0 || s.KnowsAbility((int)abilityId - 512);
            }

            public PlayerJobPoints GetJobPoints(int jobId)
            {
                var c = g.C; var x = c == null ? null : c.Extras;
                return new PlayerJobPoints { SpentJobPoints = x == null ? 0 : x.SpentJobPoints(jobId) };
            }

            // Base + gear/buff bonuses, from the character's last stats packet.
            public PlayerStats Stats
            {
                get
                {
                    var c = g.C; var x = c == null ? null : c.Extras;
                    if (x == null) return new PlayerStats();
                    var t = x.TotalStats;
                    return new PlayerStats
                    {
                        Strength = (short)t.Str, Dexterity = (short)t.Dex, Vitality = (short)t.Vit, Agility = (short)t.Agi,
                        Intelligence = (short)t.Int, Mind = (short)t.Mnd, Charisma = (short)t.Chr,
                    };
                }
            }

            public PlayerCombatSkills CombatSkills
            {
                get
                {
                    var c = g.C; var x = c == null ? null : c.Extras;
                    Func<string, CombatSkill> sk = k => new CombatSkill { Skill = (ushort)(x == null ? 0 : x.Skill(k)) };
                    return new PlayerCombatSkills
                    {
                        Healing = sk("healing_magic"), Enhancing = sk("enhancing_magic"), Enfeebling = sk("enfeebling_magic"),
                        Divine = sk("divine_magic"), Singing = sk("singing"),
                    };
                }
            }
        }

        // ================================================================ Entity
        public sealed class EntityTools
        {
            private readonly GameClient g;
            internal EntityTools(GameClient g) { this.g = g; }
            public XiEntity GetEntity(int index) { return g.EntityAt(index); }
            public List<XiEntity> GetEntities()
            {
                var list = new List<XiEntity>();
                var s = g.S;
                if (s != null) foreach (var e in s.Entities()) list.Add(ToXi(e));
                return list;
            }
        }

        // ================================================================ Target
        public sealed class TargetTools
        {
            private readonly GameClient g;
            internal TargetTools(GameClient g) { this.g = g; }

            public TargetInfo GetTargetInfo()
            {
                var t = new TargetInfo { TargetName = "", SubTargetName = "" };
                var p = g.P;
                if (p == null) return t;
                if (p.TargetIndex > 0)
                {
                    var e = g.EntityAt(p.TargetIndex);
                    t.TargetIndex = (uint)p.TargetIndex;
                    t.TargetId = e.ServerID;
                    t.TargetName = e.Name;
                    t.TargetHealthPercent = e.HealthPercent;
                }
                if (p.SubTargetIndex > 0)
                {
                    var e = g.EntityAt(p.SubTargetIndex);
                    t.SubTargetIndex = (uint)p.SubTargetIndex;
                    t.SubTargetId = e.ServerID;
                    t.SubTargetName = e.Name;
                    t.HasSubTarget = true;
                }
                t.LockedOn = p.TargetLocked;
                return t;
            }

            // Moves the reticle (client side), as EliteAPI's SetTarget did.
            public bool SetTarget(int index) { var c = g.C; if (c != null) c.SetTarget(index); return true; }
        }

        // ================================================================ Party
        public sealed class PartyTools
        {
            private readonly GameClient g;
            internal PartyTools(GameClient g) { this.g = g; }

            public const int Slots = 18;

            public List<PartyMember> GetPartyMembers()
            {
                var list = new List<PartyMember>(Slots);
                for (int i = 0; i < Slots; i++) list.Add(Empty(i));
                var s = g.S;
                if (s != null)
                    foreach (var m in s.Party)
                        if (m.Slot >= 0 && m.Slot < Slots) list[m.Slot] = ToMember(m);
                return list;
            }

            public PartyMember GetPartyMember(int slot)
            {
                var s = g.S;
                if (s != null)
                    foreach (var m in s.Party)
                        if (m.Slot == slot) return ToMember(m);
                return Empty(slot);
            }

            private static PartyMember Empty(int slot)
            {
                return new PartyMember { Name = "", Index = (byte)slot, MemberNumber = (byte)slot };
            }

            private static PartyMember ToMember(Cortana.PartyMemberState m)
            {
                return new PartyMember
                {
                    Index = (byte)m.Slot, MemberNumber = (byte)m.Slot, Name = m.Name ?? "", ID = m.Id, TargetIndex = (uint)m.Index,
                    CurrentHP = (uint)m.Hp, CurrentHPP = (byte)m.Hpp, CurrentMP = (uint)m.Mp, CurrentMPP = (byte)m.Mpp,
                    CurrentTP = (uint)m.Tp, Zone = (ushort)m.Zone,
                    MainJob = (byte)m.MainJob, MainJobLvl = (byte)m.MainJobLevel, SubJob = (byte)m.SubJob, SubJobLvl = (byte)m.SubJobLevel,
                    Active = 1,
                };
            }
        }

        // ================================================================ Recast
        public sealed class RecastTools
        {
            private readonly GameClient g;
            internal RecastTools(GameClient g) { this.g = g; }

            // Callers resolve a slot with GetAbilityIds() and read it with GetAbilityRecast(slot) in two
            // calls, so slot numbers must never shift: they are assigned append-only per recast id.
            private readonly object _slotLock = new object();
            private readonly List<int> _slots = new List<int>();

            public int GetSpellRecast(int spellId)
            {
                var s = g.S; var r = s == null ? null : s.Recasts;
                return r == null ? 0 : (int)Math.Round(r.SpellRecast(spellId) * 60f);
            }

            public int GetAbilityRecast(int slot)
            {
                int id;
                lock (_slotLock)
                {
                    if (slot < 0 || slot >= _slots.Count) return 0;
                    id = _slots[slot];
                }
                var s = g.S; var r = s == null ? null : s.Recasts;
                return r == null ? 0 : (int)Math.Ceiling(r.AbilityRecast(id) * 60f);
            }

            public List<int> GetAbilityIds()
            {
                var s = g.S; var r = s == null ? null : s.Recasts;
                lock (_slotLock)
                {
                    if (r != null)
                        foreach (var id in r.AbilitySeconds.Keys)
                            if (!_slots.Contains(id)) _slots.Add(id);
                    return new List<int>(_slots);
                }
            }
        }

        // ================================================================ Resources (Windower res/*.lua; English only)
        public sealed class ResourceTools
        {
            private readonly GameClient g;
            internal ResourceTools(GameClient g) { this.g = g; }

            private static readonly ConcurrentDictionary<string, ISpell> _spells = new ConcurrentDictionary<string, ISpell>(StringComparer.OrdinalIgnoreCase);
            private static readonly ConcurrentDictionary<string, IAbility> _abilities = new ConcurrentDictionary<string, IAbility>(StringComparer.OrdinalIgnoreCase);
            private static readonly ConcurrentDictionary<string, IItem> _items = new ConcurrentDictionary<string, IItem>(StringComparer.OrdinalIgnoreCase);

            private static Cortana.WindowerResources Res { get { return CortanaLink.Ipc.Resources; } }

            public ISpell GetSpell(string name, int language)
            {
                ISpell v;
                if (name == null) return null;
                if (_spells.TryGetValue(name.Trim(), out v)) return v;
                var r = Res; if (r == null) return null;
                v = ToSpell(r.FindSpell(name.Trim()));
                if (v != null) _spells[name.Trim()] = v;
                return v;
            }

            public IAbility GetAbility(string name, int language)
            {
                IAbility v;
                if (name == null) return null;
                if (_abilities.TryGetValue(name.Trim(), out v)) return v;
                var r = Res; if (r == null) return null;
                v = ToAbility(r.FindAbility(name.Trim()));
                if (v != null) _abilities[name.Trim()] = v;
                return v;
            }

            public IItem GetItem(string name, int language)
            {
                IItem v;
                if (name == null) return null;
                if (_items.TryGetValue(name.Trim(), out v)) return v;
                var r = Res; if (r == null) return null;
                v = ToItem(r.FindItem(name.Trim()));
                if (v != null) _items[name.Trim()] = v;
                return v;
            }

            private static ISpell ToSpell(Cortana.SpellInfo s)
            {
                if (s == null) return null;
                var levels = new short[24];
                for (int i = 0; i < levels.Length; i++) levels[i] = -1;
                foreach (var kv in s.Levels) if (kv.Key >= 0 && kv.Key < levels.Length) levels[kv.Key] = (short)kv.Value;
                return new ISpell
                {
                    Index = (ushort)s.Id, ID = (ushort)s.RecastId, MPCost = (ushort)s.MpCost, Element = (ushort)s.Element,
                    ValidTargets = (ushort)s.Targets, Skill = (ushort)s.Skill,
                    CastTime = (byte)Math.Min(255, s.CastTime * 4), RecastDelay = (byte)Math.Min(255, s.Recast * 4),
                    Range = (byte)s.Range, LevelRequired = levels, Name = new[] { s.Name, s.NameJa },
                };
            }

            private static IAbility ToAbility(Cortana.AbilityInfo a)
            {
                if (a == null) return null;
                return new IAbility
                {
                    ID = (ushort)(a.Kind == Cortana.AbilityKind.JobAbility ? a.Id + 512 : a.Id), TimerID = (ushort)a.RecastId,
                    MP = (ushort)a.MpCost, TP = (short)a.TpCost, ValidTargets = (ushort)a.Targets, Name = new[] { a.Name, a.NameJa },
                };
            }

            private static IItem ToItem(Cortana.ItemInfo i)
            {
                if (i == null) return null;
                return new IItem
                {
                    ItemID = (uint)i.Id, StackSize = (ushort)i.Stack, Flags = (ushort)i.Flags, ValidTargets = (ushort)i.Targets,
                    Name = new[] { i.Name, i.NameJa },
                };
            }
        }

        // ================================================================ AutoFollow
        // A direction vector + on/off flag. While on, the direction is streamed to the addon as a RUN
        // lease that RunKeepalive renews, so the character stops by itself if Cure Please goes away.
        public sealed class AutoFollowTools
        {
            private readonly GameClient g;
            internal AutoFollowTools(GameClient g) { this.g = g; }

            private volatile bool _following;
            private int _zone;
            private float _dirX, _dirZ;
            private DateTime _lastSent = DateTime.MinValue;
            private double _lastAngle = double.NaN;
            private readonly object _lock = new object();

            // The client drops auto-follow on zoning, death or an event; mirror that.
            private bool StillValid()
            {
                var s = g.S; var p = s == null ? null : s.Player;
                if (p == null || s.Login != Cortana.LoginState.InWorld || p.Zone != _zone) return false;
                return p.Status == 0 || p.Status == 1;
            }

            public bool IsAutoFollowing
            {
                get
                {
                    if (_following && !StillValid()) _following = false;
                    return _following;
                }
                set
                {
                    if (value == _following) return;
                    if (value)
                    {
                        var p = g.P;
                        _zone = p == null ? 0 : p.Zone;
                        _following = true;
                        SendRun(true);
                    }
                    else
                    {
                        _following = false;
                        var c = g.C; if (c != null) c.StopRunning();
                    }
                }
            }

            public bool SetAutoFollowCoords(float dx, float dy, float dz)
            {
                lock (_lock) { _dirX = dx; _dirZ = dz; }
                if (_following) SendRun(false);
                return true;
            }

            internal void SendRun(bool force)
            {
                double ang;
                lock (_lock)
                {
                    if (_dirX == 0f && _dirZ == 0f) return;
                    ang = Cortana.CortanaCharacter.HeadingTo(0, 0, _dirX, _dirZ);   // EliteAPI ground X/Z = Windower x/y
                    var now = DateTime.UtcNow;
                    double d = double.IsNaN(_lastAngle) ? 10 : Math.Abs(Math.IEEERemainder(ang - _lastAngle, 2 * Math.PI));
                    if (!force && d <= 0.035 && (now - _lastSent).TotalMilliseconds < 400) return;
                    _lastAngle = ang;
                    _lastSent = now;
                }
                var c = g.C; if (c != null) c.Run(ang);
            }
        }

        // One background thread renews the RUN lease of every following character.
        private static class RunKeepalive
        {
            private static readonly List<WeakReference> _clients = new List<WeakReference>();
            private static Thread _thread;

            public static void Register(GameClient client)
            {
                lock (_clients)
                {
                    _clients.Add(new WeakReference(client));
                    if (_thread != null) return;
                    _thread = new Thread(Loop) { IsBackground = true, Name = "CortanaRunKeepalive" };
                    _thread.Start();
                }
            }

            private static void Loop()
            {
                while (true)
                {
                    Thread.Sleep(450);
                    var live = new List<GameClient>();
                    lock (_clients)
                    {
                        _clients.RemoveAll(w => !w.IsAlive);
                        foreach (var w in _clients) { var c = w.Target as GameClient; if (c != null) live.Add(c); }
                    }
                    foreach (var c in live)
                    {
                        try { if (c.AutoFollow.IsAutoFollowing) c.AutoFollow.SendRun(false); } catch { }
                    }
                }
            }
        }

        // ================================================================ ThirdParty (input)
        public sealed class ThirdPartyTools
        {
            private readonly GameClient g;
            internal ThirdPartyTools(GameClient g) { this.g = g; }

            // Any chat line or /command; "//cmd" runs a Windower command.
            public void SendString(string text) { var c = g.C; if (c != null) c.Chat(text); }

            // Accept a pending Raise / Reraise (the "Revival" prompt). Harmless when none is pending.
            public void AcceptRaise() { var c = g.C; if (c != null) c.AcceptRaise(); }
        }

        // ================================================================ Cast bar
        public sealed class CastBarTools
        {
            private readonly GameClient g;
            internal CastBarTools(GameClient g) { this.g = g; }
            // 0 idle, rising while casting, 1.0 briefly once the cast completes.
            public float Percent { get { var p = g.P; return p == null ? 0f : p.CastProgress; } }
        }

        // ================================================================ Inventory
        public sealed class InventoryTools
        {
            private readonly GameClient g;
            internal InventoryTools(GameClient g) { this.g = g; }

            public int GetItemCount(int itemId)
            {
                var s = g.S; var inv = s == null ? null : s.Inventory;
                return inv == null ? 0 : inv.CountOf(itemId);
            }

            public int GetTempItemCount(int itemId)
            {
                var c = g.C; var x = c == null ? null : c.Extras;
                return x == null ? 0 : x.TemporaryItemCount(itemId);
            }
        }

        // ================================================================ Chat log
        public sealed class ChatTools
        {
            private readonly GameClient g;
            internal ChatTools(GameClient g) { this.g = g; }

            public ChatEntry GetNextChatLine()
            {
                var s = g.S;
                Cortana.ChatLine line;
                if (s == null || !s.TryReadChat(out line)) return null;
                return new ChatEntry
                {
                    ChatType = line.Mode, ChatColor = System.Drawing.Color.Black,
                    Timestamp = line.ReceivedAt.ToLocalTime(), Text = line.Text,
                };
            }
        }
    }
}
