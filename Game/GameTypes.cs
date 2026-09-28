namespace CurePlease.Game
{
    // Data shapes returned by GameClient. Member names and types match the EliteAPI library Cure
    // Please was originally written against, so the call sites read the same.
    //
    // COORDINATES keep that original convention: X/Z is the ground plane, Y is HEIGHT.
    // (Windower uses x/y ground + z height; GameClient swaps the axes.)

    public enum LoginStatus
    {
        LoginScreen = 0,
        Loading = 1,
        LoggedIn = 2,
    }

    public class XiEntity
    {
        public uint TargetID { get; set; }          // the entity INDEX, as EliteAPI named it
        public uint ServerID { get; set; }
        public uint ClaimID { get; set; }
        public ushort TargetingIndex { get; set; }
        public string Name { get; set; }
        public byte HealthPercent { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float H { get; set; }
        public float Distance { get; set; }         // 0 for an empty slot
        public uint Status { get; set; }
        public ushort PetIndex { get; set; }
        public int SpawnFlags { get; set; }
        public byte Race { get; set; }
        public float ModelSize { get; set; }
    }

    public class TargetInfo
    {
        public uint TargetIndex;
        public uint TargetId;
        public string TargetName;
        public byte TargetHealthPercent;
        public uint SubTargetIndex;
        public uint SubTargetId;
        public string SubTargetName;
        public bool HasSubTarget;
        public bool LockedOn;
    }

    public class PartyMember
    {
        public byte Index;
        public byte MemberNumber;
        public string Name;
        public uint ID;
        public uint TargetIndex;
        public uint CurrentHP;
        public uint CurrentMP;
        public uint CurrentTP;
        public byte CurrentHPP;
        public byte CurrentMPP;
        public ushort Zone;
        public byte MainJob;
        public byte MainJobLvl;
        public byte SubJob;
        public byte SubJobLvl;
        public byte Active;                         // 0 = empty slot
    }

    public class ISpell
    {
        public ushort Index;                        // spell id
        public ushort ID;                           // recast id
        public ushort MagicType;
        public ushort Element;
        public ushort ValidTargets;
        public ushort Skill;
        public ushort MPCost;
        public byte CastTime;                       // quarter seconds
        public byte RecastDelay;                    // quarter seconds
        public short[] LevelRequired;               // by job id, -1 = the job can't learn it
        public byte Range;
        public string[] Name;
    }

    public class IAbility
    {
        public ushort ID;                           // job abilities: Windower id + 512; weapon skills: raw id
        public ushort TimerID;                      // recast id
        public ushort MP;
        public short TP;
        public ushort ValidTargets;
        public string[] Name;
    }

    public class IItem
    {
        public uint ItemID;
        public ushort Flags;
        public ushort StackSize;
        public ushort ValidTargets;
        public string[] Name;
    }

    public class ChatEntry
    {
        public int ChatType { get; set; }
        public System.Drawing.Color ChatColor { get; set; }
        public System.DateTime Timestamp { get; set; }
        public string Text { get; set; }
    }

    public class PlayerInfo
    {
        public short[] Buffs;
    }

    public class PlayerJobPoints
    {
        public int SpentJobPoints;
    }

    public class PlayerStats
    {
        public short Strength;
        public short Dexterity;
        public short Vitality;
        public short Agility;
        public short Intelligence;
        public short Mind;
        public short Charisma;
    }

    public class CombatSkill
    {
        public ushort Skill;
    }

    public class PlayerCombatSkills
    {
        public CombatSkill Healing;
        public CombatSkill Enhancing;
        public CombatSkill Enfeebling;
        public CombatSkill Divine;
        public CombatSkill Singing;
    }
}
