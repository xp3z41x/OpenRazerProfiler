using System;
using System.Collections.Generic;
using OpenRazerProfiler.Aparelhos;
using OpenRazerProfiler.Protocolo;

namespace OpenRazerProfiler.Interface
{
    // Conexão com o teclado, o último estado lido e as edições pendentes.
    sealed class Session : IDisposable
    {
        DeviceCatalog.Connection connection;

        public DeviceDefinition Definition => connection?.Definition;
        public RazerKeyboard Keyboard => connection?.Keyboard;
        public bool Connected => connection != null;
        public DeviceState Device { get; private set; }    // o que está gravado no teclado (última leitura)
        public DeviceState Working { get; private set; }   // o que o usuário quer gravar
        public bool PreviewActive { get; private set; }    // a RAM do teclado mostra uma prévia não gravada

        public List<StateChange> Pending => Device == null ? new List<StateChange>() : StateDiff.Compare(Device, Working);

        public void Read(Action<string> progress)
        {
            if (!Connected)
            {
                progress?.Invoke("Procurando o teclado…");
                connection = DeviceCatalog.Connect() ??
                    throw new InvalidOperationException("Nenhum teclado Razer compatível foi encontrado. Confira o cabo e tente de novo.");
            }
            try
            {
                Device = DeviceSync.ReadAll(Keyboard, progress);
                Working = Device.Clone();
                PreviewActive = false;
            }
            catch (Exception)
            {
                Disconnect();
                throw;
            }
        }

        public DeviceSync.SaveResult Save(Action<string> progress)
        {
            try
            {
                var result = DeviceSync.Save(Keyboard, Device, Working, Definition.NewProfileTemplate, progress);
                Device = result.State;
                if (result.Mismatches.Count == 0)
                    Working = Device.Clone();
                PreviewActive = false;   // gravar no perfil ativo atualiza a luz ao vivo
                return result;
            }
            catch (Exception)
            {
                Disconnect();
                throw;
            }
        }

        public void Discard() => Working = Device?.Clone();

        // Troca os perfis, o perfil ativo e o polling pelos do backup (fica pendente até gravar).
        // O polling segue a família do teclado conectado, não a do arquivo.
        public void LoadBackup(DeviceState backup)
        {
            var kind = Definition.Polling;
            var w = Device.Clone();
            w.Profiles = new SortedDictionary<byte, ProfileState>();
            foreach (var p in backup.Profiles.Values)
            {
                var copy = p.Clone();
                if (!PollingCodes.PerProfile(kind))
                    copy.PollingCode = 0;
                else if (!Definition.OffersPollingCode(copy.PollingCode))
                    throw new FormatException($"O perfil {copy.Slot} do backup tem uma taxa de polling que este teclado não usa (código 0x{copy.PollingCode:X2}).");
                w.Profiles[copy.Slot] = copy;
            }
            if (kind == PollingKind.Classic)
            {
                if (!Definition.OffersPollingCode(backup.PollingCode))
                    throw new FormatException($"O backup tem uma taxa de polling que este teclado não usa (código 0x{backup.PollingCode:X2}).");
                w.PollingCode = backup.PollingCode;
            }
            w.ActiveProfile = backup.ActiveProfile;
            Working = w;
        }

        // Mostra uma iluminação ao vivo (só RAM, nada é gravado).
        public void Preview(LightingEffect effect, byte brightness)
        {
            if (!Connected)
                return;
            Keyboard.SetEffect(RazerKeyboard.LiveStorage, effect);
            Keyboard.SetBrightness(RazerKeyboard.LiveStorage, brightness);
            PreviewActive = true;
        }

        // Devolve à RAM a iluminação gravada no perfil ativo.
        public void EndPreview()
        {
            if (!PreviewActive || !Connected || Device == null || !Device.Profiles.TryGetValue(Device.ActiveProfile, out var p))
                return;
            try
            {
                Keyboard.SetEffect(RazerKeyboard.LiveStorage, p.Lighting);
                Keyboard.SetBrightness(RazerKeyboard.LiveStorage, p.Brightness);
            }
            catch (Exception)
            {
                // teclado desconectado: a RAM se perdeu junto
            }
            PreviewActive = false;
        }

        public void SetNormalMode()
        {
            Keyboard.SetDeviceMode(0x00);
            Device.DeviceMode = Working.DeviceMode = Keyboard.GetDeviceMode();
        }

        public void Disconnect()
        {
            connection?.Keyboard.Dispose();
            connection = null;
        }

        public void Dispose() => Disconnect();
    }
}
