using System.Collections.Generic;
using Newtonsoft.Json;

namespace MSN2Expansion
{
    // ---- pack data (read-only, from packs/<id>/pack.json) ----
    public class Pack
    {
        public string Id, Name, Version, Author, MinFramework;
        public List<Mission> Missions = new List<Mission>();
        [JsonIgnore] public string Dir;
    }

    public class Mission
    {
        public string Id, Title, Giver, Description;
        public Unlock Unlock = new Unlock();
        public List<Line> Intro = new List<Line>();
        public List<Objective> Objectives = new List<Objective>();
        public List<Objective> Bonus = new List<Objective>();
        public Reward Reward = new Reward();
        public List<Line> Outro = new List<Line>();
        [JsonIgnore] public string PackId;
    }

    public class Unlock
    {
        public List<string> After = new List<string>();
        public List<string> Houses = new List<string>();  // HouseZoneIDs that vanilla progression must have opened
        public int MinDay = 1, MinLevel = 0;
        public int FromHour = 0, ToHour = 24;
    }

    public class Line { public string Speaker, Text; }

    // Every non-null filter must match the event. Types: photo, steal, hidden_camera, trap, sex, cum_on, enter,
    // and bonus-only constraints: no_alert (fails when any NPC reaches Alert), within_hours (fails after N game hours).
    public class Objective
    {
        public string Id, Text, Type;
        public string Actor, House, Room, Item, Trap, Activity;
        public bool? Naked;
        public bool Spawn;  // steal only: place the item on a free theft spot in House when the job starts
        public int Count = 1;
        public int Hours;
        public Reward Reward = new Reward();
    }

    public class Reward
    {
        public int Money, Subscribers;
        public Dictionary<string, int> Items = new Dictionary<string, int>();
    }

    // ---- per-save state (sidecar file next to saveNN.dat) ----
    public enum MissionStatus { Locked, Available, Active, Completed }

    public class MissionState
    {
        public MissionStatus Status;
        public Dictionary<string, int> Progress = new Dictionary<string, int>();
        public List<string> BonusFailed = new List<string>();
        public Dictionary<string, string> Spawned = new Dictionary<string, string>();  // objective id -> entity Data.Id
        public float StartedAtMinutes;
        public int CompletedDay = -1;
    }

    public class ExpansionState
    {
        public int Schema = 1;
        public string FrameworkVersion = ExpansionPlugin.Version;
        public string TrackedMission;
        public Dictionary<string, MissionState> Missions = new Dictionary<string, MissionState>();
    }

    // ---- normalized game event ----
    public class GameEvent
    {
        public string Type, Actor, House, Room, Item, Trap, Activity, EntityId;
        public bool Naked;
        public override string ToString() => $"{Type} actor={Actor} house={House} room={Room} activity={Activity} item={Item} trap={Trap} naked={Naked}";
    }
}
