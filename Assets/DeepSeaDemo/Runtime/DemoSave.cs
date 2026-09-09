using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeepSeaDemo
{
    public enum DemoStage { Platform, Dive, Seabed, Submarine, Return, Analysis, Ending }
    [Serializable] public sealed class PropSnapshot
    {
        public string id;
        public Vector3 position;
        public Quaternion rotation;
        public bool submitted;
        public bool lightOn;
    }
    [Serializable] public sealed class DemoSave
    {
        public int version = 1;
        public DemoStage stage;
        public List<string> facts = new();
        public List<PropSnapshot> props = new();
        public Vector3 spawn;
        public float yaw;
        public bool doorOpen;
        public bool engineStarted;
        public string ending;
        public bool Has(string id) => facts.Contains(id);
        public bool Add(string id)
        {
            if (string.IsNullOrEmpty(id) || facts.Contains(id)) return false;
            facts.Add(id); return true;
        }
        public DemoSave Copy() => JsonUtility.FromJson<DemoSave>(JsonUtility.ToJson(this));
    }
}
