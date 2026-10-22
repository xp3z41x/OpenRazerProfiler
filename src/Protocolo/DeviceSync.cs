using System;
using System.Collections.Generic;
using System.Linq;

namespace OpenRazerProfiler.Protocolo
{
    // Lê o estado completo do teclado e grava só o que mudou, relendo para confirmar.
    public static class DeviceSync
    {
        public static readonly byte[] Layers = { 0, 1 };

        public static DeviceState ReadAll(RazerKeyboard kb, Action<string> progress = null)
        {
            progress?.Invoke("Lendo informações do aparelho…");
            var s = new DeviceState
            {
                Firmware = kb.GetFirmware(),
                Serial = kb.GetSerial(),
                DeviceMode = kb.GetDeviceMode(),
                LayoutCode = kb.GetLayout(),
                PollingKind = kb.Polling,
                PollingCode = kb.Polling == PollingKind.None ? (byte)0 : kb.GetPollingCode(),
                SlotCount = kb.GetProfileSlotCount(),
                ActiveProfile = kb.GetActiveProfile(),
            };
            foreach (byte slot in kb.GetProfiles())
            {
                progress?.Invoke($"Lendo perfil {slot}…");
                s.Profiles[slot] = ReadProfile(kb, slot);
                s.SlotCount = Math.Max(s.SlotCount, slot);
            }
            return s;
        }

        public static ProfileState ReadProfile(RazerKeyboard kb, byte slot) => new ProfileState
        {
            Slot = slot,
            Metadata = kb.ReadProfileMetadata(slot),
            Lighting = kb.GetEffect(slot),
            Brightness = kb.GetBrightness(slot),
            PollingCode = PollingCodes.PerProfile(kb.Polling) ? kb.GetPollingCode(slot) : (byte)0,
            Keys = kb.ReadKeymap(slot, 0),
            FnKeys = kb.ReadKeymap(slot, 1),
        };

        // Grava as diferenças de 'current' (o que está no teclado) para 'desired'.
        // newProfileTemplate devolve o conteúdo que o firmware põe num perfil recém-criado (05 02).
        public static int Apply(RazerKeyboard kb, DeviceState current, DeviceState desired,
            Func<byte, ProfileState> newProfileTemplate, Action<string> progress = null)
        {
            if (!desired.Profiles.ContainsKey(desired.ActiveProfile))
                throw new InvalidOperationException("O perfil ativo precisa existir.");
            int writes = 0;

            // Família clássica: uma taxa só para o aparelho. (Na família por perfil ela vai junto de cada perfil.)
            if (desired.PollingKind == PollingKind.Classic && current.PollingCode != desired.PollingCode)
            {
                progress?.Invoke("Gravando a taxa de polling…");
                kb.SetPollingCode(RazerKeyboard.LiveStorage, desired.PollingCode);
                writes++;
            }

            foreach (var target in desired.Profiles.Values)
            {
                if (!current.Profiles.TryGetValue(target.Slot, out var baseline))
                {
                    progress?.Invoke($"Criando o perfil {target.Slot}…");
                    kb.CreateProfile(target.Slot);
                    writes++;
                    baseline = newProfileTemplate(target.Slot);
                    // 05 02 reinicia luz, brilho e teclas, mas não mexe nos metadados nem no polling,
                    // que ficam com o que havia no slot: sempre regravar os dois.
                    baseline.Metadata = null;
                    if (PollingCodes.PerProfile(desired.PollingKind))
                        baseline.PollingCode = 0;
                }
                writes += WriteProfile(kb, baseline, target, progress);
            }

            if (current.ActiveProfile != desired.ActiveProfile)
            {
                progress?.Invoke($"Ativando o perfil {desired.ActiveProfile}…");
                kb.SetActiveProfile(desired.ActiveProfile);
                writes++;
            }

            foreach (var slot in current.Profiles.Keys.ToList())
            {
                if (desired.Profiles.ContainsKey(slot))
                    continue;
                if (slot == desired.ActiveProfile)
                    throw new InvalidOperationException("O perfil ativo não pode ser apagado.");
                progress?.Invoke($"Apagando o perfil {slot}…");
                kb.DeleteProfile(slot);
                writes++;
            }
            return writes;
        }

        static int WriteProfile(RazerKeyboard kb, ProfileState from, ProfileState to, Action<string> progress)
        {
            int writes = 0;
            foreach (var change in StateDiff.CompareProfile(from, to))
            {
                switch (change.Kind)
                {
                    case ChangeKind.Metadata:
                        progress?.Invoke($"Perfil {to.Slot}: gravando o nome…");
                        kb.WriteProfileMetadata(to.Slot, to.Metadata);
                        break;
                    case ChangeKind.Lighting:
                        progress?.Invoke($"Perfil {to.Slot}: gravando a iluminação…");
                        kb.SetEffect(to.Slot, to.Lighting);
                        break;
                    case ChangeKind.Brightness:
                        progress?.Invoke($"Perfil {to.Slot}: gravando o brilho…");
                        kb.SetBrightness(to.Slot, to.Brightness);
                        break;
                    case ChangeKind.Polling:
                        progress?.Invoke($"Perfil {to.Slot}: gravando a taxa de polling…");
                        kb.SetPollingCode(to.Slot, to.PollingCode);
                        break;
                    case ChangeKind.Key:
                        progress?.Invoke($"Perfil {to.Slot}: gravando teclas…");
                        kb.WriteKey(to.Slot, change.Key, change.Layer, to.Layer(change.Layer)[change.Key]);
                        break;
                }
                writes++;
            }
            return writes;
        }

        public sealed class SaveResult
        {
            public DeviceState State;                                  // estado relido do teclado
            public List<StateChange> Mismatches = new List<StateChange>();   // o que não ficou igual ao pedido
            public int Writes;
        }

        // Grava, relê e confere. Se algo não bater, tenta mais uma vez a partir do que foi relido.
        public static SaveResult Save(RazerKeyboard kb, DeviceState current, DeviceState desired,
            Func<byte, ProfileState> newProfileTemplate, Action<string> progress = null)
        {
            var result = new SaveResult();
            result.Writes = Apply(kb, current, desired, newProfileTemplate, progress);
            progress?.Invoke("Relendo o teclado para conferir…");
            result.State = ReadAll(kb);
            result.Mismatches = StateDiff.Compare(result.State, desired);
            if (result.Mismatches.Count > 0)
            {
                progress?.Invoke("Repetindo o que não conferiu…");
                result.Writes += Apply(kb, result.State, desired, newProfileTemplate, progress);
                result.State = ReadAll(kb);
                result.Mismatches = StateDiff.Compare(result.State, desired);
            }
            return result;
        }
    }
}
