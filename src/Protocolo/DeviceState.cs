using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRazerProfiler.Protocolo
{
    // Tudo o que o utilitário lê e grava na memória do teclado.
    public sealed class DeviceState
    {
        public string Firmware = "";
        public string Serial = "";
        public byte DeviceMode;
        public byte LayoutCode;
        public PollingKind PollingKind;
        // Família por perfil: taxa em uso agora (só informativa). Família clássica: a taxa do aparelho. Sem polling: 0.
        public byte PollingCode;
        public int SlotCount;
        public byte ActiveProfile;
        public SortedDictionary<byte, ProfileState> Profiles = new SortedDictionary<byte, ProfileState>();   // só os slots que existem

        public DeviceState Clone()
        {
            var c = (DeviceState)MemberwiseClone();
            c.Profiles = new SortedDictionary<byte, ProfileState>();
            foreach (var p in Profiles)
                c.Profiles[p.Key] = p.Value.Clone();
            return c;
        }
    }

    public sealed class ProfileState
    {
        public byte Slot;
        public byte[] Metadata = Array.Empty<byte>();
        public LightingEffect Lighting = LightingEffect.Off();
        public byte Brightness;
        public byte PollingCode;     // só na família de polling por perfil; nas outras fica 0
        public SortedDictionary<byte, KeyBinding> Keys = new SortedDictionary<byte, KeyBinding>();     // camada base
        public SortedDictionary<byte, KeyBinding> FnKeys = new SortedDictionary<byte, KeyBinding>();   // camada Fn

        public string Name => ProfileMetadata.DecodeName(Metadata);

        public SortedDictionary<byte, KeyBinding> Layer(byte layer) => layer == 0 ? Keys : FnKeys;

        public ProfileState Clone()
        {
            var c = (ProfileState)MemberwiseClone();
            c.Metadata = (byte[])Metadata.Clone();
            c.Keys = new SortedDictionary<byte, KeyBinding>(Keys);
            c.FnKeys = new SortedDictionary<byte, KeyBinding>(FnKeys);
            return c;
        }
    }

    public enum ChangeKind
    {
        Polling,
        ActiveProfile,
        CreateProfile,
        DeleteProfile,
        Metadata,
        Lighting,
        Brightness,
        Key,
    }

    public sealed class StateChange
    {
        public StateChange(ChangeKind kind, byte slot = 0, byte layer = 0, byte key = 0)
        {
            Kind = kind;
            Slot = slot;
            Layer = layer;
            Key = key;
        }

        public ChangeKind Kind { get; }
        public byte Slot { get; }
        public byte Layer { get; }
        public byte Key { get; }

        public override string ToString() => $"{Kind} perfil {Slot} camada {Layer} tecla {Key:X2}";
    }

    public static class StateDiff
    {
        // Diferenças exatas (byte a byte) entre dois estados. Um perfil criado conta como um item só.
        // Polling: na família clássica é uma mudança do aparelho (perfil 0); na família por perfil vem em cada perfil.
        public static List<StateChange> Compare(DeviceState from, DeviceState to)
        {
            var changes = new List<StateChange>();
            if (to.PollingKind == PollingKind.Classic && from.PollingCode != to.PollingCode)
                changes.Add(new StateChange(ChangeKind.Polling));
            foreach (var p in to.Profiles.Values)
            {
                if (!from.Profiles.TryGetValue(p.Slot, out var old))
                {
                    changes.Add(new StateChange(ChangeKind.CreateProfile, p.Slot));
                    continue;
                }
                changes.AddRange(CompareProfile(old, p));
            }
            foreach (var slot in from.Profiles.Keys)
                if (!to.Profiles.ContainsKey(slot))
                    changes.Add(new StateChange(ChangeKind.DeleteProfile, slot));
            if (from.ActiveProfile != to.ActiveProfile)
                changes.Add(new StateChange(ChangeKind.ActiveProfile, to.ActiveProfile));
            return changes;
        }

        public static List<StateChange> CompareProfile(ProfileState from, ProfileState to)
        {
            var changes = new List<StateChange>();
            if (from.Metadata == null || !from.Metadata.SequenceEqual(to.Metadata))
                changes.Add(new StateChange(ChangeKind.Metadata, to.Slot));
            if (!Equals(from.Lighting, to.Lighting))
                changes.Add(new StateChange(ChangeKind.Lighting, to.Slot));
            if (from.Brightness != to.Brightness)
                changes.Add(new StateChange(ChangeKind.Brightness, to.Slot));
            if (from.PollingCode != to.PollingCode)
                changes.Add(new StateChange(ChangeKind.Polling, to.Slot));
            foreach (byte layer in new byte[] { 0, 1 })
            {
                var a = from.Layer(layer);
                foreach (var kv in to.Layer(layer))
                    if (!a.TryGetValue(kv.Key, out var old) || !old.Equals(kv.Value))
                        changes.Add(new StateChange(ChangeKind.Key, to.Slot, layer, kv.Key));
            }
            return changes;
        }
    }
}
